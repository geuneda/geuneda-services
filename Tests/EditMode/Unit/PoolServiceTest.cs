using System;
using System.Collections.Generic;
using Geuneda.Services;
using Geuneda.Services.Pooling;
using NSubstitute;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	[TestFixture]
	public class PoolServiceTest
	{
		private PoolService _poolService;
		private IObjectPool<IMockPoolableEntity> _pool;

		public interface IMockPoolableEntity : IPoolEntitySpawn, IPoolEntityDespawn { }
		public class MockPoolableEntity : IMockPoolableEntity
		{
			public void OnSpawn() {}
			public void OnDespawn() {}
		}

		public interface IMockDataEntity : IPoolEntitySpawn, IPoolEntityDespawn, IPoolEntitySpawn<int> { }
		public class MockDataEntity : IMockDataEntity
		{
			public int SpawnData;
			public void OnSpawn() {}
			public void OnDespawn() {}
			public void OnSpawn(int data) => SpawnData = data;
		}

		// Hand-written fake — NSubstitute can't proxy IObjectPool<T> with self-referential
		// generic args on Mono. See Tests/AGENTS.md §4.
		private class FakeObjectPool<T> : IObjectPool<T> where T : class
		{
			public int DisposeCount;

			public T SampleEntity => null;
			public IReadOnlyList<T> SpawnedReadOnly => System.Array.Empty<T>();

			public void Dispose() { DisposeCount++; }
			public void Dispose(bool disposeSampleEntity) { DisposeCount++; }

			public bool IsSpawned(System.Func<T, bool> conditionCheck) => false;
			public void Reset(uint initSize, T sampleEntity) { }
			public T Spawn() => null;
			public T Spawn<TData>(TData data) => null;
			public bool Despawn(bool onlyFirst, System.Func<T, bool> entityGetter) => false;
			public bool Despawn(T entity) => false;
			public List<T> Clear() => new List<T>();
			public void DespawnAll() { }
		}

		[SetUp]
		public void Init()
		{
			_poolService = new PoolService();
			_pool = new ObjectPool<IMockPoolableEntity>(0, () => new MockPoolableEntity());
			
			_poolService.AddPool(_pool);
		}

		[TearDown]
		public void Dispose()
		{
			_poolService.Dispose();
		}

		[Test]
		public void TryGetPool_Successfully()
		{
			Assert.True(_poolService.TryGetPool<IMockPoolableEntity>(out var pool));
			Assert.AreEqual(_pool, pool);
		}

		[Test]
		public void GetPool_Successfully()
		{
			Assert.AreEqual(_pool, _poolService.GetPool<IMockPoolableEntity>());
		}

		[Test]
		public void AddPool_Successfully()
		{
			Assert.True(_poolService.TryGetPool<IMockPoolableEntity>(out _));
		}

		[Test]
		public void AddPool_SameType_ThrowsException()
		{
			Assert.Throws<ArgumentException>(() => _poolService.AddPool(_pool));
		}

		[Test]
		public void Spawn_Successfully()
		{
			var entity = _poolService.Spawn<IMockPoolableEntity>();
			
			Assert.IsNotNull(entity);
			Assert.IsInstanceOf<MockPoolableEntity>(entity);
		}

		[Test]
		public void Spawn_NotAddedPool_ThrowsException()
		{
			_poolService = new PoolService();
			
			Assert.Throws<ArgumentException>(() => _poolService.Spawn<IMockPoolableEntity>());
		}

		[Test]
		public void Despawn_Successfully()
		{
			var entity = _poolService.Spawn<IMockPoolableEntity>();
			
			Assert.DoesNotThrow(() => _poolService.Despawn(entity));
		}

		[Test]
		public void Despawn_NotAddedPool_ThrowsException()
		{
			var entity = new MockPoolableEntity();
			
			_poolService = new PoolService();
			
			Assert.Throws<ArgumentException>(() => _poolService.Despawn(entity));
		}

		[Test]
		public void DespawnAll_Successfully()
		{
			_poolService.Spawn<IMockPoolableEntity>();
			_poolService.DespawnAll<IMockPoolableEntity>();
			
			Assert.DoesNotThrow(() => _poolService.DespawnAll<IMockPoolableEntity>());
		}

		[Test]
		public void RemovePool_Successfully()
		{
			_poolService.RemovePool<IMockPoolableEntity>();

			Assert.Throws<ArgumentException>(() => _poolService.GetPool<IMockPoolableEntity>());
		}

		[Test]
		public void RemovePool_NotAdded_DoesNothing()
		{
			_poolService = new PoolService();
			
			Assert.DoesNotThrow(() => _poolService.RemovePool<IMockPoolableEntity>());
		}

		[Test]
		public void SpawnWithData_Successfully()
		{
			var dataPool = new ObjectPool<IMockDataEntity>(0, () => new MockDataEntity());
			_poolService.AddPool(dataPool);

			var entity = _poolService.Spawn<IMockDataEntity, int>(42);

			Assert.IsNotNull(entity);
			Assert.AreEqual(42, ((MockDataEntity)entity).SpawnData);
		}

		[Test]
		public void Clear_ReturnsAllPools()
		{
			IDictionary<Type, IObjectPool> cleared = _poolService.Clear();

			Assert.AreEqual(1, cleared.Count);
			Assert.IsTrue(cleared.ContainsKey(typeof(IMockPoolableEntity)));
			Assert.IsFalse(_poolService.TryGetPool<IMockPoolableEntity>(out _));
		}

		[Test]
		public void Dispose_RemovesAndDisposesPool()
		{
			_poolService.Dispose<IMockPoolableEntity>(disposeSampleEntity: false);

			Assert.IsFalse(_poolService.TryGetPool<IMockPoolableEntity>(out _));
		}

		[Test]
		public void Dispose_DisposesAllRegisteredPools()
		{
			var fakeA = new FakeObjectPool<IMockPoolableEntity>();
			var fakeB = new FakeObjectPool<IMockDataEntity>();

			var service = new PoolService();
			service.AddPool<IMockPoolableEntity>(fakeA);
			service.AddPool<IMockDataEntity>(fakeB);

			service.Dispose();

			Assert.AreEqual(1, fakeA.DisposeCount);
			Assert.AreEqual(1, fakeB.DisposeCount);
			Assert.IsFalse(service.TryGetPool<IMockPoolableEntity>(out _));
			Assert.IsFalse(service.TryGetPool<IMockDataEntity>(out _));
		}
	}
}
