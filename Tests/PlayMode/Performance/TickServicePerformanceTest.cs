using System.Collections;
using GameLovers.Services;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	public class TickServicePerformanceTest
	{
		[UnityTest, Performance]
		public IEnumerator Update_1000Subscribers_MeasureFrameTime()
		{
			var tickService = new TickService();
			for (var i = 0; i < 1000; i++)
			{
				tickService.SubscribeOnUpdate(dt => {});
			}

			yield return Measure.Frames()
				.WarmupCount(10)
				.MeasurementCount(100)
				.Run();
			
			tickService.Dispose();
		}

		[Test, Performance]
		public void Subscribe_Unsubscribe_Churn_MeasureTime()
		{
			var tickService = new TickService();
			System.Action<float>[] actions = new System.Action<float>[100];
			for (int i = 0; i < 100; i++) actions[i] = dt => {};

			Measure.Method(() =>
				{
					for (var i = 0; i < 100; i++) tickService.SubscribeOnUpdate(actions[i]);
					for (var i = 0; i < 100; i++) tickService.UnsubscribeOnUpdate(actions[i]);
				})
				.WarmupCount(5)
				.MeasurementCount(20)
				.Run();
			
			tickService.Dispose();
		}
	}
}
