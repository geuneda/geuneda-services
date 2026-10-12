using System.Collections;
using Geuneda.Services;
using Geuneda.Services.Pooling;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	public class GameObjectPoolTypedTest
	{
		public class MockBehaviour : MonoBehaviour, IPoolEntitySpawn, IPoolEntityDespawn, IPoolEntitySpawn<int>
		{
			public int SpawnCount;
			public int DespawnCount;
			public int LastSpawnData;

			public void OnSpawn() => SpawnCount++;
			public void OnDespawn() => DespawnCount++;
			public void OnSpawn(int data) => LastSpawnData = data;
		}

		private GameObject _sampleGo;
		private MockBehaviour _sampleBehaviour;
		private GameObjectPool<MockBehaviour> _pool;

		[SetUp]
		public void Init()
		{
			_sampleGo = new GameObject("SampleTyped");
			_sampleBehaviour = _sampleGo.AddComponent<MockBehaviour>();
			_sampleGo.SetActive(false);
			_pool = new GameObjectPool<MockBehaviour>(0, _sampleBehaviour);
		}

		[TearDown]
		public void Cleanup()
		{
			_pool.Dispose();
			if (_sampleGo != null) Object.Destroy(_sampleGo);
		}

		[UnityTest]
		public IEnumerator Spawn_ReturnsComponentReference()
		{
			var instance = _pool.Spawn();

			Assert.IsNotNull(instance);
			Assert.IsInstanceOf<MockBehaviour>(instance);
			Assert.AreNotSame(_sampleBehaviour, instance);
			Assert.IsTrue(instance.gameObject.activeSelf);

			yield return null;
		}

		[UnityTest]
		public IEnumerator Despawn_DeactivatesGameObject()
		{
			var instance = _pool.Spawn();
			_pool.Despawn(instance);

			Assert.IsFalse(instance.gameObject.activeSelf);

			yield return null;
		}

		[UnityTest]
		public IEnumerator LifecycleHooks_InvokedOnSpawnAndDespawn()
		{
			var instance = _pool.Spawn();

			Assert.AreEqual(1, instance.SpawnCount);
			Assert.AreEqual(0, instance.DespawnCount);

			_pool.Despawn(instance);

			Assert.AreEqual(1, instance.DespawnCount);

			yield return null;
		}

		[UnityTest]
		public IEnumerator SpawnWithData_InvokesTypedSpawnHook()
		{
			var instance = _pool.Spawn(42);

			Assert.AreEqual(42, instance.LastSpawnData);
			Assert.AreEqual(1, instance.SpawnCount);

			yield return null;
		}

		[UnityTest]
		public IEnumerator Dispose_DestroysAllSpawnedInstances()
		{
			var instance1 = _pool.Spawn();
			var instance2 = _pool.Spawn();

			_pool.Dispose();

			yield return null;

			Assert.IsTrue(instance1 == null);
			Assert.IsTrue(instance2 == null);
		}

		[UnityTest]
		public IEnumerator DespawnAll_DeactivatesAllSpawnedInstances()
		{
			var instance1 = _pool.Spawn();
			var instance2 = _pool.Spawn();

			_pool.DespawnAll();

			Assert.IsFalse(instance1.gameObject.activeSelf);
			Assert.IsFalse(instance2.gameObject.activeSelf);
			Assert.AreEqual(0, _pool.SpawnedReadOnly.Count);

			yield return null;
		}

		[UnityTest]
		public IEnumerator SampleEntity_ReturnsSampleReference()
		{
			Assert.AreSame(_sampleBehaviour, _pool.SampleEntity);

			yield return null;
		}

		[UnityTest]
		public IEnumerator SpawnedReadOnly_ReflectsSpawnedEntities()
		{
			Assert.AreEqual(0, _pool.SpawnedReadOnly.Count);

			var instance = _pool.Spawn();

			Assert.AreEqual(1, _pool.SpawnedReadOnly.Count);
			Assert.AreSame(instance, _pool.SpawnedReadOnly[0]);

			yield return null;
		}

		[UnityTest]
		public IEnumerator IsSpawned_ReturnsTrueWhenMatch()
		{
			var instance = _pool.Spawn();

			Assert.IsTrue(_pool.IsSpawned(e => e == instance));
			Assert.IsFalse(_pool.IsSpawned(e => false));

			yield return null;
		}

		[UnityTest]
		public IEnumerator Despawn_WithCondition_FirstOnly_Successfully()
		{
			var instance1 = _pool.Spawn();
			var instance2 = _pool.Spawn();

			Assert.IsTrue(_pool.Despawn(onlyFirst: true, e => e == instance1));
			Assert.AreEqual(1, _pool.SpawnedReadOnly.Count);
			Assert.IsFalse(instance1.gameObject.activeSelf);
			Assert.IsTrue(instance2.gameObject.activeSelf);

			yield return null;
		}

		[UnityTest]
		public IEnumerator Despawn_WithCondition_AllMatching_DespawnsAll()
		{
			_pool.Spawn();
			_pool.Spawn();

			Assert.IsTrue(_pool.Despawn(onlyFirst: false, e => true));
			Assert.AreEqual(0, _pool.SpawnedReadOnly.Count);

			yield return null;
		}

		[UnityTest]
		public IEnumerator Reset_ClearsAndReinitializesPool()
		{
			_pool.Spawn();

			var newSampleGo = new GameObject("NewSampleTyped");
			var newSample = newSampleGo.AddComponent<MockBehaviour>();
			newSampleGo.SetActive(false);

			_pool.Reset(2, newSample);

			Assert.AreEqual(0, _pool.SpawnedReadOnly.Count);
			Assert.AreSame(newSample, _pool.SampleEntity);

			Object.Destroy(newSampleGo);
			yield return null;
		}

		[UnityTest]
		public IEnumerator DespawnToSampleParent_ReparentsOnDespawn()
		{
			var parent = new GameObject("Parent");
			_sampleGo.transform.SetParent(parent.transform);

			var instance = _pool.Spawn();
			instance.transform.SetParent(null);

			_pool.Despawn(instance);

			Assert.AreSame(parent.transform, instance.transform.parent);

			// 부모를 파괴하기 전에 분리해서, 연쇄 파괴로 (풀이 아직 추적 중인) 풀링된 인스턴스까지
			// 파괴되지 않게 한다. 그런 상황에서 Dispose 가 견디는지는
			// Dispose_AfterDespawnedInstanceDestroyedExternally_DoesNotThrow 에서 검증한다.
			_sampleGo.transform.SetParent(null);
			instance.transform.SetParent(null);

			Object.Destroy(parent);
			yield return null;
		}

		[UnityTest]
		public IEnumerator Dispose_AfterDespawnedInstanceDestroyedExternally_DoesNotThrow()
		{
			var externalParent = new GameObject("ExternalParent");
			_sampleGo.transform.SetParent(externalParent.transform);

			var instance = _pool.Spawn();
			_pool.Despawn(instance);

			// PostDespawnEntity 가 `instance` 를 `externalParent` 아래로 옮겼으므로, 이를 파괴하면
			// 풀이 아직 `instance` 를 추적하는 상태에서 두 자식이 함께 파괴된다.
			Object.Destroy(externalParent);
			yield return null;

			Assert.DoesNotThrow(() => _pool.Dispose());
		}

		[UnityTest]
		public IEnumerator Spawn_AfterDespawnedInstanceDestroyedExternally_ReturnsLiveInstance()
		{
			var instance = _pool.Spawn();
			_pool.Despawn(instance);

			// 풀 스택에 남은 컴포넌트의 GameObject 를 풀 밖에서 파괴한다(Unity fake null 이 된다).
			Object.Destroy(instance.gameObject);
			yield return null;

			MockBehaviour spawned = null;
			Assert.DoesNotThrow(() => spawned = _pool.Spawn());
			Assert.IsTrue(spawned != null);
			Assert.IsTrue(spawned.gameObject.activeSelf);
			Assert.AreEqual(1, _pool.SpawnedReadOnly.Count);
		}

		[UnityTest]
		public IEnumerator DisposeWithSampleDestroy_DestroysSampleGameObject()
		{
			_pool.Dispose(disposeSampleEntity: true);

			yield return null;

			Assert.IsTrue(_sampleGo == null);
		}

		[UnityTest]
		public IEnumerator DisposeWithoutSampleDestroy_KeepsSampleGameObject()
		{
			var instance = _pool.Spawn();

			_pool.Dispose(disposeSampleEntity: false);

			yield return null;

			Assert.IsTrue(instance == null);
			Assert.IsTrue(_sampleGo != null);
		}
	}
}
