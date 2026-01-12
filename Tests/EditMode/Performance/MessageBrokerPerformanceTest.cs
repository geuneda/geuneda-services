using Geuneda.Services;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	/// <summary>
	/// Performance tests for MessageBrokerService.
	/// Uses PrebuildSetup to ensure performance test metadata is initialized before tests run.
	/// </summary>
	[TestFixture]
	[Category("Performance")]
	[PrebuildSetup(typeof(PerformanceTestSetup))]
	public class MessageBrokerPerformanceTest
	{
		public struct TestMessage : IMessage {}

		[Test, Performance]
		public void Publish_100Subscribers_MeasureTime()
		{
			var broker = new MessageBrokerService();
			for (var i = 0; i < 100; i++)
			{
				var sub = new MockSubscriber();
				broker.Subscribe<TestMessage>(sub.OnMessage);
			}

			Measure.Method(() => broker.Publish(new TestMessage()))
				.WarmupCount(10)
				.MeasurementCount(100)
				.Run();
		}

		[Test, Performance]
		public void PublishSafe_MeasureAllocations()
		{
			var broker = new MessageBrokerService();
			for (var i = 0; i < 100; i++)
			{
				var sub = new MockSubscriber();
				broker.Subscribe<TestMessage>(sub.OnMessage);
			}

			Measure.Method(() => broker.PublishSafe(new TestMessage()))
				.GC()
				.WarmupCount(10)
				.MeasurementCount(100)
				.Run();
		}

		private class MockSubscriber
		{
			public void OnMessage(TestMessage m) {}
		}
	}
}
