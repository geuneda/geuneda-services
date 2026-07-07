using System;
using System.Collections.Generic;

// ReSharper disable CheckNamespace

namespace Geuneda.Services.Pooling
{
	/// <inheritdoc />
	public abstract class ObjectPoolBase<T> : IObjectPool<T> where T : class
	{
		protected readonly IList<T> SpawnedEntities = new List<T>();

		private readonly Stack<T> _stack;
		private readonly Func<T, T> _instantiator;
		
		private T _sampleEntity;
		
		/// <inheritdoc />
		public T SampleEntity => _sampleEntity;

		/// <inheritdoc />
		public IReadOnlyList<T> SpawnedReadOnly => SpawnedEntities as IReadOnlyList<T>;

		protected ObjectPoolBase(uint initSize, T sampleEntity, Func<T, T> instantiator)
		{
			_sampleEntity = sampleEntity;
			_instantiator = instantiator;
			_stack = new Stack<T>((int)initSize);

			for (var i = 0; i < initSize; i++)
			{
				_stack.Push(CallInstantiator());
			}
		}

		/// <inheritdoc />
		public bool IsSpawned(Func<T, bool> conditionCheck)
		{
			for (var i = 0; i < SpawnedEntities.Count; i++)
			{
				if (conditionCheck(SpawnedEntities[i]))
				{
					return true;
				}
			}

			return false;
		}

		/// <inheritdoc />
		public void Reset(uint initSize, T sampleEntity)
		{
			Dispose();
			
			_sampleEntity = sampleEntity;

			for (var i = 0; i < initSize; i++)
			{
				_stack.Push(CallInstantiator());
			}
		}

		/// <inheritdoc />
		public List<T> Clear()
		{
			var ret = new List<T>(SpawnedEntities);

			ret.AddRange(_stack);
			SpawnedEntities.Clear();
			_stack.Clear();

			return ret;
		}

		/// <inheritdoc />
		public void DespawnAll()
		{
			for (var i = SpawnedEntities.Count - 1; i > -1; i--)
			{
				Despawn(SpawnedEntities[i]);
			}
		}

		public virtual void Dispose(bool disposeSampleEntity)
		{
			if (disposeSampleEntity)
			{
				_sampleEntity = null;
			}
			
			Dispose();
		}

		/// <inheritdoc />
		public T Spawn()
		{
			var entity = SpawnEntity();

			CallOnSpawned(entity);

			return entity;
		}

		/// <inheritdoc />
		public T Spawn<TData>(TData data)
		{
			var entity = SpawnEntity();

			CallOnSpawned(entity);
			CallOnSpawned(entity, data);

			return entity;
		}

		/// <inheritdoc />
		public bool Despawn(T entity)
		{
			if (!SpawnedEntities.Remove(entity) || entity == null || entity.Equals(null))
			{
				return false;
			}

			_stack.Push(entity);
			CallOnDespawned(entity);
			PostDespawnEntity(entity);

			return true;
		}

		/// <inheritdoc />
		public bool Despawn(bool onlyFirst, Func<T, bool> entityGetter)
		{
			var despawned = false;

			for (var i = 0; i < SpawnedEntities.Count; i++)
			{
				if (!entityGetter(SpawnedEntities[i]))
				{
					continue;
				}

				// Despawn(entity) removes the first occurrence from SpawnedEntities, shifting
				// subsequent items down by one. Step back so the next iteration revisits the
				// current index, otherwise adjacent matches would be skipped.
				if (Despawn(SpawnedEntities[i]))
				{
					despawned = true;
					i--;
				}

				if (onlyFirst)
				{
					break;
				}
			}

			return despawned;
		}

		/// <inheritdoc />
		public virtual void Dispose()
		{
			Clear();
		}

		protected virtual T SpawnEntity()
		{
			T entity = null;

			do
			{
				entity = _stack.Count == 0 ? CallInstantiator() : _stack.Pop();
			}
			// Need to do while loop and check as parent objects could have destroyed the entity/gameobject before it could
			// be properly disposed by pool service
			while (entity == null);

			SpawnedEntities.Add(entity);

			return entity;
		}

		protected virtual void PostDespawnEntity(T entity) { }

		protected T CallInstantiator()
		{
			var entity = _instantiator.Invoke(SampleEntity);
			var poolEntity = entity as IPoolEntityObject<T>;

			poolEntity?.Init(this);

			return entity;
		}

		protected virtual void CallOnSpawned(T entity)
		{
			var poolEntity = entity as IPoolEntitySpawn;

			poolEntity?.OnSpawn();
		}

		protected virtual void CallOnSpawned<TData>(T entity, TData data)
		{
			var poolEntity = entity as IPoolEntitySpawn<TData>;

			poolEntity?.OnSpawn(data);
		}

		protected virtual void CallOnDespawned(T entity)
		{
			var poolEntity = entity as IPoolEntityDespawn;

			poolEntity?.OnDespawn();
		}
	}

	/// <inheritdoc />
	public class ObjectPool<T> : ObjectPoolBase<T> where T : class
	{
		public ObjectPool(uint initSize, T sampleEntity, Func<T, T> instantiator) : base(initSize, sampleEntity, instantiator)
        {
        }
		
		public ObjectPool(uint initSize, Func<T> instantiator) : base(initSize, instantiator(), entityRef => instantiator.Invoke())
		{
		}
	}
}
