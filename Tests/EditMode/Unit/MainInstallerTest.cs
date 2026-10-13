using System;
using System.Reflection;
using Geuneda.Services;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	[TestFixture]
	public class MainInstallerTest
	{
		public interface IInterface {}
		public class Implementation : IInterface {}
		public interface IDisposableInterface : IDisposable {}
		public class DisposableImplementation : IDisposableInterface
		{
			public void Dispose() {}
		}

		[TearDown]
		public void Cleanup()
		{
			MainInstaller.Clean();
		}

		[Test]
		public void Bind_Resolve_Successfully()
		{
			var implementation = new Implementation();
			MainInstaller.Bind<IInterface>(implementation);
			
			Assert.AreSame(implementation, MainInstaller.Resolve<IInterface>());
		}

		[Test]
		public void Clean_RemovesAllBindings()
		{
			MainInstaller.Bind<IInterface>(new Implementation());
			MainInstaller.Clean();
			
			Assert.IsFalse(MainInstaller.TryResolve<IInterface>(out _));
		}

		[Test]
		public void CleanGeneric_RemovesSpecificBinding()
		{
			MainInstaller.Bind<IInterface>(new Implementation());
			MainInstaller.Clean<IInterface>();
			
			Assert.IsFalse(MainInstaller.TryResolve<IInterface>(out _));
		}

		[Test]
		public void CleanDispose_CallsDispose()
		{
			var disposable = Substitute.For<IDisposableInterface>();
			MainInstaller.Bind(disposable);
			
			MainInstaller.CleanDispose<IDisposableInterface>();
			
			disposable.Received(1).Dispose();
			Assert.IsFalse(MainInstaller.TryResolve<IDisposableInterface>(out _));
		}

		[Test]
		public void TryResolve_NotBound_ReturnsFalse()
		{
			Assert.IsFalse(MainInstaller.TryResolve<IInterface>(out _));
		}

		[Test]
		public void SubsystemRegistrationReset_PreviousSessionBinding_IsClearedWithoutDispose()
		{
			// 도메인 리로드 없이 다시 Play 에 들어가면 Unity 가 이 훅만 다시 부른다. 그 상황을 리플렉션으로 재현한다
			var disposable = Substitute.For<IDisposableInterface>();
			MainInstaller.Bind(disposable);

			GetResetMethod().Invoke(null, null);

			Assert.IsFalse(MainInstaller.TryResolve<IDisposableInterface>(out _));
			disposable.DidNotReceive().Dispose();

			// 같은 타입을 새 인스턴스로 다시 바인딩해도 중복 예외가 나지 않고 새 인스턴스가 해석된다
			var next = Substitute.For<IDisposableInterface>();

			Assert.DoesNotThrow(() => MainInstaller.Bind<IDisposableInterface>(next));
			Assert.AreSame(next, MainInstaller.Resolve<IDisposableInterface>());
		}

		[Test]
		public void SubsystemRegistrationReset_IsRegisteredAtSubsystemRegistration()
		{
			var attribute = GetResetMethod().GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();

			Assert.IsNotNull(attribute);
			Assert.AreEqual(RuntimeInitializeLoadType.SubsystemRegistration, attribute.loadType);
		}

		private static MethodInfo GetResetMethod()
		{
			var method = typeof(MainInstaller).GetMethod("ResetOnSubsystemRegistration", BindingFlags.NonPublic | BindingFlags.Static);

			Assert.IsNotNull(method, "MainInstaller.ResetOnSubsystemRegistration 을 찾을 수 없습니다");

			return method;
		}
	}
}
