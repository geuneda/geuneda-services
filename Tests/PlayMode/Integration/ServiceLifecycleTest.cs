using System.Collections;
using Geuneda.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	public class ServiceLifecycleTest
	{
		public struct TestMessage : IMessage {}

		[TearDown]
		public void Cleanup()
		{
			MainInstaller.Clean();
		}

		[UnityTest]
		public IEnumerator TickService_WithMessageBroker_PublishesOnTick()
		{
			var tickService = new TickService();
			var broker = new MessageBrokerService();
			var messageReceived = false;

			broker.Subscribe<TestMessage>(m => messageReceived = true);
			tickService.SubscribeOnUpdate(dt => broker.Publish(new TestMessage()));

			yield return null;
			yield return null;

			Assert.IsTrue(messageReceived);
			
			tickService.Dispose();
		}

		[UnityTest]
		public IEnumerator PoolService_WithGameObjectPool_FullLifecycle()
		{
			var poolService = new PoolService();
			var sample = new GameObject("Sample");
			var pool = new GameObjectPool(0, sample);
			
			poolService.AddPool(pool);
			
			var instance = poolService.Spawn<GameObject>();
			Assert.IsNotNull(instance);
			Assert.IsTrue(instance.activeSelf);
			
			poolService.Despawn(instance);
			Assert.IsFalse(instance.activeSelf);
			
			Object.Destroy(sample);
			pool.Dispose();
			yield return null;
		}

		[Test]
		public void MainInstaller_BindServices_ResolveAll_Successfully()
		{
			MainInstaller.Bind<ITickService>(new TickService());
			MainInstaller.Bind<IMessageBrokerService>(new MessageBrokerService());
			MainInstaller.Bind<IPoolService>(new PoolService());
			
			Assert.IsNotNull(MainInstaller.Resolve<ITickService>());
			Assert.IsNotNull(MainInstaller.Resolve<IMessageBrokerService>());
			Assert.IsNotNull(MainInstaller.Resolve<IPoolService>());
			
			MainInstaller.CleanDispose<ITickService>();
			MainInstaller.Clean<IMessageBrokerService>();
			MainInstaller.Clean<IPoolService>();
		}
	}
}
