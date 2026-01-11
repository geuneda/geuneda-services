using System;
using GameLovers.Services;
using NSubstitute;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
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
	}
}
