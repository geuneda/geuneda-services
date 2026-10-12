using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services.Pooling
{
	/// <summary>
	/// 서로 다른 타입의 풀 여러 개를 관리하는 서비스입니다.
	/// 같은 타입의 풀은 하나만 가질 수 있습니다.
	/// </summary>
	public interface IPoolService : IDisposable
	{
		/// <summary>
		/// <typeparamref name="T"/> 타입 객체의 풀을 가져옵니다.
		/// 풀이 없으면 <see cref="ArgumentException"/> 을 던집니다.
		/// </summary>
		/// <typeparam name="T">풀에 담긴 객체의 타입.</typeparam>
		/// <returns>객체 풀.</returns>
		/// <exception cref="ArgumentException">풀이 없을 때 발생합니다.</exception>
		IObjectPool<T> GetPool<T>() where T : class;

		/// <summary>
		/// <typeparamref name="T"/> 타입 객체의 풀을 가져오려고 시도합니다.
		/// </summary>
		/// <typeparam name="T">풀에 담긴 객체의 타입.</typeparam>
		/// <param name="pool">객체 풀. 없으면 null.</param>
		/// <returns>풀이 있으면 true, 없으면 false.</returns>
		bool TryGetPool<T>(out IObjectPool<T> pool) where T : class;

		/// <summary>
		/// <typeparamref name="T"/> 타입 객체의 새 풀을 서비스에 추가합니다.
		/// 같은 타입의 풀이 이미 있으면 <see cref="ArgumentException"/> 을 던집니다.
		/// </summary>
		/// <typeparam name="T">풀에 담긴 객체의 타입.</typeparam>
		/// <param name="pool">추가할 객체 풀.</param>
		/// <exception cref="ArgumentException">같은 타입의 풀이 이미 있을 때 발생합니다.</exception>
		void AddPool<T>(IObjectPool<T> pool) where T : class;

		/// <summary>
		/// <typeparamref name="T"/> 타입 객체의 풀을 서비스에서 제거합니다.
		/// </summary>
		/// <typeparam name="T">풀에 담긴 객체의 타입.</typeparam>
		void RemovePool<T>() where T : class;

		/// <inheritdoc cref="IObjectPool{T}.Spawn"/>
		T Spawn<T>() where T : class;

		/// <inheritdoc cref="IObjectPool{T}.Spawn{TData}"/>
		T Spawn<T, TData>(TData data) where T : class, IPoolEntitySpawn<TData>;

		/// <inheritdoc cref="IObjectPool{T}.Despawn(T)"/>
		bool Despawn<T>(T entity) where T : class;

		/// <summary>
		/// <typeparamref name="T"/> 타입 풀의 모든 엔티티를 디스폰합니다.
		/// </summary>
		/// <typeparam name="T">디스폰할 엔티티의 타입.</typeparam>
		/// <exception cref="ArgumentException">
		/// 서비스에 주어진 <typeparamref name="T"/> 타입의 풀이 없을 때 발생합니다.
		/// </exception>
		void DespawnAll<T>() where T : class;

		/// <summary>
		/// 이 서비스의 내용을 비웁니다.
		/// 모든 풀을 반환하므로 따로 해제할 수 있습니다.
		/// </summary>
		/// <returns>
		/// 이 서비스의 모든 풀을 담은 딕셔너리. 키는 풀의 타입, 값은 풀 자체입니다.
		/// </returns>
		IDictionary<Type, IObjectPool> Clear();

		/// <inheritdoc cref="IObjectPool{T}.Dispose(bool)"/>
		void Dispose<T>(bool disposeSampleEntity) where T : class;
	}
}
