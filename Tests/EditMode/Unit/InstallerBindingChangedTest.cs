using System;
using System.Collections.Generic;
using System.Reflection;
using Geuneda.Core;
using Geuneda.Services;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	[TestFixture]
	public class InstallerBindingChangedTest
	{
		public interface IFirst {}
		public interface ISecond {}
		public class Implementation : IFirst, ISecond {}

		private Installer _installer;
		private List<Type> _changes;
		private ILogSink _originalSink;
		private List<LogEntry> _logs;

		private sealed class ListSink : ILogSink
		{
			private readonly List<LogEntry> _entries;

			public ListSink(List<LogEntry> entries)
			{
				_entries = entries;
			}

			public void Write(in LogEntry entry)
			{
				_entries.Add(entry);
			}
		}

		[SetUp]
		public void Init()
		{
			_logs = new List<LogEntry>();
			_originalSink = Log.Sink;
			Log.Sink = new ListSink(_logs);
			_installer = new Installer();
			_changes = new List<Type>();
		}

		[TearDown]
		public void Cleanup()
		{
			Log.Sink = _originalSink;
			GetResetMethod().Invoke(null, null);
		}

		[Test]
		public void Bind_RaisesAfterBindingIsResolvable()
		{
			var implementation = new Implementation();
			object resolvedInHandler = null;
			_installer.BindingChanged += type =>
			{
				_changes.Add(type);
				_installer.TryResolve<IFirst>(out var resolved);
				resolvedInHandler = resolved;
			};

			_installer.Bind<IFirst>(implementation);

			CollectionAssert.AreEqual(new[] { typeof(IFirst) }, _changes);
			Assert.AreSame(implementation, resolvedInHandler);
		}

		[Test]
		public void Bind_MultiInterface_RaisesPerInterface()
		{
			_installer.BindingChanged += _changes.Add;

			_installer.Bind<Implementation, IFirst, ISecond>(new Implementation());

			CollectionAssert.AreEqual(new[] { typeof(IFirst), typeof(ISecond) }, _changes);
		}

		[Test]
		public void Bind_Fails_DoesNotRaise()
		{
			_installer.Bind<IFirst>(new Implementation());
			_installer.BindingChanged += _changes.Add;

			Assert.Throws<ArgumentException>(() => _installer.Bind<IFirst>(new Implementation()));
			Assert.Throws<ArgumentException>(() => _installer.Bind(new Implementation()));

			CollectionAssert.IsEmpty(_changes);
		}

		[Test]
		public void BindMultiInterface_LaterInterfaceFails_EarlierBindingAndNotificationRemain()
		{
			// 다중 인터페이스 Bind 는 원자적이지 않다. 뒤쪽이 실패해도 앞서 성공한 바인딩과 그 알림은 그대로다
			_installer.Bind<ISecond>(new Implementation());
			_installer.BindingChanged += _changes.Add;

			Assert.Throws<ArgumentException>(() => _installer.Bind<Implementation, IFirst, ISecond>(new Implementation()));

			CollectionAssert.AreEqual(new[] { typeof(IFirst) }, _changes);
			Assert.IsTrue(_installer.TryResolve<IFirst>(out _));
		}

		[Test]
		public void CleanGeneric_RaisesOnlyWhenRemoved()
		{
			_installer.Bind<IFirst>(new Implementation());
			var stillBound = true;
			_installer.BindingChanged += type =>
			{
				_changes.Add(type);
				stillBound = _installer.TryResolve<IFirst>(out _);
			};

			Assert.IsFalse(_installer.Clean<ISecond>());
			Assert.IsTrue(_installer.Clean<IFirst>());
			Assert.IsFalse(_installer.Clean<IFirst>());

			CollectionAssert.AreEqual(new[] { typeof(IFirst) }, _changes);
			Assert.IsFalse(stillBound);
		}

		[Test]
		public void CleanAll_RaisesPerTypeAfterEverythingIsRemoved()
		{
			_installer.Bind<IFirst>(new Implementation());
			_installer.Bind<ISecond>(new Implementation());
			var boundCounts = new List<int>();
			_installer.BindingChanged += type =>
			{
				_changes.Add(type);
				boundCounts.Add((_installer.TryResolve<IFirst>(out _) ? 1 : 0) + (_installer.TryResolve<ISecond>(out _) ? 1 : 0));
			};

			_installer.Clean();

			CollectionAssert.AreEquivalent(new[] { typeof(IFirst), typeof(ISecond) }, _changes);
			CollectionAssert.AreEqual(new[] { 0, 0 }, boundCounts);
		}

		[Test]
		public void CleanAll_Empty_DoesNotRaise()
		{
			_installer.BindingChanged += _changes.Add;

			_installer.Clean();

			CollectionAssert.IsEmpty(_changes);
		}

		[Test]
		public void HandlerThrows_IsLoggedOthersStillRunAndBindSucceeds()
		{
			_installer.BindingChanged += _ => throw new InvalidOperationException("handler failure");
			_installer.BindingChanged += _changes.Add;

			Assert.DoesNotThrow(() => _installer.Bind<IFirst>(new Implementation()));

			CollectionAssert.AreEqual(new[] { typeof(IFirst) }, _changes);
			Assert.IsTrue(_installer.TryResolve<IFirst>(out _));
			Assert.IsTrue(_logs.Exists(entry => entry.Level == LogLevel.Error && entry.Exception is InvalidOperationException));
		}

		[Test]
		public void Unsubscribe_SameHandlerTwice_RemovesLastOneOnly()
		{
			Action<Type> handler = _changes.Add;
			_installer.BindingChanged += handler;
			_installer.BindingChanged += handler;
			_installer.BindingChanged -= handler;

			_installer.Bind<IFirst>(new Implementation());

			Assert.AreEqual(1, _changes.Count);

			_installer.BindingChanged -= handler;
			_installer.BindingChanged -= handler;
			_installer.Bind<ISecond>(new Implementation());

			Assert.AreEqual(1, _changes.Count);
		}

		[Test]
		public void NullHandler_IsIgnored()
		{
			Assert.DoesNotThrow(() => _installer.BindingChanged += null);
			Assert.DoesNotThrow(() => _installer.BindingChanged -= null);
			Assert.DoesNotThrow(() => _installer.Bind<IFirst>(new Implementation()));
		}

		[Test]
		public void HandlerAddedDuringRaise_IsCalledFromNextChange()
		{
			var late = new List<Type>();
			Action<Type> lateHandler = late.Add;
			var added = false;
			_installer.BindingChanged += _ =>
			{
				if (!added)
				{
					added = true;
					_installer.BindingChanged += lateHandler;
				}
			};

			_installer.Bind<IFirst>(new Implementation());

			CollectionAssert.IsEmpty(late);

			_installer.Bind<ISecond>(new Implementation());

			CollectionAssert.AreEqual(new[] { typeof(ISecond) }, late);
		}

		[Test]
		public void HandlerBindsInsideRaise_NestedChangeIsDelivered()
		{
			_installer.BindingChanged += type =>
			{
				_changes.Add(type);
				if (type == typeof(IFirst))
				{
					_installer.Bind<ISecond>(new Implementation());
				}
			};

			_installer.Bind<IFirst>(new Implementation());

			CollectionAssert.AreEqual(new[] { typeof(IFirst), typeof(ISecond) }, _changes);
		}

		[Test]
		public void SubscribeAndUnsubscribe_WithManyHandlers_DoesNotAllocate()
		{
			// 일반 이벤트는 += / -= 마다 처리기 배열 전체를 새로 만든다. 화면 컴포넌트가 많을 때 가비지를 만들지 않는지 확인한다
			var handlers = new Action<Type>[256];
			var calls = new int[handlers.Length];
			for (var i = 0; i < handlers.Length; i++)
			{
				// 인덱스를 캡처해 서로 다른 델리게이트로 만든다(캡처 없는 람다는 한 인스턴스로 캐시된다)
				var index = i;
				handlers[i] = _ => calls[index]++;
				_installer.BindingChanged += handlers[i];
			}

			var probe = handlers[128];

			// 검사 자체가 할당을 잡는지 먼저 확인한다
			Assert.That(() => { _ = new object[8]; }, Is.AllocatingGCMemory());
			Assert.That(() =>
			{
				_installer.BindingChanged -= probe;
				_installer.BindingChanged += probe;
			}, Is.Not.AllocatingGCMemory());

			_installer.Bind<IFirst>(new Implementation());

			CollectionAssert.AreEqual(System.Linq.Enumerable.Repeat(1, handlers.Length), calls);
		}

		[Test]
		public void MainInstaller_BindingChanged_ForwardsBindAndClean()
		{
			MainInstaller.BindingChanged += _changes.Add;

			MainInstaller.Bind<IFirst>(new Implementation());
			MainInstaller.Clean<IFirst>();
			MainInstaller.BindingChanged -= _changes.Add;
			MainInstaller.Bind<IFirst>(new Implementation());

			CollectionAssert.AreEqual(new[] { typeof(IFirst), typeof(IFirst) }, _changes);
		}

		[Test]
		public void MainInstaller_CleanDispose_RaisesAfterRemoval()
		{
			var disposable = new DisposableImplementation();
			MainInstaller.Bind<IDisposableFirst>(disposable);
			var stillBound = true;
			MainInstaller.BindingChanged += type => stillBound = MainInstaller.TryResolve<IDisposableFirst>(out _);

			MainInstaller.CleanDispose<IDisposableFirst>();

			Assert.IsTrue(disposable.Disposed);
			Assert.IsFalse(stillBound);
		}

		[Test]
		public void MainInstaller_SubsystemRegistrationReset_DropsHandlersWithoutRaising()
		{
			// 도메인 리로드 없이 다시 Play 에 들어가면 이전 세션 처리기는 파괴된 오브젝트를 가리킬 수 있다. 호출하지 않고 버려야 한다
			MainInstaller.Bind<IFirst>(new Implementation());
			MainInstaller.BindingChanged += _changes.Add;

			GetResetMethod().Invoke(null, null);

			CollectionAssert.IsEmpty(_changes);
			Assert.IsFalse(MainInstaller.TryResolve<IFirst>(out _));

			MainInstaller.Bind<IFirst>(new Implementation());

			CollectionAssert.IsEmpty(_changes);
		}

		public interface IDisposableFirst : IDisposable {}

		private sealed class DisposableImplementation : IDisposableFirst
		{
			public bool Disposed { get; private set; }

			public void Dispose()
			{
				Disposed = true;
			}
		}

		private static MethodInfo GetResetMethod()
		{
			var method = typeof(MainInstaller).GetMethod("ResetOnSubsystemRegistration", BindingFlags.NonPublic | BindingFlags.Static);

			Assert.IsNotNull(method, "MainInstaller.ResetOnSubsystemRegistration 을 찾을 수 없습니다");

			return method;
		}
	}
}
