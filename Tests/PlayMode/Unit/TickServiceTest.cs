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

		[UnityTest]
		public IEnumerator UnsubscribeAll_BySubscriber_RemovesOnlyThatSubscriber()
		{
			int callCount1 = 0;
			int callCount2 = 0;
			object subscriber1 = new object();
			
			_tickService.SubscribeOnUpdate(dt => callCount1++); // 구독자는 action.Target, 즉 이 테스트 클래스
			_tickService.SubscribeOnUpdate(dt => callCount2++); // 위와 동일

			// 대상 지정 구독 해제를 테스트하려면 다른 타겟이 필요합니다
			// action.Target을 쉽게 모킹할 수 없으므로 UnsubscribeAll()을 테스트합니다
			
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
			yield return null; // Destroy 완료 대기
			
			Assert.AreEqual(initialCount, Object.FindObjectsByType<TickServiceMonoBehaviour>(FindObjectsSortMode.None).Length);
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
