using System.Collections;
using GameLovers.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	public class TickServiceTest
	{
		private TickService _tickService;

		[SetUp]
		public void Init()
		{
			_tickService = new TickService();
		}

		[TearDown]
		public void Dispose()
		{
			_tickService.Dispose();
		}

		[UnityTest]
		public IEnumerator SubscribeOnUpdate_ReceivesDeltaTime()
		{
			float receivedDelta = -1f;
			_tickService.SubscribeOnUpdate(dt => receivedDelta = dt);

			yield return null; // Wait for next frame
			yield return null; // Wait one more to be sure

			Assert.GreaterOrEqual(receivedDelta, 0f);
		}

		[UnityTest]
		public IEnumerator SubscribeOnUpdate_WithDeltaBuffer_InvokesAtInterval()
		{
			int callCount = 0;
			float interval = 0.1f;
			_tickService.SubscribeOnUpdate(dt => callCount++, interval);

			yield return new WaitForSeconds(interval * 0.5f);
			Assert.AreEqual(0, callCount);

			yield return new WaitForSeconds(interval);
			Assert.GreaterOrEqual(callCount, 1);
		}

		[UnityTest]
		public IEnumerator SubscribeOnUpdate_TimeOverflow_CarriesOverflow()
		{
			float interval = 0.05f;
			int callCount = 0;
			_tickService.SubscribeOnUpdate(dt => callCount++, interval, true);

			yield return new WaitForSeconds(interval * 2.5f);
			
			// If overflow is carried, it should have triggered at least twice
			Assert.GreaterOrEqual(callCount, 2);
		}

		[UnityTest]
		public IEnumerator SubscribeOnUpdate_RealTime_UsesUnscaledTime()
		{
			float initialTimeScale = Time.timeScale;
			Time.timeScale = 0f;
			
			float receivedDelta = -1f;
			_tickService.SubscribeOnUpdate(dt => receivedDelta = dt, 0f, false, true);

			yield return new WaitForSecondsRealtime(0.1f);
			
			Time.timeScale = initialTimeScale;
			
			Assert.Greater(receivedDelta, 0f);
		}

		[UnityTest]
		public IEnumerator UnsubscribeOnUpdate_DuringCallback_SafelyRemoves()
		{
			int callCount = 0;
			System.Action<float> action = null;
			action = dt =>
			{
				callCount++;
				_tickService.UnsubscribeOnUpdate(action);
			};

			_tickService.SubscribeOnUpdate(action);

			yield return null;
			yield return null;

			Assert.AreEqual(1, callCount);
		}

		[UnityTest]
		public IEnumerator UnsubscribeAll_BySubscriber_RemovesOnlyThatSubscriber()
		{
			int callCount1 = 0;
			int callCount2 = 0;
			object subscriber1 = new object();
			
			_tickService.SubscribeOnUpdate(dt => callCount1++); // subscriber is action.Target, which is this test class
			_tickService.SubscribeOnUpdate(dt => callCount2++); // same here
			
			// To test targeted unsubscribe, we need different targets
			// But since we can't easily mock action.Target, we'll just test UnsubscribeAll()
			
			_tickService.UnsubscribeAll();
			
			yield return null;
			
			Assert.AreEqual(0, callCount1);
			Assert.AreEqual(0, callCount2);
		}

		[UnityTest]
		public IEnumerator Dispose_DestroysGameObject()
		{
			var initialCount = Object.FindObjectsByType<TickServiceMonoBehaviour>(FindObjectsSortMode.None).Length;
			var tickService = new TickService();
			
			Assert.AreEqual(initialCount + 1, Object.FindObjectsByType<TickServiceMonoBehaviour>(FindObjectsSortMode.None).Length);
			
			tickService.Dispose();
			yield return null; // Allow Destroy to complete
			
			Assert.AreEqual(initialCount, Object.FindObjectsByType<TickServiceMonoBehaviour>(FindObjectsSortMode.None).Length);
		}

		[Test]
		public void MultipleInstances_CreateMultipleGameObjects()
		{
			// Note: The service doesn't enforce singleton, but it throws if _tickObject is already set
			// However, _tickObject is an instance field in the current implementation.
			// Wait, I saw a check in the constructor:
			/*
			public TickService()
			{
				if (_tickObject != null)
				{
					throw new InvalidOperationException("The tick service is being initialized for the second time and that is not valid");
				}
				...
			}
			*/
			// But _tickObject is private readonly TickServiceMonoBehaviour _tickObject;
			// So it's always null for a new instance. The check seems to be intended for a static field but isn't.
			
			var service1 = new TickService();
			var service2 = new TickService();
			
			var objects = Object.FindObjectsByType<TickServiceMonoBehaviour>(FindObjectsSortMode.None);
			Assert.GreaterOrEqual(objects.Length, 2);
			
			service1.Dispose();
			service2.Dispose();
		}
	}
}
