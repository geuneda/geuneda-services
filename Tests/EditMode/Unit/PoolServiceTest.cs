using System;
using Geuneda.Services;
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

		[SetUp]
		public void Init()
		{
			_poolService = new PoolService();
			_pool = new ObjectPool<IMockPoolableEntity>(0, () => new MockPoolableEntity());
			
			_poolService.AddPool(_pool);
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
	}
}
