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
			// 회귀 테스트: Despawn_WithCondition_AllMatching_DespawnsAll 은 같은 _mockEntity 를
			// 두 번 스폰하므로(SetUp 팩토리가 단일 인스턴스를 반환한다) SpawnedEntities.Remove 가
			// 중복 항목을 참조 동등성으로 매칭한다. 이 테스트는 *서로 다른* 엔티티를 사용해,
			// ObjectPoolBase<T>.Despawn(bool, Func) 의 순회 중 변경 수정이 일치하는 각 요소가
			// 별개의 참조일 때도 유지되는지 확인한다.
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
			// 스폰된 집합의 일부만 조건에 맞을 때, 디스폰 성공 후 순회 인덱스를 되돌리는 처리가
			// 조건에 맞지 않는 이웃 항목을 잘못 제거하지 않는지 확인한다.
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

		/// <summary>
		/// 파괴된 Unity 객체처럼 <c>Equals(null)</c> 이 true 인 엔티티. Init 호출 수를 셉니다
		/// </summary>
		public class NullEquivalentEntity : IPoolEntityObject<NullEquivalentEntity>
		{
			public int InitCount;

			public void Init(IObjectPool<NullEquivalentEntity> pool) => InitCount++;
			public bool Despawn() => false;
			public override bool Equals(object obj) => obj == null || ReferenceEquals(this, obj);
			public override int GetHashCode() => 0;
		}

		[Test]
		public void Spawn_InstantiatorReturnsNullEquivalent_ThrowsWithoutRetry()
		{
			var calls = 0;
			var created = new NullEquivalentEntity();
			var pool = new ObjectPool<NullEquivalentEntity>(0, null, _ =>
			{
				// 재시도하면 이 예외가 나므로 회귀가 생겨도 테스트가 멈추지 않고 실패합니다
				if (++calls > 1) throw new AssertionException("Instantiator was called again");
				return created;
			});

			Assert.Throws<InvalidOperationException>(() => pool.Spawn());
			Assert.AreEqual(1, calls);
			Assert.AreEqual(0, created.InitCount);
			Assert.AreEqual(0, pool.SpawnedReadOnly.Count);
		}

		[Test]
		public void Spawn_InstantiatorReturnsNull_ThrowsWithoutRetry()
		{
			var calls = 0;
			var pool = new ObjectPool<object>(0, null, _ =>
			{
				if (++calls > 1) throw new AssertionException("Instantiator was called again");
				return null;
			});

			Assert.Throws<InvalidOperationException>(() => pool.Spawn());
			Assert.AreEqual(1, calls);
			Assert.AreEqual(0, pool.SpawnedReadOnly.Count);
		}
	}
}