using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <summary>
	/// 풀링된 오브젝트가 스폰될 때 알림을 받을 수 있게 하는 인터페이스
	/// </summary>
	public interface IPoolEntitySpawn
	{
		/// <summary>
		/// 엔티티가 스폰될 때 호출됩니다
		/// </summary>
		void OnSpawn();
	}

	/// <inheritdoc cref="IPoolEntitySpawn"/>
	/// <remarks>
	/// 주어진 <typeparamref name="T"/> <paramref name="data"/>와 함께 풀링된 오브젝트를 스폰할 수 있는 인터페이스
	/// </remarks>
	public interface IPoolEntitySpawn<T>
	{
		/// <inheritdoc cref="IPoolEntitySpawn.OnSpawn"/>
		/// <remarks>
		/// 주어진 <typeparamref name="T"/> <paramref name="data"/>와 함께 풀링된 오브젝트를 스폰합니다
		/// </remarks>
		void OnSpawn(T data);
	}

	/// <summary>
	/// 풀링된 오브젝트가 디스폰될 때 알림을 받을 수 있게 하는 인터페이스
	/// </summary>
	public interface IPoolEntityDespawn
	{
		/// <summary>
		/// 엔티티가 디스폰될 때 호출됩니다
		/// </summary>
		void OnDespawn();
	}

	/// <summary>
	/// 디스폰 호출의 참조를 유지하여 자체 디스폰할 수 있게 하는 인터페이스
	/// </summary>
	/// <remarks>
	/// 이 클래스의 구현:
	/// <code>
	/// public class MyObjectPool : IPoolEntityObject<typeparamref name="T"/>
	/// {
	///		private IObjectPool<typeparamref name="T"/> _pool;
	///		
	/// 	public void Init(IObjectPool<typeparamref name="T"/> pool)
	/// 	{
	/// 		_pool = pool;
	/// 	}
	/// 	
	/// 	public bool Despawn()
	/// 	{
	/// 		return _pool.Despawn(this);
	/// 	}	
	/// }
	/// </code>
	/// </remarks>
	public interface IPoolEntityObject<T> where T : class
	{
		/// <summary>
		/// <see cref="IObjectPool{T}"/>에 의해 주어진 <paramref name="pool"/>로 초기화하기 위해 호출됩니다
		/// </summary>
		void Init(IObjectPool<T> pool);

		/// <summary>
		/// 이 풀링된 오브젝트를 디스폰합니다
		/// </summary>
		bool Despawn();
	}

	/// <summary>
	/// 모든 타입의 엔티티 오브젝트를 처리할 수 있는 간단한 오브젝트 풀 구현
	/// </summary>
	public interface IObjectPool : IDisposable
	{
		/// <summary>
		/// 활성화된 모든 스폰 엔티티를 디스폰하고 나중에 재사용할 수 있도록 풀에 반환합니다.
		/// 이 함수는 엔티티를 리셋하지 않습니다. 리셋이 필요하면 <see cref="IPoolEntityDespawn"/>을 구현하거나 외부에서 처리하세요.
		/// </summary>
		void DespawnAll();
		
		/// <inheritdoc cref="IDisposable.Dispose"/>
		/// <remarks>
		/// <paramref name="disposeSampleEntity"/> 값에 따라 샘플 엔티티도 해제합니다
		/// </remarks>
		void Dispose(bool disposeSampleEntity);
	}

	/// <inheritdoc />
	public interface IObjectPool<T> : IObjectPool where T : class
	{
		/// <summary>
		/// 풀링된 엔티티를 생성하는 데 사용되는 엔티티 참조
		/// </summary>
		T SampleEntity { get; }
		
		/// <summary>
		/// 이미 스폰된 요소의 컬렉션을 읽기 전용 리스트로 요청합니다
		/// </summary>
		IReadOnlyList<T> SpawnedReadOnly { get; }

		/// <summary>
		/// 풀에 주어진 <paramref name="conditionCheck"/>와 일치하는 엔티티가 있는지 확인합니다
		/// </summary>
		bool IsSpawned(Func<T, bool> conditionCheck);

		/// <summary>
		/// 풀의 모든 엔티티를 비우고 주어진 <paramref name="initSize"/>로 리셋합니다
		/// </summary>
		void Reset(uint initSize, T sampleEntity);

		/// <summary>
		/// 주어진 <typeparamref name="T"/> 타입의 엔티티를 스폰하여 반환합니다.
		/// 이 함수는 엔티티를 초기화하지 않습니다. 초기화가 필요하면 <see cref="IPoolEntitySpawn"/>을 구현하거나 외부에서 처리하세요.
		/// 풀이 비어있으면 <exception cref="StackOverflowException" />을 발생시킵니다.
		/// </summary>
		T Spawn();

		/// <inheritdoc cref="Spawn"/>
		/// <remarks>
		/// 주어진 <typeparamref name="T"/> <paramref name="data"/>와 함께 풀링된 오브젝트를 스폰합니다
		/// </remarks>
		T Spawn<TData>(TData data);

		/// <summary>
		/// 주어진 <paramref name="entityGetter"/> 조건에 유효한 엔티티를 디스폰하고 나중에 재사용할 수 있도록 풀에 반환합니다.
		/// <paramref name="onlyFirst"/>가 true이면 하나의 엔티티만 디스폰하고 추가로 조건에 맞는 엔티티를 찾지 않습니다.
		/// 이 함수는 엔티티를 리셋하지 않습니다. 리셋이 필요하면 <see cref="IPoolEntityDespawn"/>을 구현하거나 외부에서 처리하세요.
		/// 엔티티를 풀에 성공적으로 디스폰하면 true, 그렇지 않으면 false를 반환합니다.
		/// </summary>
		bool Despawn(bool onlyFirst, Func<T, bool> entityGetter);

		/// <summary>
		/// 주어진 <paramref name="entity"/>를 디스폰하고 나중에 재사용할 수 있도록 풀에 반환합니다.
		/// 이 함수는 엔티티를 리셋하지 않습니다. 리셋이 필요하면 <see cref="IPoolEntityDespawn"/>을 구현하거나 외부에서 처리하세요.
		/// 엔티티를 풀에 성공적으로 디스폰하면 true, 그렇지 않으면 false를 반환합니다.
		/// </summary>
		bool Despawn(T entity);

		/// <summary>
		/// 이 풀의 내용을 비웁니다.
		/// 풀 내용을 반환하여 개별적으로 해제할 수 있도록 합니다.
		/// </summary>
		List<T> Clear();
	}

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

				despawned = Despawn(SpawnedEntities[i]);

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
			// 부모 오브젝트가 풀 서비스에 의해 적절히 해제되기 전에 엔티티/게임 오브젝트를 파괴했을 수 있으므로
			// while 루프로 확인이 필요합니다
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

	/// <inheritdoc />
	/// <remarks>
	/// 오브젝트 참조를 사용하여 새 <see cref="GameObject"/>를 생성하는 풀에 유용합니다
	/// </remarks>
	public class GameObjectPool : ObjectPoolBase<GameObject>
	{
		/// <summary>
		/// true이면 오브젝트가 풀로 디스폰될 때 샘플 엔티티의 부모 트랜스폼과 동일한 부모로 설정됩니다
		/// </summary>
		public bool DespawnToSampleParent { get; set; } = true;

		public GameObjectPool(uint initSize, GameObject sampleEntity) : base(initSize, sampleEntity, Instantiator)
		{
		}

		public GameObjectPool(uint initSize, GameObject sampleEntity, Func<GameObject, GameObject> instantiator) : base(initSize, sampleEntity, instantiator)
		{
		}

		/// <inheritdoc />
		public override void Dispose(bool disposeSampleEntity)
		{
			Object.Destroy(SampleEntity);

			base.Dispose(disposeSampleEntity);
		}

		/// <inheritdoc />
		public override void Dispose()
		{
			var content = Clear();

			foreach (var obj in content)
			{
				Object.Destroy(obj);
			}
		}

		/// <summary>
		/// <see cref="GameObject"/> 풀을 위한 범용 인스턴시에이터
		/// </summary>
		public static GameObject Instantiator(GameObject entityRef)
		{
			var instance = Object.Instantiate(entityRef, entityRef.transform.parent, true);

			instance.SetActive(false);

			return instance;
		}

		protected override GameObject SpawnEntity()
		{
			var entity = base.SpawnEntity();

			entity.SetActive(true);

			return entity;
		}

		/// <inheritdoc />
		protected override void CallOnSpawned(GameObject entity)
		{
			var poolEntity = entity.GetComponent<IPoolEntitySpawn>();

			poolEntity?.OnSpawn();
		}

		/// <inheritdoc />
		protected override void CallOnSpawned<TData>(GameObject entity, TData data)
		{
			var poolEntity = entity.GetComponent<IPoolEntitySpawn<TData>>();

			poolEntity?.OnSpawn(data);
		}

		/// <inheritdoc />
		protected override void CallOnDespawned(GameObject entity)
		{
			var poolEntity = entity.GetComponent<IPoolEntityDespawn>();

			poolEntity?.OnDespawn();
		}

		protected override void PostDespawnEntity(GameObject entity)
		{
			entity.SetActive(false);

			if (DespawnToSampleParent && SampleEntity != null)
			{
				entity.transform.SetParent(SampleEntity.transform.parent);
			}
		}
	}

	/// <inheritdoc />
	/// <remarks>
	/// 컴포넌트 참조로 오브젝트 참조를 사용하여 새 <see cref="GameObject"/>를 생성하는 풀에 유용합니다
	/// </remarks>
	public class GameObjectPool<T> : ObjectPoolBase<T> where T : Behaviour
	{
		/// <summary>
		/// true이면 오브젝트가 풀로 디스폰될 때 샘플 엔티티의 부모 트랜스폼과 동일한 부모로 설정됩니다
		/// </summary>
		public bool DespawnToSampleParent { get; set; } = true;

		public GameObjectPool(uint initSize, T sampleEntity) : base(initSize, sampleEntity, Instantiator)
		{
		}

		public GameObjectPool(uint initSize, T sampleEntity, Func<T, T> instantiator) : base(initSize, sampleEntity, instantiator)
		{
		}

		/// <inheritdoc />
		public override void Dispose(bool disposeSampleEntity)
		{
			Object.Destroy(SampleEntity.gameObject);

			base.Dispose(disposeSampleEntity);
		}

		/// <inheritdoc />
		public override void Dispose()
		{
			var content = Clear();

			foreach (var obj in content)
			{
				Object.Destroy(obj.gameObject);
			}
		}

		/// <summary>
		/// <see cref="GameObject"/> 풀을 위한 범용 인스턴시에이터
		/// </summary>
		public static T Instantiator(T entityRef)
		{
			// ReSharper disable once MergeConditionalExpression
			var parent = entityRef == null ? null : entityRef.transform.parent;
			var instance = Object.Instantiate(entityRef, parent, true);

			instance.gameObject.SetActive(false);

			return instance;
		}

		protected override T SpawnEntity()
		{
			T entity = null;

			while(entity == null)
			{
				entity = base.SpawnEntity();

				if(entity.gameObject == null)
				{
					SpawnedEntities.Remove(entity);

					entity = null;
				}
			}

			entity.gameObject.SetActive(true);

			return entity;
		}

		/// <inheritdoc />
		protected override void CallOnSpawned(T entity)
		{
			var poolEntity = entity.GetComponent<IPoolEntitySpawn>();

			poolEntity?.OnSpawn();
		}

		/// <inheritdoc />
		protected override void CallOnSpawned<TData>(T entity, TData data)
		{
			var poolEntity = entity.GetComponent<IPoolEntitySpawn<TData>>();

			poolEntity?.OnSpawn(data);
		}

		/// <inheritdoc />
		protected override void CallOnDespawned(T entity)
		{
			var poolEntity = entity.GetComponent<IPoolEntityDespawn>();

			poolEntity?.OnDespawn();
		}

		protected override void PostDespawnEntity(T entity)
		{
			entity.gameObject.SetActive(false);

			if (DespawnToSampleParent && SampleEntity is not null && !SampleEntity.Equals(null))
			{
				entity.transform.SetParent(SampleEntity.transform.parent);
			}
		}
	}
}