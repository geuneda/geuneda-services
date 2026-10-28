using System;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable CheckNamespace

namespace Geuneda.Services.Pooling
{
	/// <inheritdoc />
	/// <remarks>
	/// 객체 참조로 새 <see cref="GameObject"/> 를 만드는 풀에 유용합니다
	/// </remarks>
	public class GameObjectPool : ObjectPoolBase<GameObject>
	{
		/// <summary>
		/// true 이면 객체가 디스폰되어 풀로 돌아올 때 샘플 엔티티와 같은 부모 트랜스폼 아래로
		/// 옮겨집니다
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
			if (disposeSampleEntity && SampleEntity != null)
			{
				Object.Destroy(SampleEntity);
			}

			base.Dispose(disposeSampleEntity);
		}

		/// <inheritdoc />
		public override void Dispose()
		{
			var content = Clear();

			foreach (var obj in content)
			{
				// 외부 소유자가 이미 파괴한 항목은 건너뜁니다(Unity fake-null).
				if (obj == null)
				{
					continue;
				}

				Object.Destroy(obj);
			}
		}

		/// <summary>
		/// <see cref="GameObject"/> 풀용 범용 인스턴시에이터
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
			// 없는 컴포넌트를 GetComponent 로 찾으면 에디터가 오류 문자열을 할당한다. Spawn(data) 는 비제네릭 훅도 함께 부르므로 매번 실패할 수 있다.
			if (entity.TryGetComponent<IPoolEntitySpawn>(out var poolEntity))
			{
				poolEntity.OnSpawn();
			}
		}

		/// <inheritdoc />
		protected override void CallOnSpawned<TData>(GameObject entity, TData data)
		{
			if (entity.TryGetComponent<IPoolEntitySpawn<TData>>(out var poolEntity))
			{
				poolEntity.OnSpawn(data);
			}
		}

		/// <inheritdoc />
		protected override void CallOnDespawned(GameObject entity)
		{
			if (entity.TryGetComponent<IPoolEntityDespawn>(out var poolEntity))
			{
				poolEntity.OnDespawn();
			}
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
	/// 컴포넌트 참조로 새 <see cref="GameObject"/> 를 만드는 풀에 유용합니다
	/// </remarks>
	public class GameObjectPool<T> : ObjectPoolBase<T> where T : Behaviour
	{
		/// <summary>
		/// true 이면 객체가 디스폰되어 풀로 돌아올 때 샘플 엔티티와 같은 부모 트랜스폼 아래로
		/// 옮겨집니다
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
			if (disposeSampleEntity && SampleEntity != null)
			{
				Object.Destroy(SampleEntity.gameObject);
			}

			base.Dispose(disposeSampleEntity);
		}

		/// <inheritdoc />
		public override void Dispose()
		{
			var content = Clear();

			foreach (var obj in content)
			{
				// 외부 소유자가 이미 파괴한 항목은 건너뜁니다. 파괴된 Behaviour 의
				// .gameObject 는 MissingReferenceException 을 던집니다.
				if (obj == null)
				{
					continue;
				}

				Object.Destroy(obj.gameObject);
			}
		}

		/// <summary>
		/// <see cref="GameObject"/> 풀용 범용 인스턴시에이터
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
			// 없는 컴포넌트를 GetComponent 로 찾으면 에디터가 오류 문자열을 할당한다. Spawn(data) 는 비제네릭 훅도 함께 부르므로 매번 실패할 수 있다.
			if (entity.TryGetComponent<IPoolEntitySpawn>(out var poolEntity))
			{
				poolEntity.OnSpawn();
			}
		}

		/// <inheritdoc />
		protected override void CallOnSpawned<TData>(T entity, TData data)
		{
			if (entity.TryGetComponent<IPoolEntitySpawn<TData>>(out var poolEntity))
			{
				poolEntity.OnSpawn(data);
			}
		}

		/// <inheritdoc />
		protected override void CallOnDespawned(T entity)
		{
			if (entity.TryGetComponent<IPoolEntityDespawn>(out var poolEntity))
			{
				poolEntity.OnDespawn();
			}
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
