using System.Collections;
using Geuneda.Services;
using Geuneda.Services.Pooling;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	public class GameObjectPoolTest
	{
		public class MockPoolEntity : MonoBehaviour, IPoolEntitySpawn, IPoolEntityDespawn
		{
			public int SpawnCount;
			public int DespawnCount;

			public void OnSpawn() => SpawnCount++;
			public void OnDespawn() => DespawnCount++;
		}

		public class MockPoolEntityWithData : MonoBehaviour, IPoolEntitySpawn, IPoolEntityDespawn, IPoolEntitySpawn<int>
		{
			public int SpawnCount;
			public int LastSpawnData;

			public void OnSpawn() => SpawnCount++;
			public void OnDespawn() {}
			public void OnSpawn(int data) => LastSpawnData = data;
		}

		private GameObject _sample;
		private GameObjectPool _pool;

		[SetUp]
		public void Init()
		{
			_sample = new GameObject("Sample");
			_sample.AddComponent<MockPoolEntity>();
			_sample.SetActive(false);
			_pool = new GameObjectPool(0, _sample);
		}

		[TearDown]
		public void Cleanup()
		{
			_pool.Dispose(true);
			if (_sample != null) Object.Destroy(_sample);
		}

		[UnityTest]
		public IEnumerator Spawn_InstantiatesPrefab()
		{
			var instance = _pool.Spawn();
			
			Assert.IsNotNull(instance);
			Assert.AreNotSame(_sample, instance);
			Assert.IsTrue(instance.activeSelf);
			
			yield return null;
		}

		[UnityTest]
		public IEnumerator Despawn_DeactivatesGameObject()
		{
			var instance = _pool.Spawn();
			_pool.Despawn(instance);
			
			Assert.IsFalse(instance.activeSelf);
			
			yield return null;
		}

		[UnityTest]
		public IEnumerator Spawn_InvokesIPoolEntitySpawn()
		{
			var instance = _pool.Spawn();
			var mock = instance.GetComponent<MockPoolEntity>();
			
			Assert.AreEqual(1, mock.SpawnCount);
			
			_pool.Despawn(instance);
			Assert.AreEqual(1, mock.DespawnCount);
			
			yield return null;
		}

		[UnityTest]
		public IEnumerator Dispose_DestroysAllInstances()
		{
			var instance = _pool.Spawn();
			_pool.Dispose();
			
			// 참고: 오브젝트 파괴는 프레임 끝 또는 다음 프레임까지 지연됩니다
			yield return null;
			
			Assert.IsTrue(instance == null);
		}

		[UnityTest]
		public IEnumerator Dispose_WithSampleDestroy_DestroysSample()
		{
			_pool.Dispose(true);
			
			yield return null;
			
			Assert.IsTrue(_sample == null);
		}

		[UnityTest]
		public IEnumerator Dispose_WithoutSampleDestroy_KeepsSample()
		{
			var instance = _pool.Spawn();

			_pool.Dispose(false);

			yield return null;

			Assert.IsTrue(instance == null);
			Assert.IsTrue(_sample != null);
		}

		[UnityTest]
		public IEnumerator SpawnWithData_InvokesIPoolEntitySpawn()
		{
			var sampleWithData = new GameObject("SampleWithData");
			sampleWithData.AddComponent<MockPoolEntityWithData>();
			sampleWithData.SetActive(false);
			var poolWithData = new GameObjectPool(0, sampleWithData);

			var instance = poolWithData.Spawn(42);
			var mock = instance.GetComponent<MockPoolEntityWithData>();

			Assert.AreEqual(42, mock.LastSpawnData);

			poolWithData.Dispose(true);

			yield return null;
		}

		[UnityTest]
		public IEnumerator Dispose_AfterDespawnedInstanceDestroyedExternally_DoesNotThrow()
		{
			var externalParent = new GameObject("ExternalParent");
			_sample.transform.SetParent(externalParent.transform);

			var instance = _pool.Spawn();
			_pool.Despawn(instance);

			// PostDespawnEntity 가 `instance` 를 `externalParent` 아래로 옮겼으므로, 이를 파괴하면
			// 풀이 아직 `instance` 를 추적하는 상태에서 두 자식이 함께 파괴된다.
			Object.Destroy(externalParent);
			yield return null;

			Assert.DoesNotThrow(() => _pool.Dispose());
		}
	}
}
