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
		// 지금 순회 중인 구독 리스트와 위치. 콜백 안에서 해제되어 리스트가 당겨지면 위치도 함께 당겨 같은 항목을 두 번 부르지 않는다
		private List<TickData> _iteratingList;
		private int _iterationIndex;

		internal IReadOnlyList<TickData> OnUpdateList => _onUpdateList;
		internal IReadOnlyList<TickData> OnFixedUpdateList => _onFixedUpdateList;
		internal IReadOnlyList<TickData> OnLateUpdateList => _onLateUpdateList;
		
		public TickService()
		{
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
			if (_tickObject != null)
			{
				Object.Destroy(_tickObject.gameObject);
			}

			ClearList(_onUpdateList);
			ClearList(_onFixedUpdateList);
			ClearList(_onLateUpdateList);
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
			RemoveFirst(_onUpdateList, action);
		}

		/// <inheritdoc />
		public void UnsubscribeOnFixedUpdate(Action<float> action)
		{
			RemoveFirst(_onFixedUpdateList, action);
		}

		/// <inheritdoc />
		public void UnsubscribeOnLateUpdate(Action<float> action)
		{
			RemoveFirst(_onLateUpdateList, action);
		}

		/// <inheritdoc />
		public void UnsubscribeAllOnUpdate()
		{
			ClearList(_onUpdateList);
		}

		/// <inheritdoc />
		public void UnsubscribeAllOnUpdate(object subscriber)
		{
			RemoveBySubscriber(_onUpdateList, subscriber);
		}

		/// <inheritdoc />
		public void UnsubscribeAllOnFixedUpdate()
		{
			ClearList(_onFixedUpdateList);
		}

		/// <inheritdoc />
		public void UnsubscribeAllOnFixedUpdate(object subscriber)
		{
			RemoveBySubscriber(_onFixedUpdateList, subscriber);
		}

		/// <inheritdoc />
		public void UnsubscribeAllOnLateUpdate()
		{
			ClearList(_onLateUpdateList);
		}

		/// <inheritdoc />
		public void UnsubscribeAllOnLateUpdate(object subscriber)
		{
			RemoveBySubscriber(_onLateUpdateList, subscriber);
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

			var previousList = _iteratingList;
			var previousIndex = _iterationIndex;

			_iteratingList = _onFixedUpdateList;

			try
			{
				// 역순으로 순회한다. 콜백 안에서 이 단계에 새로 구독한 항목은 뒤에 붙으므로 이 단계의 다음 순회부터 불린다
				for (_iterationIndex = _onFixedUpdateList.Count - 1; _iterationIndex >= 0; _iterationIndex--)
				{
					// 콜백이 항목을 지워 리스트가 짧아진 경우
					if (_iterationIndex >= _onFixedUpdateList.Count)
					{
						continue;
					}

					Invoke(_onFixedUpdateList[_iterationIndex].Action, Time.fixedTime);
				}
			}
			finally
			{
				_iteratingList = previousList;
				_iterationIndex = previousIndex;
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

			var previousList = _iteratingList;
			var previousIndex = _iterationIndex;

			_iteratingList = list;

			try
			{
				// 역순으로 순회한다. 콜백 안에서 이 단계에 새로 구독한 항목은 뒤에 붙으므로 이 단계의 다음 순회부터 불린다
				for (_iterationIndex = list.Count - 1; _iterationIndex >= 0; _iterationIndex--)
				{
					// 콜백이 항목을 지워 리스트가 짧아진 경우
					if (_iterationIndex >= list.Count)
					{
						continue;
					}

					var tickData = list[_iterationIndex];
					var time = tickData.RealTime ? Time.realtimeSinceStartup : Time.time;

					if (time < tickData.LastTickTime + tickData.DeltaTime)
					{
						continue;
					}

					var deltaTime = time - tickData.LastTickTime;

					Invoke(tickData.Action, deltaTime);

					// 콜백 안에서 앞쪽 항목이 해제되면 _iterationIndex 도 함께 당겨지므로, 이 위치에 현재 항목이 남아 있는지 확인한다
					if (_iterationIndex < list.Count && list[_iterationIndex] == tickData)
					{
						var overFlow = tickData.DeltaTime == 0 ? 0 : deltaTime % tickData.DeltaTime;
						tickData.LastTickTime = tickData.TimeOverflowToNextTick ? time - overFlow : time;
						list[_iterationIndex] = tickData;
					}
				}
			}
			finally
			{
				_iteratingList = previousList;
				_iterationIndex = previousIndex;
			}
		}

		// 콜백 하나가 예외를 던져도 같은 프레임의 나머지 구독자는 계속 부른다
		private static void Invoke(Action<float> action, float time)
		{
			try
			{
				action(time);
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
		}

		private void RemoveFirst(List<TickData> list, Action<float> action)
		{
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i].Action == action && action.Target == list[i].Subscriber)
				{
					RemoveAt(list, i);
					return;
				}
			}
		}

		// 남길 항목을 앞으로 모으고 끝을 한 번에 지운다(할당 없이 O(N)). 순회 중이면 현재 위치 앞에서 빠진 수만큼 위치를 당긴다
		private void RemoveBySubscriber(List<TickData> list, object subscriber)
		{
			var isIterating = list == _iteratingList;
			var removedBeforeCursor = 0;
			var write = 0;

			for (var read = 0; read < list.Count; read++)
			{
				var tickData = list[read];

				if (tickData.Subscriber == subscriber)
				{
					if (isIterating && read < _iterationIndex)
					{
						removedBeforeCursor++;
					}

					continue;
				}

				if (write != read)
				{
					list[write] = tickData;
				}

				write++;
			}

			if (write < list.Count)
			{
				list.RemoveRange(write, list.Count - write);
			}

			if (isIterating)
			{
				_iterationIndex -= removedBeforeCursor;
			}
		}

		private void RemoveAt(List<TickData> list, int index)
		{
			list.RemoveAt(index);

			// 순회 중인 리스트에서 아직 부르지 않은 앞쪽 항목이 빠지면 현재 항목이 한 칸 당겨지므로 위치도 당긴다
			if (list == _iteratingList && index < _iterationIndex)
			{
				_iterationIndex--;
			}
		}

		private void ClearList(List<TickData> list)
		{
			list.Clear();

			// 순회 중이었다면 남은 순회를 끝낸다. 콜백이 이어서 이 단계에 새로 구독해도 그 항목은 이 단계의 다음 순회부터 불린다
			if (list == _iteratingList)
			{
				_iterationIndex = 0;
			}
		}

		internal struct TickData
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