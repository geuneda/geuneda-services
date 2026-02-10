using System;
using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <summary>
	/// 서로 다른 타입의 여러 풀을 관리할 수 있는 서비스입니다.
	/// 동일한 타입의 풀은 하나만 가질 수 있습니다.
	/// </summary>
	public interface IPoolService : IDisposable
	{
		/// <summary>
		/// <typeparamref name="T"/> 타입의 오브젝트 풀을 가져옵니다.
		/// 풀이 존재하지 않으면 <see cref="ArgumentException"/>이 발생합니다.
		/// </summary>
		/// <typeparam name="T">풀의 오브젝트 타입입니다.</typeparam>
		/// <returns>오브젝트 풀입니다.</returns>
		/// <exception cref="ArgumentException">풀이 존재하지 않으면 발생합니다.</exception>
		IObjectPool<T> GetPool<T>() where T : class;

		/// <summary>
		/// <typeparamref name="T"/> 타입의 오브젝트 풀을 가져오려고 시도합니다.
		/// </summary>
		/// <typeparam name="T">풀의 오브젝트 타입입니다.</typeparam>
		/// <param name="pool">오브젝트 풀이거나, 존재하지 않으면 null입니다.</param>
		/// <returns>풀이 존재하면 true, 그렇지 않으면 false입니다.</returns>
		bool TryGetPool<T>(out IObjectPool<T> pool) where T : class;

		/// <summary>
		/// 서비스에 <typeparamref name="T"/> 타입의 새 오브젝트 풀을 추가합니다.
		/// 동일한 타입의 풀이 이미 존재하면 <see cref="ArgumentException"/>이 발생합니다.
		/// </summary>
		/// <typeparam name="T">풀의 오브젝트 타입입니다.</typeparam>
		/// <param name="pool">추가할 오브젝트 풀입니다.</param>
		/// <exception cref="ArgumentException">동일한 타입의 풀이 이미 존재하면 발생합니다.</exception>
		void AddPool<T>(IObjectPool<T> pool) where T : class;

		/// <summary>
		/// 서비스에서 <typeparamref name="T"/> 타입의 오브젝트 풀을 제거합니다.
		/// </summary>
		/// <typeparam name="T">풀의 오브젝트 타입입니다.</typeparam>
		void RemovePool<T>() where T : class;

		/// <inheritdoc cref="IObjectPool{T}.Spawn"/>
		T Spawn<T>() where T : class;

		/// <inheritdoc cref="IObjectPool{T}.Spawn{TData}"/>
		T Spawn<T, TData>(TData data) where T : class, IPoolEntitySpawn<TData>;

		/// <inheritdoc cref="IObjectPool{T}.Despawn(T)"/>
		bool Despawn<T>(T entity) where T : class;

		/// <summary>
		/// <typeparamref name="T"/> 타입의 풀에서 모든 엔티티를 디스폰합니다.
		/// </summary>
		/// <typeparam name="T">디스폰할 엔티티의 타입입니다.</typeparam>
		/// <exception cref="ArgumentException">
		/// 서비스에 주어진 <typeparamref name="T"/> 타입의 풀이 없으면 발생합니다.
		/// </exception>
		void DespawnAll<T>() where T : class;

		/// <summary>
		/// 이 서비스의 내용을 비웁니다.
		/// 모든 풀을 반환하여 개별적으로 해제할 수 있도록 합니다.
		/// </summary>
		/// <returns>
		/// 이 서비스의 모든 풀을 포함하는 딕셔너리로, 키는 풀의 타입이고 값은 풀 자체입니다.
		/// </returns>
		IDictionary<Type, IObjectPool> Clear();

		/// <inheritdoc cref="IObjectPool{T}.Dispose(bool)"/>
		void Dispose<T>(bool disposeSampleEntity) where T : class;
	}

	/// <inheritdoc />
	public class PoolService : IPoolService
	{
		private readonly IDictionary<Type, IObjectPool> _pools = new Dictionary<Type, IObjectPool>();

		/// <inheritdoc />
		public IObjectPool<T> GetPool<T>() where T : class
		{
			if (!TryGetPool<T>(out var pool))
			{
				throw new ArgumentException("The pool was not initialized for the type " + typeof(T));
			}

			return pool;
		}

		/// <inheritdoc />
		public bool TryGetPool<T>(out IObjectPool<T> pool) where T : class
		{
			var ret = _pools.TryGetValue(typeof(T), out var innerPool);

			pool = innerPool as IObjectPool<T>;

			return ret;
		}

		/// <inheritdoc />
		public void AddPool<T>(IObjectPool<T> pool) where T : class
		{
			_pools.Add(typeof(T), pool);
		}

		/// <inheritdoc />
		public void RemovePool<T>() where T : class
		{
			_pools.Remove(typeof(T));
		}

		/// <inheritdoc />
		public T Spawn<T>() where T : class
		{
			return GetPool<T>().Spawn();
		}

		/// <inheritdoc />
		public T Spawn<T, TData>(TData data) where T : class, IPoolEntitySpawn<TData>
		{
			return GetPool<T>().Spawn(data);
		}

		/// <inheritdoc />
		public bool Despawn<T>(T entity) where T : class
		{
			return GetPool<T>().Despawn(entity);
		}

		/// <inheritdoc />
		public void DespawnAll<T>() where T : class
		{
			GetPool<T>().DespawnAll();
		}

		/// <inheritdoc />
		public IDictionary<Type, IObjectPool> Clear()
		{
			var ret = new Dictionary<Type, IObjectPool>(_pools);

			_pools.Clear();

			return ret;
		}

		/// <inheritdoc />
		public void Dispose<T>(bool disposeSampleEntity) where T : class
		{
			GetPool<T>().Dispose(disposeSampleEntity);
			RemovePool<T>();
		}

		/// <inheritdoc />
		public void Dispose()
		{
			foreach (var pool in _pools)
			{
				pool.Value.Dispose();
			}

			_pools.Clear();
		}
	}
}