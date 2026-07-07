using System;
using System.Collections.Generic;
using Geuneda.Services.Pooling;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <inheritdoc />
	public class PoolService : IPoolService
	{
		private readonly IDictionary<Type, IObjectPool> _pools = new Dictionary<Type, IObjectPool>();

		internal IReadOnlyDictionary<Type, IObjectPool> Pools => (IReadOnlyDictionary<Type, IObjectPool>)_pools;

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
