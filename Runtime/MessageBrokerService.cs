using System;
using System.Collections;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <summary>
	/// <see cref="IMessageBrokerService"/>를 통해 발행되는 모든 메시지에 사용해야 하는 메시지 계약
	/// </summary>
	public interface IMessage { }

	/// <summary>
	/// 메시지 브로커 실행을 제공하는 서비스입니다.
	/// 메시지 이벤트를 발송하여 모든 옵저버 구독자가 수신할 수 있도록 함으로써,
	/// 독립적인 통신 채널을 통해 시스템 전반의 객체를 쉽게 분리할 수 있게 합니다.
	/// </summary>
	/// <remarks>
	/// "메시지 브로커 패턴"을 따릅니다 <see cref="https://en.wikipedia.org/wiki/Message_broker"/>
	/// </remarks>
	public interface IMessageBrokerService
	{
		/// <summary>
		/// 메시지 브로커에 메시지를 발행합니다.
		/// 해당 메시지 타입을 구독하는 객체가 없으면 아무 일도 일어나지 않습니다.
		/// </summary>
		/// <remarks>
		/// 발행 중 체인 구독이 있는 경우 <see cref="PublishSafe{T}(T)"/>를 사용하세요
		/// </remarks>
		void Publish<T>(T message) where T : IMessage;

		/// <summary>
		/// 메시지 브로커에 메시지를 발행합니다.
		/// 해당 메시지 타입을 구독하는 객체가 없으면 아무 일도 일어나지 않습니다.
		/// </summary>
		/// <remarks>
		/// <typeparamref name="T"/>의 구독자가 많으면 이 메서드는 느리고 추가 메모리를 할당할 수 있습니다.
		/// 발행 중 체인 구독이 없는 경우에만 더 빠른 반복 속도를 위해 <see cref="Publish{T}(T)"/>를 사용하세요.
		/// </remarks>
		void PublishSafe<T>(T message) where T : IMessage;

		/// <summary>
		/// 메시지 타입을 구독합니다.
		/// 구독된 타입의 메시지가 발행될 때마다 <paramref name="action"/>을 호출합니다.
		/// </summary>
		void Subscribe<T>(Action<T> action) where T : IMessage;

		/// <summary>
		/// 메시지 브로커에서 <paramref name="subscriber"/>의 <typeparamref name="T"/> 액션 구독을 해제합니다.
		/// <paramref name="subscriber"/>가 null이면 현재 <typeparamref name="T"/>를 구독 중인 모든 구독자를 해제합니다.
		/// </summary>
		void Unsubscribe<T>(object subscriber = null) where T : IMessage;
		
		/// <summary>
		/// 모든 메시지 구독을 해제합니다.
		/// <paramref name="subscriber"/>가 null이면 모든 구독을 해제하고, 그렇지 않으면 해당 구독자의 구독만 해제합니다.
		/// </summary>
		void UnsubscribeAll(object subscriber = null);
	}

	/// <inheritdoc />
	public class MessageBrokerService : IMessageBrokerService
	{
		private readonly IDictionary<Type, IDictionary<object, Delegate>> _subscriptions = new Dictionary<Type, IDictionary<object, Delegate>>();

		private (bool, IMessage) _isPublishing;

		/// <inheritdoc />
		public void Publish<T>(T message) where T : IMessage
		{
			if (!_subscriptions.TryGetValue(typeof(T), out var subscriptionObjects))
			{
				return;
			}

			_isPublishing = (true, message);

			foreach (var subscription in subscriptionObjects)
			{
				var action = (Action<T>)subscription.Value;

				action(message);
			}

			_isPublishing = (false, message);
		}

		/// <inheritdoc />
		public void PublishSafe<T>(T message) where T : IMessage
		{
			if (!_subscriptions.TryGetValue(typeof(T), out var subscriptionObjects))
			{
				return;
			}

			var subscriptionCopy = new Delegate[subscriptionObjects.Count];

			subscriptionObjects.Values.CopyTo(subscriptionCopy, 0);

			for (var i = 0; i < subscriptionCopy.Length; i++)
			{
				var action = (Action<T>)subscriptionCopy[i];

				action(message);
			}
		}

		/// <inheritdoc />
		public void Subscribe<T>(Action<T> action) where T : IMessage
		{
			var type = typeof(T);
			var subscriber = action.Target;

			if (subscriber == null)
			{
				throw new ArgumentException("Subscribe static functions to a message is not supported!");
			}
			if(_isPublishing.Item1)
			{
				throw new InvalidOperationException($"Cannot subscribe to {type.Name} message while publishing " +
					$"{_isPublishing.Item2.GetType().Name} message. Use {nameof(PublishSafe)} instead!");
			}

			if (!_subscriptions.TryGetValue(type, out var subscriptionObjects))
			{
				subscriptionObjects = new Dictionary<object, Delegate>();
				_subscriptions.Add(type, subscriptionObjects);
			}

			subscriptionObjects[subscriber] = action;
		}

		/// <inheritdoc />
		public void Unsubscribe<T>(object subscriber = null) where T : IMessage
		{
			var type = typeof(T);

			if (subscriber == null)
			{
				_subscriptions.Remove(type);

				return;
			}

			if (_isPublishing.Item1)
			{
				throw new InvalidOperationException($"Cannot unsubscribe to {type.Name} message while publishing " +
					$"{_isPublishing.Item2.GetType().Name} message. Use {nameof(PublishSafe)} instead!");
			}
			if (!_subscriptions.TryGetValue(type, out var subscriptionObjects))
			{
				return;
			}

			subscriptionObjects.Remove(subscriber);

			if (subscriptionObjects.Count == 0)
			{
				_subscriptions.Remove(type);
			}
		}

		/// <inheritdoc />
		public void UnsubscribeAll(object subscriber = null)
		{
			if (subscriber == null)
			{
				_subscriptions.Clear();
				return;
			}

			if (_isPublishing.Item1)
			{
				throw new InvalidOperationException($"Cannot unsubscribe from {subscriber} message while publishing " +
					$"{_isPublishing.Item2.GetType().Name} message. Use {nameof(PublishSafe)} instead!");
			}
			foreach (var subscriptionObjects in _subscriptions.Values)
			{
				subscriptionObjects.Remove(subscriber);
			}
		}
	}
}