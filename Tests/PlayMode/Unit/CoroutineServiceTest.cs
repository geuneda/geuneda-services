using System.Collections;
using Geuneda.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	public class CoroutineServiceTest
	{
		private CoroutineService _coroutineService;
		private int _testValue;

		private IEnumerator TestCoroutine(int value)
		{
			yield return null;

			_testValue = value;
		}

		[SetUp]
		public void Init()
		{
			_coroutineService = new CoroutineService();
			_testValue = 0;
		}

		[TearDown]
		public void Dispose()
		{
			_coroutineService.Dispose();
		}
		
		[UnityTest]
		public IEnumerator StartCoroutine_Successfully()
		{
			const int testValue1 = 5;

			yield return _coroutineService.StartCoroutine(TestCoroutine(testValue1));
			
			Assert.AreEqual(testValue1, _testValue); 
		}
		
		[UnityTest]
		public IEnumerator StartAsyncCoroutine_Successfully()
		{
			const int testValue1 = 5;
			const int testValue2 = 10;
			int testCompleted = 0;

			IAsyncCoroutine asyncCoroutine = _coroutineService.StartAsyncCoroutine(TestCoroutine(testValue1));
			asyncCoroutine.OnComplete(() => testCompleted = testValue2);

			yield return asyncCoroutine.Coroutine;
			
			Assert.IsTrue(asyncCoroutine.IsCompleted);
			Assert.AreEqual(testValue1, _testValue); 
			Assert.AreEqual(testValue2, testCompleted); 
		}
		
		[UnityTest]
		public IEnumerator StartAsyncCoroutine_WithData_Successfully()
		{
			const int testValue1 = 5;
			const int testValue2 = 10;
			int testCompleted = 0;

			var asyncCoroutine = _coroutineService.StartAsyncCoroutine(TestCoroutine(testValue1), testValue2);
			asyncCoroutine.OnComplete(newValue => testCompleted = newValue);

			yield return asyncCoroutine.Coroutine;
			
			Assert.IsTrue(asyncCoroutine.IsCompleted);
			Assert.AreEqual(testValue1, _testValue); 
			Assert.AreEqual(testValue2, testCompleted); 
		}
		
		[UnityTest]
		public IEnumerator StopCoroutine_Successfully()
		{
			const int testValue1 = 5;

			var coroutine = _coroutineService.StartCoroutine(TestCoroutine(testValue1));
			_coroutineService.StopCoroutine(coroutine);
			
			Assert.AreNotEqual(testValue1, _testValue); 

			yield return new WaitForSeconds(0.1f);
			
			Assert.AreNotEqual(testValue1, _testValue); 
		}
		
		[UnityTest]
		public IEnumerator StopAsyncCoroutine_Successfully()
		{
			const int testValue1 = 5;
			const int testValue2 = 10;
			int testCompleted = 0;

			IAsyncCoroutine asyncCoroutine = _coroutineService.StartAsyncCoroutine(TestCoroutine(testValue1));
			asyncCoroutine.OnComplete(() => testCompleted = testValue2);
			
			_coroutineService.StopCoroutine(asyncCoroutine.Coroutine);
			
			Assert.False(asyncCoroutine.IsCompleted);
			Assert.AreNotEqual(testValue1, _testValue); 
			Assert.AreNotEqual(testValue2, testCompleted); 

			yield return new WaitForSeconds(0.1f);
			
			Assert.False(asyncCoroutine.IsCompleted);
			Assert.AreNotEqual(testValue1, _testValue); 
			Assert.AreNotEqual(testValue2, testCompleted); 
		}
		
		[UnityTest]
		public IEnumerator StopAllCoroutines_Successfully()
		{
			const int testValue1 = 5;
			const int testValue2 = 10;
			const int testValue3 = 20;
			int testCompleted = 0;

			IAsyncCoroutine asyncCoroutine = _coroutineService.StartAsyncCoroutine(TestCoroutine(testValue1));
			asyncCoroutine.OnComplete(() => testCompleted = testValue2);
			_coroutineService.StartCoroutine(TestCoroutine(testValue3));
			
			_coroutineService.StopAllCoroutines();
			
			Assert.False(asyncCoroutine.IsCompleted);
			Assert.AreNotEqual(testValue1, _testValue); 
			Assert.AreNotEqual(testValue2, testCompleted); 
			Assert.AreNotEqual(testValue3, _testValue); 

			yield return new WaitForSeconds(0.1f);
			
			Assert.False(asyncCoroutine.IsCompleted);
			Assert.AreNotEqual(testValue1, _testValue); 
			Assert.AreNotEqual(testValue2, testCompleted); 
			Assert.AreNotEqual(testValue3, _testValue); 
		}

		[UnityTest]
		public IEnumerator Dispose_DestroysHostGameObject()
		{
			var initialCount = Object.FindObjectsByType<CoroutineServiceMonoBehaviour>(FindObjectsSortMode.None).Length;
			var service = new CoroutineService();

			Assert.AreEqual(
				initialCount + 1,
				Object.FindObjectsByType<CoroutineServiceMonoBehaviour>(FindObjectsSortMode.None).Length);

			service.Dispose();
			yield return null;

			Assert.AreEqual(
				initialCount,
				Object.FindObjectsByType<CoroutineServiceMonoBehaviour>(FindObjectsSortMode.None).Length);
		}

		[UnityTest]
		public IEnumerator Dispose_CalledTwice_DoesNotThrow()
		{
			var service = new CoroutineService();
			service.Dispose();
			yield return null;

			Assert.DoesNotThrow(() => service.Dispose());
		}

		[UnityTest]
		public IEnumerator StartDelayCall_Successfully()
		{
			bool called = false;
			_coroutineService.StartDelayCall(() => called = true, delay: 0.05f);

			Assert.IsFalse(called);

			yield return new WaitForSeconds(0.2f);

			Assert.IsTrue(called);
		}

		[UnityTest]
		public IEnumerator StartDelayCall_WithData_Successfully()
		{
			int received = 0;
			_coroutineService.StartDelayCall<int>(data => received = data, data: 99, delay: 0.05f);

			Assert.AreEqual(0, received);

			yield return new WaitForSeconds(0.2f);

			Assert.AreEqual(99, received);
		}

		// IAsyncCoroutine.StopCoroutine 으로 중지하면 반드시 IsCompleted/IsRunning 이 바뀌어야
		// 에디터 검사 도구(Services Explorer 의 Coroutine 탭)가 중지된 항목을 제거할 수 있다.
		[UnityTest]
		public IEnumerator AsyncCoroutineStop_FlipsCompletedAndRunning()
		{
			IAsyncCoroutine asyncCoroutine = _coroutineService.StartAsyncCoroutine(TestCoroutine(5));

			Assert.IsTrue(asyncCoroutine.IsRunning);
			Assert.IsFalse(asyncCoroutine.IsCompleted);

			asyncCoroutine.StopCoroutine();

			Assert.IsFalse(asyncCoroutine.IsRunning);
			Assert.IsTrue(asyncCoroutine.IsCompleted);

			yield return null;
		}

		// triggerOnComplete=true 이면 반드시 사용자 OnComplete 콜백을 호출해야 한다.
		[UnityTest]
		public IEnumerator AsyncCoroutineStop_TriggerOnCompleteTrue_InvokesUserCallback()
		{
			int testCompleted = 0;
			IAsyncCoroutine asyncCoroutine = _coroutineService.StartAsyncCoroutine(TestCoroutine(5));
			asyncCoroutine.OnComplete(() => testCompleted = 42);

			asyncCoroutine.StopCoroutine(triggerOnComplete: true);

			Assert.AreEqual(42, testCompleted);

			yield return null;
		}

		// triggerOnComplete=false 이면 반드시 사용자 OnComplete 콜백을 호출하지 않아야 한다.
		[UnityTest]
		public IEnumerator AsyncCoroutineStop_TriggerOnCompleteFalse_SuppressesUserCallback()
		{
			int testCompleted = 0;
			IAsyncCoroutine asyncCoroutine = _coroutineService.StartAsyncCoroutine(TestCoroutine(5));
			asyncCoroutine.OnComplete(() => testCompleted = 42);

			asyncCoroutine.StopCoroutine(triggerOnComplete: false);

			Assert.AreEqual(0, testCompleted);

			yield return null;
		}

		// 에디터 추적이 붙은 *이후에* 등록한 사용자 OnComplete 콜백도 호출되어야 한다.
		// OnComplete(...) 로 할당한 에디터 추적 람다가 사용자 콜백을 덮어쓰던(또는 사용자 콜백에
		// 덮어써지던) v2.0.0 버그의 회귀를 막는다.
		[UnityTest]
		public IEnumerator AsyncCoroutineOnComplete_RegisteredAfterCreation_FiresOnNaturalCompletion()
		{
			bool userCallbackFired = false;
			IAsyncCoroutine asyncCoroutine = _coroutineService.StartAsyncCoroutine(TestCoroutine(5));
			asyncCoroutine.OnComplete(() => userCallbackFired = true);

			yield return asyncCoroutine.Coroutine;

			Assert.IsTrue(userCallbackFired);
			Assert.IsTrue(asyncCoroutine.IsCompleted);
		}

		// 이미 완료된 핸들에 대한 StopCoroutine 은 아무 동작도 하지 않아야 한다 — IsCompleted
		// 가드가 사용자 OnComplete 콜백의 재호출과 IsRunning 상태가 덮어써지는 것을 막는다.
		// 아래의 AsyncCoroutineStop_CalledTwice_NoOps 와 짝을 이룬다.
		[UnityTest]
		public IEnumerator AsyncCoroutineStop_AfterNaturalCompletion_NoOps()
		{
			int callbackInvocations = 0;
			IAsyncCoroutine asyncCoroutine = _coroutineService.StartAsyncCoroutine(TestCoroutine(5));
			asyncCoroutine.OnComplete(() => callbackInvocations++);

			yield return asyncCoroutine.Coroutine;

			Assert.IsTrue(asyncCoroutine.IsCompleted);
			Assert.AreEqual(1, callbackInvocations);

			asyncCoroutine.StopCoroutine(triggerOnComplete: true);

			Assert.IsTrue(asyncCoroutine.IsCompleted);
			Assert.IsFalse(asyncCoroutine.IsRunning);
			Assert.AreEqual(1, callbackInvocations);
		}

		// 같은 핸들에 StopCoroutine 을 연속 두 번 호출해도 한 번으로 처리되어야 한다 — 두 번째
		// 호출은 아무 동작도 하지 않으므로 사용자 OnComplete 콜백은 총 정확히 한 번 호출된다.
		// IsCompleted 가드가 없으면 이중 중지로 OnComplete 가 두 번 호출되어 리스너가 혼란을 겪는다.
		[UnityTest]
		public IEnumerator AsyncCoroutineStop_CalledTwice_NoOps()
		{
			int callbackInvocations = 0;
			IAsyncCoroutine asyncCoroutine = _coroutineService.StartAsyncCoroutine(TestCoroutine(5));
			asyncCoroutine.OnComplete(() => callbackInvocations++);

			asyncCoroutine.StopCoroutine(triggerOnComplete: true);
			Assert.IsTrue(asyncCoroutine.IsCompleted);
			Assert.AreEqual(1, callbackInvocations);

			asyncCoroutine.StopCoroutine(triggerOnComplete: true);

			Assert.IsTrue(asyncCoroutine.IsCompleted);
			Assert.IsFalse(asyncCoroutine.IsRunning);
			Assert.AreEqual(1, callbackInvocations);

			yield return null;
		}

		[UnityTest]
		public IEnumerator AsyncCoroutineDataSetter_AfterStart_UpdatesPayload()
		{
			const int initialValue = 5;
			const int mutatedValue = 99;
			int observed = 0;

			var asyncCoroutine = _coroutineService.StartAsyncCoroutine(TestCoroutine(0), initialValue);
			asyncCoroutine.OnComplete(payload => observed = payload);

			asyncCoroutine.Data = mutatedValue;

			yield return asyncCoroutine.Coroutine;

			Assert.AreEqual(mutatedValue, observed);
			Assert.AreEqual(mutatedValue, asyncCoroutine.Data);
		}
	}
}