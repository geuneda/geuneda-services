using System.Collections;
using GameLovers.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	public class ServicesBootstrapSmokeTest
	{
		[TearDown]
		public void Cleanup()
		{
			MainInstaller.Clean();
		}

		[Test]
		public void AllServices_Instantiate_WithoutException()
		{
			Assert.DoesNotThrow(() => new MessageBrokerService());
			Assert.DoesNotThrow(() => new PoolService());
			Assert.DoesNotThrow(() => new DataService());
			Assert.DoesNotThrow(() => new TimeService());
			Assert.DoesNotThrow(() => new RngService(RngService.CreateRngData(0)));
		}

		[Test]
		public void TickService_CreatesGameObject()
		{
			var service = new TickService();
			var go = GameObject.Find("TickServiceMonoBehaviour");
			Assert.IsNotNull(go);
			service.Dispose();
		}

		[Test]
		public void CoroutineService_CreatesGameObject()
		{
			var service = new CoroutineService();
			var go = GameObject.Find("CoroutineServiceMonoBehaviour");
			Assert.IsNotNull(go);
			service.Dispose();
		}

		[Test]
		public void MainInstaller_BindResolve_Works()
		{
			var broker = new MessageBrokerService();
			MainInstaller.Bind<IMessageBrokerService>(broker);
			Assert.AreSame(broker, MainInstaller.Resolve<IMessageBrokerService>());
		}

		[Test]
		public void MessageBroker_PublishWithoutSubscribers_Works()
		{
			var broker = new MessageBrokerService();
			Assert.DoesNotThrow(() => broker.Publish(new SmokeMessage()));
		}

		public struct SmokeMessage : IMessage {}
	}
}
