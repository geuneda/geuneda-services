using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <summary>
	/// Tick 서비스는 다른 객체에 업데이트 호출을 제공합니다. OnUpdate, OnLateUpdate, OnFixedUpdate 호출을 처리합니다.
	/// 순수 C# 객체에서도 업데이트 호출을 사용할 수 있으며, 여러 GameObject에 걸친 MonoBehaviour 업데이트 메서드 사용의 오버헤드를 제거합니다.
	/// Tick 서비스는 씬에 게임 오브젝트를 생성하여 이 서비스를 통해 실행될 모든 업데이트 호출의 실제 컨테이너 역할을 합니다.
	/// 또한 씬 로드/언로드 시에도 업데이트 데이터를 유지합니다.
	/// Tick 서비스를 올바르게 정리하려면 <see cref="Dispose"/>를 호출하세요.
	/// </summary>
	public interface ITickService : IDisposable
	{
		/// <summary>
		/// <paramref name="action"/>을 프레임 기반 업데이트에 구독합니다.
		/// 각 호출 사이에 <paramref name="deltaTime"/> 버퍼를 두며, <paramref name="realTime"/> 또는 게임 시간을 사용할 수 있습니다
		/// (게임 시간은 더 빠르거나 느리게 조작할 수 있음).
		/// <paramref name="timeOverflowToNextTick"/>을 설정하면 각 <paramref name="action"/>을 정의된 <paramref name="deltaTime"/>에
		/// 가깝게 호출할 수 있습니다. 이는 업데이트가 항상 동일한 델타 타임으로 실행되지 않으며,
		/// 마지막 프레임 처리가 더 오래 걸렸을 경우 이를 고려하기 때문입니다.
		/// </summary>
		void SubscribeOnUpdate(Action<float> action, float deltaTime = 0f, bool timeOverflowToNextTick = false, bool realTime = false);
		
		/// <summary>
		/// <paramref name="action"/>을 메인 업데이트 이후의 프레임 기반 업데이트에 구독합니다.
		/// 각 호출 사이에 <paramref name="deltaTime"/> 버퍼를 두며, <paramref name="realTime"/> 또는 게임 시간을 사용할 수 있습니다
		/// (게임 시간은 더 빠르거나 느리게 조작할 수 있음).
		/// <paramref name="timeOverflowToNextTick"/>을 설정하면 각 <paramref name="action"/>을 정의된 <paramref name="deltaTime"/>에
		/// 가깝게 호출할 수 있습니다. 이는 업데이트가 항상 동일한 델타 타임으로 실행되지 않으며,
		/// 마지막 프레임 처리가 더 오래 걸렸을 경우 이를 고려하기 때문입니다.
		/// </summary>
		void SubscribeOnLateUpdate(Action<float> action, float deltaTime = 0f, bool timeOverflowToNextTick = false, bool realTime = false);
		
		/// <summary>
		/// <paramref name="action"/>을 고정 업데이트에 구독합니다
		/// </summary>
		void SubscribeOnFixedUpdate(Action<float> action);
		
		/// <summary>
		/// <paramref name="action"/>을 모든 업데이트에서 구독 해제합니다
		/// </summary>
		void Unsubscribe(Action<float> action);
		
		/// <summary>
		/// <paramref name="action"/>을 업데이트에서 구독 해제합니다
		/// </summary>
		void UnsubscribeOnUpdate(Action<float> action);
		
		/// <summary>
		/// <paramref name="action"/>을 고정 업데이트 호출에서 구독 해제합니다
		/// </summary>
		void UnsubscribeOnFixedUpdate(Action<float> action);
		
		/// <summary>
		/// <paramref name="action"/>을 후기 업데이트 호출에서 구독 해제합니다
		/// </summary>
		void UnsubscribeOnLateUpdate(Action<float> action);
		
		/// <summary>
		/// 모든 업데이트 구독을 해제합니다
		/// </summary>
		void UnsubscribeAllOnUpdate();

		/// <summary>
		/// 주어진 <paramref name="subscriber"/>의 모든 업데이트 구독을 해제합니다
		/// </summary>
		void UnsubscribeAllOnUpdate(object subscriber);

		/// <summary>
		/// 모든 고정 업데이트 구독을 해제합니다
		/// </summary>
		void UnsubscribeAllOnFixedUpdate();

		/// <summary>
		/// 주어진 <paramref name="subscriber"/>의 모든 고정 업데이트 구독을 해제합니다
		/// </summary>
		void UnsubscribeAllOnFixedUpdate(object subscriber);

		/// <summary>
		/// 모든 후기 업데이트 구독을 해제합니다
		/// </summary>
		void UnsubscribeAllOnLateUpdate();

		/// <summary>
		/// 주어진 <paramref name="subscriber"/>의 모든 후기 업데이트 구독을 해제합니다
		/// </summary>
		void UnsubscribeAllOnLateUpdate(object subscriber);

		/// <summary>
		/// 모든 업데이트 구독을 해제합니다
		/// </summary>
		void UnsubscribeAll();

		/// <summary>
		/// 주어진 <paramref name="subscriber"/>의 모든 업데이트 구독을 해제합니다
		/// </summary>
		void UnsubscribeAll(object subscriber);
	}

	/// <inheritdoc cref="ITickService"/>
	public class TickService : ITickService
	{
		private readonly TickServiceMonoBehaviour _tickObject;

		private readonly List<TickData> _onUpdateList = new List<TickData>();
		private readonly List<TickData> _onFixedUpdateList = new List<TickData>();
		private readonly List<TickData> _onLateUpdateList = new List<TickData>();

		private int _tickDataIdRef;
		
		public TickService()
		{
			if (_tickObject != null)
			{
				throw new InvalidOperationException("The tick service is being initialized for the second time and that is not valid");
			}

			var gameObject = new GameObject(typeof(TickServiceMonoBehaviour).Name);
			
			Object.DontDestroyOnLoad(gameObject);

			_tickObject = gameObject.AddComponent<TickServiceMonoBehaviour>();
			_tickObject.OnUpdate = OnUpdate;
			_tickObject.OnFixedUpdate = OnFixedUpdate;
			_tickObject.OnLateUpdate = OnLateUpdate;
		}

		/// <summary>
		/// Tick 서비스를 정리하고 게임의 모든 업데이트 호출을 포함하는 틱 게임 오브젝트를 삭제합니다.
		/// 현재 실행 중인 모든 업데이트도 중지됩니다.
		/// </summary>
		public void Dispose()
		{
			Object.Destroy(_tickObject.gameObject);

			_onUpdateList.Clear();
			_onFixedUpdateList.Clear();
			_onLateUpdateList.Clear();
		}

		/// <inheritdoc />
		public void SubscribeOnUpdate(Action<float> action, float deltaTime = 0f, bool timeOverflowToNextTick = false, bool realTime = false)
		{
			_onUpdateList.Add(new TickData
			{
				Id = ++_tickDataIdRef,
				Action = action,
				DeltaTime = deltaTime,
				TimeOverflowToNextTick = timeOverflowToNextTick,
				RealTime = realTime,
				LastTickTime = realTime ? Time.realtimeSinceStartup : Time.time,
				Subscriber = action.Target
			});
		}

		/// <inheritdoc />
		public void SubscribeOnLateUpdate(Action<float> action, float deltaTime = 0f, bool timeOverflowToNextTick = false, bool realTime = false)
		{
			_onLateUpdateList.Add(new TickData
			{
				Id = ++_tickDataIdRef,
				Action = action,
				DeltaTime = deltaTime,
				TimeOverflowToNextTick = timeOverflowToNextTick,
				RealTime = realTime,
				LastTickTime = realTime ? Time.realtimeSinceStartup : Time.time,
				Subscriber = action.Target
			});
		}

		/// <inheritdoc />
		public void SubscribeOnFixedUpdate(Action<float> action)
		{
			_onFixedUpdateList.Add(new TickData
			{
				Id = ++_tickDataIdRef,
				Action = action,
				Subscriber = action.Target
			});
		}

		/// <inheritdoc />
		public void Unsubscribe(Action<float> action)
		{
			UnsubscribeOnUpdate(action);
			UnsubscribeOnFixedUpdate(action);
			UnsubscribeOnLateUpdate(action);
		}

		/// <inheritdoc />
		public void UnsubscribeOnUpdate(Action<float> action)
		{
			for (int i = 0; i < _onUpdateList.Count; i++)
			{
				if (_onUpdateList[i].Action == action && action.Target == _onUpdateList[i].Subscriber)
				{
					_onUpdateList.RemoveAt(i);
					return;
				}
			}
		}

		/// <inheritdoc />
		public void UnsubscribeOnFixedUpdate(Action<float> action)
		{
			for (int i = 0; i < _onFixedUpdateList.Count; i++)
			{
				if (_onFixedUpdateList[i].Action == action && action.Target == _onFixedUpdateList[i].Subscriber)
				{
					_onFixedUpdateList.RemoveAt(i);
					return;
				}
			}
		}

		/// <inheritdoc />
		public void UnsubscribeOnLateUpdate(Action<float> action)
		{
			for (int i = 0; i < _onLateUpdateList.Count; i++)
			{
				if (_onLateUpdateList[i].Action == action && action.Target == _onLateUpdateList[i].Subscriber)
				{
					_onLateUpdateList.RemoveAt(i);
					return;
				}
			}
		}

		/// <inheritdoc />
		public void UnsubscribeAllOnUpdate()
		{
			_onUpdateList.Clear();
		}

		/// <inheritdoc />
		public void UnsubscribeAllOnUpdate(object subscriber)
		{
			_onUpdateList.RemoveAll(data => data.Subscriber == subscriber);
		}

		/// <inheritdoc />
		public void UnsubscribeAllOnFixedUpdate()
		{
			_onFixedUpdateList.Clear();
		}

		/// <inheritdoc />
		public void UnsubscribeAllOnFixedUpdate(object subscriber)
		{
			_onFixedUpdateList.RemoveAll(data => data.Subscriber == subscriber);
		}

		/// <inheritdoc />
		public void UnsubscribeAllOnLateUpdate()
		{
			_onLateUpdateList.Clear();
		}

		/// <inheritdoc />
		public void UnsubscribeAllOnLateUpdate(object subscriber)
		{
			_onLateUpdateList.RemoveAll(data => data.Subscriber == subscriber);
		}

		/// <inheritdoc />
		public void UnsubscribeAll()
		{
			UnsubscribeAllOnUpdate();
			UnsubscribeAllOnFixedUpdate();
			UnsubscribeAllOnLateUpdate();
		}

		/// <inheritdoc />
		public void UnsubscribeAll(object subscriber)
		{
			UnsubscribeAllOnUpdate(subscriber);
			UnsubscribeAllOnFixedUpdate(subscriber);
			UnsubscribeAllOnLateUpdate(subscriber);
		}

		private void OnUpdate()
		{
			if (_onUpdateList.Count == 0)
			{
				return;
			}

			Update(_onUpdateList);
		}

		private void OnFixedUpdate()
		{
			if (_onFixedUpdateList.Count == 0)
			{
				return;
			}

			// 반복 중 안전한 변경을 위해 역순으로 순회
			for (int i = _onFixedUpdateList.Count - 1; i >= 0; i--)
			{
				// 이전 액션에 의해 항목이 제거된 경우 건너뜀
				if (i >= _onFixedUpdateList.Count)
				{
					continue;
				}

				_onFixedUpdateList[i].Action(Time.fixedTime);
			}
		}

		private void OnLateUpdate()
		{
			if (_onLateUpdateList.Count == 0)
			{
				return;
			}

			Update(_onLateUpdateList);
		}

		private void Update(List<TickData> list)
		{
			if (list.Count == 0)
			{
				return;
			}

			// 반복 중 안전한 변경을 위해 역순으로 순회
			for (int i = list.Count - 1; i >= 0; i--)
			{
				// 이전 액션에 의해 항목이 제거된 경우 건너뜀
				if (i >= list.Count)
				{
					continue;
				}

				var tickData = list[i];
				var time = tickData.RealTime ? Time.realtimeSinceStartup : Time.time;

				if (time < tickData.LastTickTime + tickData.DeltaTime)
				{
					continue;
				}

				var deltaTime = time - tickData.LastTickTime;

				tickData.Action(deltaTime);

				// 항목이 여전히 존재하고 구독 해제되지 않았는지 확인
				if (i < list.Count && list[i] == tickData)
				{
					var overFlow = tickData.DeltaTime == 0 ? 0 : deltaTime % tickData.DeltaTime;
					tickData.LastTickTime = tickData.TimeOverflowToNextTick ? time - overFlow : time;
					list[i] = tickData;
				}
			}
		}

		private struct TickData
		{
			public int Id;
			public Action<float> Action;
			public float DeltaTime;
			public bool TimeOverflowToNextTick;
			public bool RealTime;
			public float LastTickTime;
			public object Subscriber;

			public bool Equals(TickData other)
			{
				return other.Id == Id;
			}

			public override bool Equals(object other)
			{
				return other is TickData && Equals((TickData)other);
			}

			public override int GetHashCode()
			{
				return Id;
			}

			public static bool operator ==(TickData a, TickData b)
			{
				return a.Id == b.Id;
			}

			public static bool operator !=(TickData a, TickData b)
			{
				return a.Id != b.Id;
			}
		}
	}

	/// <summary>
	/// <see cref="ITickService"/>에서 처리되는 게임 오브젝트에 부착될 MonoBehaviour 클래스입니다.
	/// 이 객체가 모든 업데이트의 주요 호출자 역할을 합니다.
	/// </summary>
	public class TickServiceMonoBehaviour : MonoBehaviour
	{
		public Action OnUpdate;
		public Action OnFixedUpdate;
		public Action OnLateUpdate;

		private void Update()
		{
			OnUpdate();
		}
		private void FixedUpdate()
		{
			OnFixedUpdate();
		}
		private void LateUpdate()
		{
			OnLateUpdate();
		}
	}
}