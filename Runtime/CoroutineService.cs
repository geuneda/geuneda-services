using System;
using System.Collections;
using UnityEngine;
using Action = System.Action;
using Object = UnityEngine.Object;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <summary>
	/// 코루틴 완료 대기를 위한 인터페이스입니다.
	/// 코루틴 완료를 수동적으로 대기하고, 완료 시 콜백을 호출할 수 있습니다.
	/// </summary>
	public interface IAsyncCoroutine
	{
		/// <summary>
		/// 코루틴의 실행 상태를 가져옵니다
		/// </summary>
		bool IsRunning { get; }
		/// <summary>
		/// 코루틴의 완료 상태를 가져옵니다
		/// </summary>
		bool IsCompleted { get; }
		/// <summary>
		/// 현재 실행 중인 코루틴을 가져옵니다
		/// </summary>
		Coroutine Coroutine { get; }
		/// <summary>
		/// 코루틴이 시작된 Unity 시간
		/// </summary>
		float StartTime { get; }
		
		/// <summary>
		/// 코루틴 완료 시 호출될 <paramref name="onComplete"/> 콜백 액션을 설정합니다
		/// </summary>
		void OnComplete(Action onComplete);
		/// <summary>
		/// 이 코루틴의 실행을 중지합니다
		/// </summary>
		void StopCoroutine(bool triggerOnComplete = false);
	}

	/// <inheritdoc />
	public interface IAsyncCoroutine<T> : IAsyncCoroutine
	{
		/// <summary>
		/// 코루틴 완료 시 반환될 데이터
		/// </summary>
		T Data { get; set; }
		
		/// <summary>
		/// 코루틴 완료 시 <seealso cref="Data"/> 참조와 함께 호출될 <paramref name="onComplete"/> 콜백 액션을 설정합니다
		/// </summary>
		void OnComplete(Action<T> onComplete);
	}
	
	/// <summary>
	/// 코루틴 서비스는 Unity 게임 오브젝트의 범위 밖에서도 코루틴을 사용할 수 있게 합니다.
	/// 순수 C# 클래스에서 코루틴의 기능을 활용할 수 있습니다.
	/// 또한 <see cref="IAsyncCoroutine"/>을 제공하여 코루틴 완료 시 콜백 기능으로 코루틴의 기능을 확장합니다.
	/// 코루틴 서비스는 씬에 게임 오브젝트를 생성하여 이 서비스를 통해 생성될 모든 코루틴의 실제 컨테이너 역할을 합니다.
	/// 코루틴 서비스는 씬 로드/언로드 시에도 코루틴을 유지합니다.
	/// </summary>
	public interface ICoroutineService : IDisposable
	{
		/// <summary>
		/// <see cref="MonoBehaviour.StartCoroutine(IEnumerator)"/>와 동일한 원리로 실행되지만,
		/// 순수 C# 클래스에서 사용할 수 있습니다. 오브젝트가 비활성화된 상태에서도 코루틴을 실행할 수 있어 유용합니다.
		/// </summary>
		Coroutine StartCoroutine(IEnumerator routine);
		/// <summary>
		/// <see cref="MonoBehaviour.StartCoroutine(IEnumerator)"/>와 동일한 원리로 실행되지만,
		/// 코루틴 완료 시 콜백을 제공하는 <see cref="IAsyncCoroutine"/>을 반환합니다
		/// </summary>
		IAsyncCoroutine StartAsyncCoroutine(IEnumerator routine);
		/// <summary>
		/// <see cref="MonoBehaviour.StartCoroutine(IEnumerator)"/>와 동일한 원리로 실행되지만,
		/// 주어진 <paramref name="data"/>와 함께 코루틴 완료 시 콜백을 제공하는 <see cref="IAsyncCoroutine{T}"/>을 반환합니다
		/// </summary>
		IAsyncCoroutine<T> StartAsyncCoroutine<T>(IEnumerator routine, T data);
		/// <summary>
		/// 주어진 <paramref name="delay"/> 후에 <see cref="StartAsyncCoroutine"/>에서 <paramref name="call"/>을 실행합니다.
		/// 지연 콜백에 유용합니다.
		/// </summary>
		IAsyncCoroutine StartDelayCall(Action call, float delay);
		/// <summary>
		/// 주어진 <paramref name="delay"/> 및 <paramref name="data"/> 데이터 타입과 함께
		/// <see cref="StartAsyncCoroutine"/>에서 <paramref name="call"/>을 실행합니다.
		/// 지연 콜백에 유용합니다.
		/// </summary>
		IAsyncCoroutine<T> StartDelayCall<T>(Action<T> call, T data,float delay);
		/// <inheritdoc cref="MonoBehaviour.StopCoroutine(Coroutine)"/>
		void StopCoroutine(Coroutine coroutine);
		/// <inheritdoc cref="MonoBehaviour.StopAllCoroutines"/>
		void StopAllCoroutines();
	}

	/// <inheritdoc cref="ICoroutineService"/>
	public class CoroutineService : ICoroutineService
	{
		private CoroutineServiceMonoBehaviour _serviceObject;

		public CoroutineService()
		{
			var gameObject = new GameObject(nameof(CoroutineServiceMonoBehaviour));

			_serviceObject = gameObject.AddComponent<CoroutineServiceMonoBehaviour>();
			
			Object.DontDestroyOnLoad(gameObject);
		}

		/// <summary>
		/// 코루틴 서비스를 정리하고 게임의 모든 코루틴을 포함하는 코루틴 게임 오브젝트를 삭제합니다.
		/// 현재 실행 중인 모든 코루틴도 중지됩니다.
		/// </summary>
		public void Dispose()
		{
			if(_serviceObject == null)
			{
				return;
			}
			
			_serviceObject.StopAllCoroutines();

			Object.Destroy(_serviceObject.gameObject);

			_serviceObject = null;
		}

		/// <inheritdoc />
		public Coroutine StartCoroutine(IEnumerator routine)
		{
			return _serviceObject.ExternalStartCoroutine(routine);
		}

		/// <inheritdoc />
		public IAsyncCoroutine StartAsyncCoroutine(IEnumerator routine)
		{
			var asyncCoroutine = new AsyncCoroutine(this);

			asyncCoroutine.SetCoroutine(_serviceObject.ExternalStartCoroutine(InternalCoroutine(routine, asyncCoroutine)));

			return asyncCoroutine;
		}

		/// <inheritdoc />
		public IAsyncCoroutine<T> StartAsyncCoroutine<T>(IEnumerator routine, T data)
		{
			var asyncCoroutine = new AsyncCoroutine<T>(this, data);

			asyncCoroutine.SetCoroutine(_serviceObject.ExternalStartCoroutine(InternalCoroutine(routine, asyncCoroutine)));

			return asyncCoroutine;
		}

		/// <inheritdoc />
		public IAsyncCoroutine StartDelayCall(Action call, float delay)
		{
			var asyncCoroutine = new AsyncCoroutine(this);

			asyncCoroutine.OnComplete(call);
			asyncCoroutine.SetCoroutine(_serviceObject.ExternalStartCoroutine(InternalDelayCoroutine(delay, asyncCoroutine)));

			return asyncCoroutine;
		}

		/// <inheritdoc />
		public IAsyncCoroutine<T> StartDelayCall<T>(Action<T> call, T data, float delay)
		{
			var asyncCoroutine = new AsyncCoroutine<T>(this, data);

			asyncCoroutine.OnComplete(call);
			asyncCoroutine.SetCoroutine(_serviceObject.ExternalStartCoroutine(InternalDelayCoroutine(delay, asyncCoroutine)));

			return asyncCoroutine;
		}

		/// <inheritdoc />
		public void StopCoroutine(Coroutine coroutine)
		{
			if (coroutine == null || _serviceObject == null || _serviceObject.gameObject == null)
			{
				return;
			}
			
			_serviceObject.ExternalStopCoroutine(coroutine);
		}

		/// <inheritdoc />
		public void StopAllCoroutines()
		{
			if (_serviceObject == null || _serviceObject.gameObject == null)
			{
				return;
			}
			
			_serviceObject.StopAllCoroutines();
		}

		private static IEnumerator InternalCoroutine(IEnumerator routine, ICompleteCoroutine completed)
		{
			yield return routine;

			completed.Completed();
		}

		private static IEnumerator InternalDelayCoroutine(float delayInSeconds, ICompleteCoroutine completed)
		{
			yield return new WaitForSeconds(delayInSeconds);

			completed.Completed();
		}
		
		#region 비공개 인터페이스
		
		private interface ICompleteCoroutine
		{
			void Completed();
		}
		
		private class AsyncCoroutine : IAsyncCoroutine, ICompleteCoroutine
		{
			private readonly ICoroutineService _coroutineService;
			
			private Action _onComplete;
		
			public bool IsRunning => Coroutine != null;
			public bool IsCompleted { get; private set; }
			public Coroutine Coroutine { get; private set; }
			public float StartTime { get; } = Time.time;
			
			private AsyncCoroutine() {}

			public AsyncCoroutine(ICoroutineService coroutineService)
			{
				_coroutineService = coroutineService;
			}

			public void SetCoroutine(Coroutine coroutine)
			{
				Coroutine = coroutine;
			}
		
			public void OnComplete(Action onComplete)
			{
				_onComplete = onComplete;
			}

			public void StopCoroutine(bool triggerOnComplete = false)
			{
				_coroutineService.StopCoroutine(Coroutine);
				
				OnCompleteTrigger();
			}

			public void Completed()
			{
				if (IsCompleted)
				{
					return;
				}

				IsCompleted = true;
				Coroutine = null;

				OnCompleteTrigger();
			}

			protected virtual void OnCompleteTrigger()
			{
				_onComplete?.Invoke();
			}
		}

		private class AsyncCoroutine<T> : AsyncCoroutine, IAsyncCoroutine<T>
		{
			private Action<T> _onComplete;
			
			public T Data { get; set; }

			public AsyncCoroutine(ICoroutineService coroutineService, T data) : base(coroutineService)
			{
				Data = data;
			}
		
			public void OnComplete(Action<T> onComplete)
			{
				_onComplete = onComplete;
			}

			protected override void OnCompleteTrigger()
			{
				base.OnCompleteTrigger();
				_onComplete?.Invoke(Data);
			}
		}
		
		#endregion
	}

	/// <summary>
	/// <see cref="ICoroutineService"/>에서 생성되는 씬의 게임 오브젝트에 부착될 MonoBehaviour입니다.
	/// 코루틴 서비스에 의해 생성된 모든 코루틴의 컨테이너 역할을 합니다.
	/// </summary>
	public class CoroutineServiceMonoBehaviour : MonoBehaviour
	{
		/// <inheritdoc cref="ICoroutineService.StartCoroutine(IEnumerator)"/>
		public Coroutine ExternalStartCoroutine(IEnumerator routine)
		{
			return StartCoroutine(routine);
		}

		/// <inheritdoc cref="ICoroutineService.StopCoroutine(Coroutine)"/>
		public void ExternalStopCoroutine(Coroutine coroutine)
		{
			StopCoroutine(coroutine);
		}
	}
}