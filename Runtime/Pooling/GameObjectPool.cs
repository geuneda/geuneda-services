using System;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable CheckNamespace

namespace Geuneda.Services.Pooling
{
	/// <inheritdoc />
	/// <remarks>
	/// Useful for pools that use object references to create new <see cref="GameObject"/>
	/// </remarks>
	public class GameObjectPool : ObjectPoolBase<GameObject>
	{
		/// <summary>
		/// If true then when the object is despawned back to the pool will be parented to the same as the sample entity
		/// parent transform
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
				// Skip entries already destroyed by an external owner (Unity fake-null).
				if (obj == null)
				{
					continue;
				}

				Object.Destroy(obj);
			}
		}

		/// <summary>
		/// Generic instantiator for <see cref="GameObject"/> pools
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
	/// Useful for pools that use object references to create new <see cref="GameObject"/> by their component reference
	/// </remarks>
	public class GameObjectPool<T> : ObjectPoolBase<T> where T : Behaviour
	{
		/// <summary>
		/// If true then when the object is despawned back to the pool will be parented to the same as the sample entity
		/// parent transform
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
				// Skip entries already destroyed by an external owner; .gameObject
				// on a destroyed Behaviour throws MissingReferenceException.
				if (obj == null)
				{
					continue;
				}

				Object.Destroy(obj.gameObject);
			}
		}

		/// <summary>
		/// Generic instantiator for <see cref="GameObject"/> pools
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
