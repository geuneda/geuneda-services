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

			// 풀 서비스가 제대로 해제하기 전에 부모 객체가 대기 중인 엔티티/게임 오브젝트를 파괴했을 수 있으므로
			// 무효한 항목은 건너뜁니다. T 가 class 제약뿐이라 == 는 참조 비교가 되므로,
			// 파괴된 Unity 객체(fake null)는 Despawn 과 같이 Equals(null) 로 걸러냅니다
			while (_stack.Count > 0)
			{
				var candidate = _stack.Pop();

				if (!IsNullOrDestroyed(candidate))
				{
					entity = candidate;
					break;
				}
			}

			if (entity == null)
			{
				entity = CallInstantiator();

				// 새로 만든 엔티티까지 무효하면 다시 만들어도 같은 결과일 수 있으므로 재시도하지 않습니다
				if (IsNullOrDestroyed(entity))
				{
					throw new InvalidOperationException(
						$"The pool instantiator returned a null or destroyed {typeof(T).Name} instance");
				}
			}

			SpawnedEntities.Add(entity);

			return entity;
		}

		private static bool IsNullOrDestroyed(T entity)
		{
			return entity == null || entity.Equals(null);
		}

		protected virtual void PostDespawnEntity(T entity) { }

		protected T CallInstantiator()
		{
			var entity = _instantiator.Invoke(SampleEntity);

			// 무효한 인스턴스(파괴된 Unity 객체 등)에는 Init 훅을 부르지 않습니다.
			// Spawn 은 이 결과를 다시 확인해 예외를 던지고, 생성자/Reset 은 스택에 넣어 Spawn 때 건너뜁니다
			if (IsNullOrDestroyed(entity))
			{
				return entity;
			}

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
