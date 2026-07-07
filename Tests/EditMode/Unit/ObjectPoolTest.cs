using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Geuneda.Services;
using Geuneda.Services.Pooling;
using NSubstitute;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	public class ObjectPoolTest
	{
		private ObjectPool<IMockEntity> _pool;
		private IMockEntity _mockEntity;
		private uint _initialSize = 5;

		public interface IMockEntity : IPoolEntitySpawn, IPoolEntityDespawn, IPoolEntityObject<IMockEntity>, IPoolEntitySpawn<object> { }
		public class MockEntity : IMockEntity
		{
			private IObjectPool<IMockEntity> _pool;

			public void Init(IObjectPool<IMockEntity> pool) => _pool = pool;

			public bool Despawn() => _pool.Despawn(this);
			public void OnDespawn()	{}

			public void OnSpawn() {}
			public void OnSpawn(object data) {}
		}

		[SetUp]
		public void Init()
		{
			_mockEntity = Substitute.For<IMockEntity>();
			_pool = new ObjectPool<IMockEntity>(_initialSize, () => _mockEntity);
		}

		[Test]
		public void Spawn_Successfully()
		{
			var newEntity = _pool.Spawn();
			
			newEntity.Received().OnSpawn();
			
			Assert.AreSame(_mockEntity, newEntity);
		}

		[Test]
		public void Spawn_WithData_Successfully()
		{
			var obj = new object();
			var newEntity = _pool.Spawn(obj);

			newEntity.Received().OnSpawn(obj);

			Assert.AreSame(_mockEntity, newEntity);
		}

		[Test]
		public void Spawn_ZeroInitialSize_Successfully()
		{
			var pool = new ObjectPool<IMockEntity>(0, () => _mockEntity);
			var newEntity = pool.Spawn();

			newEntity.Received().OnSpawn();

			Assert.AreSame(_mockEntity, newEntity);
		}

		[Test]
		public void Despawn_Successfully()
		{
			_pool.Spawn();

			Assert.IsTrue(_pool.Despawn(_mockEntity));
			_mockEntity.Received().OnDespawn();
		}

		[Test]
		public void EntityDespawn_Successfully()
		{
			// Substitute.For<IObjectPool<IMockEntity>>() 대신 실제 ObjectPool<IMockEntity>를 사용한다.
			// 제네릭 인자가 자기 참조 인터페이스(IMockEntity : IPoolEntityObject<IMockEntity>)일 때
			// NSubstitute + Castle DynamicProxy가 Unity Mono 런타임에서 프록시 생성 중 크래시하기 때문이다
			// — ILGenerator.DeclareLocal이 null localType을 받는다. 실제 풀은 동일한
			// MockEntity.Despawn -> pool.Despawn(this) 계약을 그대로 수행하며, SpawnedReadOnly.Count로
			// observable 상태를 통한 라우팅을 확인한다.
			MockEntity sharedEntity = null;
			var pool = new ObjectPool<IMockEntity>(1, () => sharedEntity ??= new MockEntity());
			var entity = pool.Spawn();

			Assert.AreSame(sharedEntity, entity);
			Assert.AreEqual(1, pool.SpawnedReadOnly.Count);
			Assert.IsTrue(sharedEntity.Despawn());
			Assert.AreEqual(0, pool.SpawnedReadOnly.Count);
		}

		[Test]
		public void Despawn_NotSpawnedObject_ReturnsFalse()
		{
			Assert.IsFalse(_pool.Despawn(_mockEntity));
			_mockEntity.DidNotReceive().OnDespawn();
		}

		[Test]
		public void DespawnAll_Successfully()
		{
			var newEntity1 = _pool.Spawn();
			var newEntity2 = _pool.Spawn();
			
			_pool.DespawnAll();

			newEntity1.Received().OnDespawn();
			newEntity2.Received().OnDespawn();
		}

		[Test]
		public void Clear_Successfully()
		{
			var clearedEntities = _pool.Clear();

			Assert.AreEqual(_initialSize, clearedEntities.Count);
		}

		[Test]
		public void SampleEntity_ReturnsSampleEntity()
		{
			Assert.AreSame(_mockEntity, _pool.SampleEntity);
		}

		[Test]
		public void SpawnedReadOnly_ReturnsSpawnedEntities()
		{
			var entity = _pool.Spawn();

			var spawned = _pool.SpawnedReadOnly;

			Assert.AreEqual(1, spawned.Count);
			Assert.AreSame(entity, spawned[0]);
		}

		[Test]
		public void IsSpawned_ReturnsTrueWhenMatch()
		{
			var entity = _pool.Spawn();

			Assert.IsTrue(_pool.IsSpawned(e => e == entity));
			Assert.IsFalse(_pool.IsSpawned(e => false));
		}

		[Test]
		public void Despawn_WithCondition_FirstOnly_Successfully()
		{
			var entity = _pool.Spawn();

			Assert.IsTrue(_pool.Despawn(onlyFirst: true, e => e == entity));
			Assert.AreEqual(0, _pool.SpawnedReadOnly.Count);
		}

		[Test]
		public void Despawn_WithCondition_NoMatch_ReturnsFalse()
		{
			_pool.Spawn();

			Assert.IsFalse(_pool.Despawn(onlyFirst: true, e => false));
			Assert.AreEqual(1, _pool.SpawnedReadOnly.Count);
		}

		[Test]
		public void Despawn_WithCondition_AllMatching_DespawnsAll()
		{
			_pool.Spawn();
			_pool.Spawn();

			Assert.IsTrue(_pool.Despawn(onlyFirst: false, e => true));
			Assert.AreEqual(0, _pool.SpawnedReadOnly.Count);
		}

		[Test]
		public void Despawn_WithCondition_DistinctMatchingEntities_AllDespawn()
		{
			// Regression: Despawn_WithCondition_AllMatching_DespawnsAll spawns the same _mockEntity
			// twice (the SetUp factory returns a single instance), so SpawnedEntities.Remove matches
			// by reference equality on duplicates. This test uses DISTINCT entities to confirm the
			// iterate-while-mutating fix in ObjectPoolBase<T>.Despawn(bool, Func) also holds when
			// each matching element is a separate reference.
			var pool = new ObjectPool<IMockEntity>(0, () => Substitute.For<IMockEntity>());
			var first = pool.Spawn();
			var second = pool.Spawn();

			Assert.AreNotSame(first, second);
			Assert.IsTrue(pool.Despawn(onlyFirst: false, e => true));
			Assert.AreEqual(0, pool.SpawnedReadOnly.Count);
		}

		[Test]
		public void Despawn_WithCondition_PartialMatch_NonMatchingSurvives()
		{
			// Confirms the iteration step-back after a successful despawn doesn't spuriously remove
			// non-matching neighbours when only a subset of the spawned set matches the predicate.
			var pool = new ObjectPool<IMockEntity>(0, () => Substitute.For<IMockEntity>());
			var target = pool.Spawn();
			var keeper = pool.Spawn();

			Assert.IsTrue(pool.Despawn(onlyFirst: false, e => e == target));
			Assert.AreEqual(1, pool.SpawnedReadOnly.Count);
			Assert.AreSame(keeper, pool.SpawnedReadOnly[0]);
		}

		[Test]
		public void Reset_ClearsAndReinitializes()
		{
			_pool.Spawn();
			var newSample = Substitute.For<IMockEntity>();
			uint newSize = 3;

			_pool.Reset(newSize, newSample);

			Assert.AreEqual(0, _pool.SpawnedReadOnly.Count);
			Assert.AreSame(newSample, _pool.SampleEntity);
		}

		[Test]
		public void ObjectPool_FuncOnlyCtor_UsesProvidedFactory()
		{
			var invocations = 0;
			IMockEntity Factory()
			{
				invocations++;
				return Substitute.For<IMockEntity>();
			}

			const uint initSize = 3;
			var pool = new ObjectPool<IMockEntity>(initSize, Factory);

			Assert.AreEqual((int)initSize + 1, invocations);

			pool.Spawn();
			pool.Spawn();
			pool.Spawn();

			Assert.AreEqual((int)initSize + 1, invocations);
			Assert.AreEqual(3, pool.SpawnedReadOnly.Count);
		}
	}
}