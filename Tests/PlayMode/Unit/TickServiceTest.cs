using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Geuneda.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

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

		[UnityTest]
		public IEnumerator UnsubscribeOnUpdate_EarlierSubscriberDuringCallback_CurrentCalledOncePerFrame()
		{
			var earlierCalls = 0;
			var currentFrames = new List<int>();
			Action<float> earlier = dt => earlierCalls++;

			// 역순으로 순회하므로 나중에 구독한 current 가 먼저 불리고, 아직 부르지 않은 earlier 를 해제한다
			_tickService.SubscribeOnUpdate(earlier);
			_tickService.SubscribeOnUpdate(dt =>
			{
				currentFrames.Add(Time.frameCount);
				_tickService.UnsubscribeOnUpdate(earlier);
			});

			yield return null;
			yield return null;

			Assert.AreEqual(0, earlierCalls);
			Assert.IsNotEmpty(currentFrames);
			CollectionAssert.AllItemsAreUnique(currentFrames);
		}

		[UnityTest]
		public IEnumerator UnsubscribeOnFixedUpdate_EarlierSubscriberDuringCallback_CurrentCalledOncePerStep()
		{
			var earlierCalls = 0;
			var currentSteps = new List<float>();
			Action<float> earlier = time => earlierCalls++;

			_tickService.SubscribeOnFixedUpdate(earlier);
			_tickService.SubscribeOnFixedUpdate(time =>
			{
				currentSteps.Add(time);
				_tickService.UnsubscribeOnFixedUpdate(earlier);
			});

			yield return new WaitForFixedUpdate();
			yield return new WaitForFixedUpdate();

			Assert.AreEqual(0, earlierCalls);
			Assert.IsNotEmpty(currentSteps);
			CollectionAssert.AllItemsAreUnique(currentSteps);
		}

		[UnityTest]
		public IEnumerator UnsubscribeAllBySubscriber_EarlierSubscriptionsDuringCallback_CurrentCalledOncePerFrame()
		{
			var subscriber = new TickSubscriber();
			var currentFrames = new List<int>();

			_tickService.SubscribeOnUpdate(subscriber.OnTick);
			_tickService.SubscribeOnUpdate(subscriber.OnTick);
			_tickService.SubscribeOnUpdate(dt =>
			{
				currentFrames.Add(Time.frameCount);
				_tickService.UnsubscribeAll(subscriber);
			});

			yield return null;
			yield return null;

			Assert.AreEqual(0, subscriber.CallCount);
			Assert.IsNotEmpty(currentFrames);
			CollectionAssert.AllItemsAreUnique(currentFrames);
		}

		[UnityTest]
		public IEnumerator UnsubscribeAllBySubscriber_InterleavedWithOthersDuringCallback_KeptSubscribersCalledOncePerFrame()
		{
			var subscriber = new TickSubscriber();
			var firstKeptFrames = new List<int>();
			var secondKeptFrames = new List<int>();
			var currentFrames = new List<int>();

			// [A, K1, A, K2, current] 순서로 구독한다. current 가 먼저 불려 앞쪽의 A 두 개를 지운다
			_tickService.SubscribeOnUpdate(subscriber.OnTick);
			_tickService.SubscribeOnUpdate(dt => firstKeptFrames.Add(Time.frameCount));
			_tickService.SubscribeOnUpdate(subscriber.OnTick);
			_tickService.SubscribeOnUpdate(dt => secondKeptFrames.Add(Time.frameCount));
			_tickService.SubscribeOnUpdate(dt =>
			{
				currentFrames.Add(Time.frameCount);
				_tickService.UnsubscribeAllOnUpdate(subscriber);
			});

			yield return null;
			yield return null;

			Assert.AreEqual(0, subscriber.CallCount);
			Assert.IsNotEmpty(currentFrames);
			CollectionAssert.AllItemsAreUnique(currentFrames);
			CollectionAssert.AreEqual(currentFrames, firstKeptFrames);
			CollectionAssert.AreEqual(currentFrames, secondKeptFrames);
		}

		[UnityTest]
		public IEnumerator UnsubscribeAllOnUpdate_ThenSubscribeDuringCallback_NewSubscriberStartsNextFrame()
		{
			var clearFrame = -1;
			var newFrames = new List<int>();

			_tickService.SubscribeOnUpdate(dt => { });
			_tickService.SubscribeOnUpdate(dt =>
			{
				if (clearFrame >= 0)
				{
					return;
				}

				clearFrame = Time.frameCount;
				_tickService.UnsubscribeAllOnUpdate();
				_tickService.SubscribeOnUpdate(newDt => newFrames.Add(Time.frameCount));
			});

			yield return null;
			yield return null;

			Assert.GreaterOrEqual(clearFrame, 0);
			CollectionAssert.DoesNotContain(newFrames, clearFrame);
			Assert.IsNotEmpty(newFrames);
		}

		[UnityTest]
		public IEnumerator SubscribeOnUpdate_CallbackThrows_OtherSubscribersStillCalledSameFrame()
		{
			var otherCalls = 0;
			var thrown = false;

			// 나중에 구독한 쪽이 먼저 불리므로 예외를 던지는 콜백 뒤에 other 가 불린다
			_tickService.SubscribeOnUpdate(dt => otherCalls++);
			_tickService.SubscribeOnUpdate(dt =>
			{
				if (thrown)
				{
					return;
				}

				thrown = true;
				throw new InvalidOperationException("tick boom");
			});

			LogAssert.Expect(LogType.Error, new Regex(@"\[Geuneda\.Services\.Tick\] A tick callback threw"));
			LogAssert.Expect(LogType.Exception, new Regex("tick boom"));

			yield return null;

			Assert.IsTrue(thrown);
			Assert.GreaterOrEqual(otherCalls, 1);
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
			// 참고: 서비스는 싱글톤을 강제하지 않습니다. 인스턴스마다 자체 호스트 GameObject 를 만듭니다.
			// (v2.1.4 에서 항상 거짓이던 생성자의 _tickObject 중복 초기화 검사를 제거했습니다)

			var service1 = new TickService();
			var service2 = new TickService();
			
			var objects = Object.FindObjectsByType<TickServiceMonoBehaviour>(FindObjectsSortMode.None);
			Assert.GreaterOrEqual(objects.Length, 2);
			
			service1.Dispose();
			service2.Dispose();
		}
	}
}
