using System.Collections;
using Geuneda.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
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

			yield return null; // 다음 프레임 대기
			yield return null; // 확인을 위해 한 프레임 더 대기

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
			
			// 오버플로우가 전달되면 최소 두 번 트리거되어야 합니다
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

		private class TickSubscriber
		{
			public int CallCount;
			public void OnTick(float dt) => CallCount++;
		}

		[UnityTest]
		public IEnumerator UnsubscribeAll_RemovesAllSubscribers()
		{
			var sub1 = new TickSubscriber();
			var sub2 = new TickSubscriber();

			_tickService.SubscribeOnUpdate(sub1.OnTick);
			_tickService.SubscribeOnUpdate(sub2.OnTick);

			_tickService.UnsubscribeAll();

			yield return null;

			Assert.AreEqual(0, sub1.CallCount);
			Assert.AreEqual(0, sub2.CallCount);
		}

		[UnityTest]
		public IEnumerator UnsubscribeAll_BySubscriber_RemovesOnlyThatSubscriber()
		{
			var sub1 = new TickSubscriber();
			var sub2 = new TickSubscriber();

			_tickService.SubscribeOnUpdate(sub1.OnTick);
			_tickService.SubscribeOnUpdate(sub2.OnTick);

			// 특정 구독자만 지정하여 구독 해제되는지 검증합니다
			_tickService.UnsubscribeAll(sub1);

			yield return null;

			Assert.AreEqual(0, sub1.CallCount, "sub1 should have been unsubscribed");
			Assert.Greater(sub2.CallCount, 0, "sub2 should still receive ticks");
		}

		[UnityTest]
		public IEnumerator Dispose_DestroysGameObject()
		{
			var initialCount = Object.FindObjectsByType<TickServiceMonoBehaviour>(FindObjectsSortMode.None).Length;
			var tickService = new TickService();
			
			Assert.AreEqual(initialCount + 1, Object.FindObjectsByType<TickServiceMonoBehaviour>(FindObjectsSortMode.None).Length);
			
			tickService.Dispose();
			yield return null; // Destroy 완료 대기
			
			Assert.AreEqual(initialCount, Object.FindObjectsByType<TickServiceMonoBehaviour>(FindObjectsSortMode.None).Length);
		}

		[UnityTest]
		public IEnumerator SubscribeOnFixedUpdate_ReceivesDeltaTime()
		{
			float receivedDelta = -1f;
			_tickService.SubscribeOnFixedUpdate(dt => receivedDelta = dt);

			yield return new WaitForFixedUpdate();
			yield return new WaitForFixedUpdate();

			Assert.GreaterOrEqual(receivedDelta, 0f);
		}

		[UnityTest]
		public IEnumerator SubscribeOnLateUpdate_ReceivesDeltaTime()
		{
			float receivedDelta = -1f;
			_tickService.SubscribeOnLateUpdate(dt => receivedDelta = dt);

			yield return null;
			yield return null;

			Assert.GreaterOrEqual(receivedDelta, 0f);
		}

		[UnityTest]
		public IEnumerator UnsubscribeOnFixedUpdate_RemovesCallback()
		{
			int callCount = 0;
			System.Action<float> action = dt => callCount++;
			_tickService.SubscribeOnFixedUpdate(action);

			yield return new WaitForFixedUpdate();
			Assert.GreaterOrEqual(callCount, 1);

			int countAtUnsubscribe = callCount;
			_tickService.UnsubscribeOnFixedUpdate(action);

			yield return new WaitForFixedUpdate();
			yield return new WaitForFixedUpdate();

			Assert.AreEqual(countAtUnsubscribe, callCount);
		}

		[UnityTest]
		public IEnumerator UnsubscribeOnLateUpdate_RemovesCallback()
		{
			int callCount = 0;
			System.Action<float> action = dt => callCount++;
			_tickService.SubscribeOnLateUpdate(action);

			yield return null;
			Assert.GreaterOrEqual(callCount, 1);

			int countAtUnsubscribe = callCount;
			_tickService.UnsubscribeOnLateUpdate(action);

			yield return null;
			yield return null;

			Assert.AreEqual(countAtUnsubscribe, callCount);
		}

		[UnityTest]
		public IEnumerator UnsubscribeAllOnUpdate_RemovesAllUpdateSubscribers()
		{
			var sub1 = new TickSubscriber();
			var sub2 = new TickSubscriber();

			_tickService.SubscribeOnUpdate(sub1.OnTick);
			_tickService.SubscribeOnUpdate(sub2.OnTick);

			_tickService.UnsubscribeAllOnUpdate();

			yield return null;

			Assert.AreEqual(0, sub1.CallCount);
			Assert.AreEqual(0, sub2.CallCount);
		}

		[UnityTest]
		public IEnumerator UnsubscribeAllOnUpdate_BySubscriber_RemovesOnlyThatSubscriber()
		{
			var sub1 = new TickSubscriber();
			var sub2 = new TickSubscriber();

			_tickService.SubscribeOnUpdate(sub1.OnTick);
			_tickService.SubscribeOnUpdate(sub2.OnTick);

			_tickService.UnsubscribeAllOnUpdate(sub1);

			yield return null;

			Assert.AreEqual(0, sub1.CallCount);
			Assert.Greater(sub2.CallCount, 0);
		}

		[UnityTest]
		public IEnumerator UnsubscribeAllOnFixedUpdate_RemovesAllFixedUpdateSubscribers()
		{
			var sub1 = new TickSubscriber();
			var sub2 = new TickSubscriber();

			_tickService.SubscribeOnFixedUpdate(sub1.OnTick);
			_tickService.SubscribeOnFixedUpdate(sub2.OnTick);

			_tickService.UnsubscribeAllOnFixedUpdate();

			yield return new WaitForFixedUpdate();
			yield return new WaitForFixedUpdate();

			Assert.AreEqual(0, sub1.CallCount);
			Assert.AreEqual(0, sub2.CallCount);
		}

		[UnityTest]
		public IEnumerator UnsubscribeAllOnFixedUpdate_BySubscriber_RemovesOnlyThatSubscriber()
		{
			var sub1 = new TickSubscriber();
			var sub2 = new TickSubscriber();

			_tickService.SubscribeOnFixedUpdate(sub1.OnTick);
			_tickService.SubscribeOnFixedUpdate(sub2.OnTick);

			_tickService.UnsubscribeAllOnFixedUpdate(sub1);

			yield return new WaitForFixedUpdate();
			yield return new WaitForFixedUpdate();

			Assert.AreEqual(0, sub1.CallCount);
			Assert.Greater(sub2.CallCount, 0);
		}

		[UnityTest]
		public IEnumerator UnsubscribeAllOnLateUpdate_RemovesAllLateUpdateSubscribers()
		{
			var sub1 = new TickSubscriber();
			var sub2 = new TickSubscriber();

			_tickService.SubscribeOnLateUpdate(sub1.OnTick);
			_tickService.SubscribeOnLateUpdate(sub2.OnTick);

			_tickService.UnsubscribeAllOnLateUpdate();

			yield return null;
			yield return null;

			Assert.AreEqual(0, sub1.CallCount);
			Assert.AreEqual(0, sub2.CallCount);
		}

		[UnityTest]
		public IEnumerator UnsubscribeAllOnLateUpdate_BySubscriber_RemovesOnlyThatSubscriber()
		{
			var sub1 = new TickSubscriber();
			var sub2 = new TickSubscriber();

			_tickService.SubscribeOnLateUpdate(sub1.OnTick);
			_tickService.SubscribeOnLateUpdate(sub2.OnTick);

			_tickService.UnsubscribeAllOnLateUpdate(sub1);

			yield return null;
			yield return null;

			Assert.AreEqual(0, sub1.CallCount);
			Assert.Greater(sub2.CallCount, 0);
		}

		[UnityTest]
		public IEnumerator Unsubscribe_UmbrellaOverload_RemovesActionFromAllThreeUpdateLists()
		{
			int callCount = 0;
			System.Action<float> action = dt => callCount++;

			_tickService.SubscribeOnUpdate(action);
			_tickService.SubscribeOnFixedUpdate(action);
			_tickService.SubscribeOnLateUpdate(action);

			yield return null;
			yield return new WaitForFixedUpdate();
			Assert.Greater(callCount, 0);

			_tickService.Unsubscribe(action);
			int countAtUnsubscribe = callCount;

			yield return null;
			yield return new WaitForFixedUpdate();
			yield return null;
			yield return new WaitForFixedUpdate();

			Assert.AreEqual(countAtUnsubscribe, callCount);
		}

		[Test]
		public void MultipleInstances_CreateMultipleGameObjects()
		{
			// 참고: 서비스가 싱글톤을 강제하지 않지만, _tickObject가 이미 설정된 경우 예외를 발생시킵니다
			// 그러나 _tickObject는 현재 구현에서 인스턴스 필드입니다.
			// 생성자에 다음 검사가 있습니다:
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
			// 하지만 _tickObject는 private readonly TickServiceMonoBehaviour _tickObject;
			// 따라서 새 인스턴스에서는 항상 null입니다. 이 검사는 정적 필드를 위한 것으로 보이지만 그렇지 않습니다.
			
			var service1 = new TickService();
			var service2 = new TickService();
			
			var objects = Object.FindObjectsByType<TickServiceMonoBehaviour>(FindObjectsSortMode.None);
			Assert.GreaterOrEqual(objects.Length, 2);
			
			service1.Dispose();
			service2.Dispose();
		}
	}
}
