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

				// Despawn(entity) 는 SpawnedEntities 에서 첫 번째 항목을 제거해 뒤 항목들을 한 칸씩 당깁니다.
				// 다음 반복에서 현재 인덱스를 다시 보도록 한 칸 물러서지 않으면 연속으로 일치하는 항목을 건너뜁니다.
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
			// 풀 서비스가 제대로 해제하기 전에 부모 객체가 엔티티/게임 오브젝트를 파괴했을 수 있으므로
			// while 루프로 확인해야 합니다
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
