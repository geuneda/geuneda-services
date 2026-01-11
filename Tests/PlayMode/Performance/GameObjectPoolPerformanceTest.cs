using System.Collections;
using GameLovers.Services;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	public class GameObjectPoolPerformanceTest
	{
		[UnityTest, Performance]
		public IEnumerator GameObjectPool_SpawnDespawn_100Cycles()
		{
			var sample = new GameObject("Sample");
			var pool = new GameObjectPool(100, sample);
			var instances = new GameObject[100];

			Measure.Method(() =>
				{
					for (var i = 0; i < 100; i++) instances[i] = pool.Spawn();
					for (var i = 0; i < 100; i++) pool.Despawn(instances[i]);
				})
				.WarmupCount(5)
				.MeasurementCount(20)
				.Run();
			
			yield return null;
			
			pool.Dispose();
			Object.Destroy(sample);
		}
	}
}
