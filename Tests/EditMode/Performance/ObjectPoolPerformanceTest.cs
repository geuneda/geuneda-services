using Geuneda.Services;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	/// <summary>
	/// ObjectPool 성능 테스트입니다.
	/// 테스트 실행 전 성능 테스트 메타데이터가 초기화되도록 PrebuildSetup을 사용합니다.
	/// </summary>
	[TestFixture]
	[Category("Performance")]
	[PrebuildSetup(typeof(PerformanceTestSetup))]
	public class ObjectPoolPerformanceTest
	{
		public class MockEntity
		{
		}

		[Test, Performance]
		public void ObjectPool_SpawnDespawn_1000Cycles()
		{
			var pool = new ObjectPool<MockEntity>(1000, () => new MockEntity());
			var entities = new MockEntity[1000];

			Measure.Method(() =>
				{
					for (var i = 0; i < 1000; i++) entities[i] = pool.Spawn();
					for (var i = 0; i < 1000; i++) pool.Despawn(entities[i]);
				})
				.WarmupCount(5)
				.MeasurementCount(20)
				.Run();
		}
	}
}
