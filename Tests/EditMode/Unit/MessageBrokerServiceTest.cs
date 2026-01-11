using System;
using GameLovers.Services;
using NSubstitute;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	public class MessageBrokerServiceTest
	{
		public interface IMockSubscriber
		{
			void MockMessageCall(MessageType1 message);
			void MockMessageCall2(MessageType1 message);
			void MockMessageAlternativeCall(MessageType2 message);
			void MockMessageAlternativeCall2(MessageType2 message);
		}
		
		public struct MessageType1 : IMessage {}
		public struct MessageType2 : IMessage {}

		private MessageType1 _messageType1;
		private MessageType2 _messageType2;
		private IMockSubscriber _subscriber;
		private MessageBrokerService _messageBroker;

		[SetUp]
		public void Init()
		{
			_messageBroker = new MessageBrokerService();
			_subscriber = Substitute.For<IMockSubscriber>();
			_messageType1 = new MessageType1();
			_messageType2 = new MessageType2();
		}

		[Test]
		public void Subscribe_Publish_Successfully()
		{
			_messageBroker.Subscribe<MessageType1>(_subscriber.MockMessageCall);
			_messageBroker.Publish(_messageType1);
			_messageBroker.PublishSafe(_messageType1);

			_subscriber.Received(2).MockMessageCall(_messageType1);
		}

		[Test]
		public void Subscribe_MultipleSubscriptionSameType_ReplacePreviousSubscription()
		{
			_messageBroker.Subscribe<MessageType1>(_subscriber.MockMessageCall);
			_messageBroker.Subscribe<MessageType1>(_subscriber.MockMessageCall2);
			_messageBroker.Publish(_messageType1);
			_messageBroker.PublishSafe(_messageType1);

			_subscriber.DidNotReceive().MockMessageCall(_messageType1);
			_subscriber.Received(2).MockMessageCall2(_messageType1);
		}

		[Test]
		public void Publish_ChainSubscribe_ThrowsException()
		{
			_messageBroker.Subscribe<MessageType1>(m => _messageBroker.Subscribe<MessageType2>(_subscriber.MockMessageAlternativeCall));
			
			Assert.Throws<InvalidOperationException>(() => _messageBroker.Publish(_messageType1));
		}

		[Test]
		public void PublishSafe_ChainSubscribe_Succeeds()
		{
			_messageBroker.Subscribe<MessageType1>(m => _messageBroker.Subscribe<MessageType2>(_subscriber.MockMessageAlternativeCall));
			
			Assert.DoesNotThrow(() => _messageBroker.PublishSafe(_messageType1));
			_messageBroker.Publish(_messageType2);
			
			_subscriber.Received(1).MockMessageAlternativeCall(_messageType2);
		}

		[Test]
		public void Subscribe_StaticMethod_ThrowsException()
		{
			// The current implementation uses action.Target as the key. 
			// For static methods, action.Target is null, which is explicitly checked
			// and throws ArgumentException with a descriptive message.
			
			Assert.Throws<ArgumentException>(() => _messageBroker.Subscribe<MessageType1>(StaticMockCall));
		}

		private static void StaticMockCall(MessageType1 message) {}

		[Test]
		public void Publish_NoSubscribers_DoesNotThrow()
		{
			Assert.DoesNotThrow(() => _messageBroker.Publish(_messageType1));
			Assert.DoesNotThrow(() => _messageBroker.PublishSafe(_messageType1));
		}

		[Test]
		public void Unsubscribe_Successfully()
		{
			_messageBroker.Subscribe<MessageType1>(_subscriber.MockMessageCall);
			_messageBroker.Unsubscribe<MessageType1>(_subscriber);
			_messageBroker.Publish(_messageType1);
			_messageBroker.PublishSafe(_messageType1);

			_subscriber.DidNotReceive().MockMessageCall(_messageType1);
		}

		[Test]
		public void UnsubscribeWithAction_MultipleSubscriptionSameType_RemoveAllScriptionsOfSameType()
		{
			_messageBroker.Subscribe<MessageType1>(_subscriber.MockMessageCall);
			_messageBroker.Subscribe<MessageType1>(_subscriber.MockMessageCall2);
			_messageBroker.Unsubscribe<MessageType1>(_subscriber);
			_messageBroker.Publish(_messageType1);
			_messageBroker.PublishSafe(_messageType1);

			_subscriber.DidNotReceive().MockMessageCall(_messageType1);
			_subscriber.DidNotReceive().MockMessageCall2(_messageType1);
		}

		[Test]
		public void UnsubscribeWithoutAction_KeepsSubscriptionDifferentType_Successfully()
		{
			_messageBroker.Subscribe<MessageType1>(_subscriber.MockMessageCall);
			_messageBroker.Subscribe<MessageType2>(_subscriber.MockMessageAlternativeCall);
			_messageBroker.Unsubscribe<MessageType1>();
			_messageBroker.Publish(_messageType2);
			_messageBroker.PublishSafe(_messageType2);

			_subscriber.DidNotReceive().MockMessageCall(_messageType1);
			_subscriber.Received(2).MockMessageAlternativeCall(_messageType2);
		}

		[Test]
		public void UnsubscribeAll_Successfully()
		{
			_messageBroker.Subscribe<MessageType1>(_subscriber.MockMessageCall);
			_messageBroker.Subscribe<MessageType1>(_subscriber.MockMessageCall2);
			_messageBroker.Subscribe<MessageType2>(_subscriber.MockMessageAlternativeCall);
			_messageBroker.Subscribe<MessageType2>(_subscriber.MockMessageAlternativeCall2);
			_messageBroker.UnsubscribeAll();
			_messageBroker.Publish(_messageType1);
			_messageBroker.Publish(_messageType2);
			_messageBroker.PublishSafe(_messageType1);
			_messageBroker.PublishSafe(_messageType2);

			_subscriber.DidNotReceive().MockMessageCall(_messageType1);
			_subscriber.DidNotReceive().MockMessageCall2(_messageType1);
			_subscriber.DidNotReceive().MockMessageAlternativeCall(_messageType2);
			_subscriber.DidNotReceive().MockMessageAlternativeCall2(_messageType2);
		}

		[Test]
		public void Unsubscribe_WithoutSubscription_DoesNothing()
		{
			Assert.DoesNotThrow(() => _messageBroker.Unsubscribe<MessageType1>(_subscriber));
			Assert.DoesNotThrow(() => _messageBroker.Unsubscribe<MessageType1>());
			Assert.DoesNotThrow(() => _messageBroker.UnsubscribeAll());
		}
	}
}
