using System;
using Geuneda.Services;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	public class InstallerTest
	{
		private interface IInterface {}
		private interface IInterface2 {}
		private interface IInterface3 {}
		private class Implementation : IInterface {}
		private class MultiImpl : IInterface, IInterface2 {}
		private class TripleImpl : IInterface, IInterface2, IInterface3 {}

		private Installer _installer;
		
		[SetUp]
		public void Init()
		{
			_installer = new Installer();
		}

		[Test]
		public void Bind_Resolve_Successfully()
		{
			_installer.Bind<IInterface>(new Implementation());
			
			var instance = _installer.Resolve<IInterface>();
			
			Assert.IsNotNull(instance);
			Assert.AreSame(typeof(Implementation), instance.GetType());
		}

		[Test]
		public void Bind_NotInterface_ThrowsException()
		{
			Assert.Throws<ArgumentException>(() => _installer.Bind(new Implementation()));
		}

		[Test]
		public void Resolve_NotBinded_ThrowsException()
		{
			Assert.Throws<ArgumentException>(() => _installer.Resolve<IInterface>());
		}

		[Test]
		public void Bind_MultiInterface_ResolveBothInterfaces()
		{
			var instance = new MultiImpl();
			_installer.Bind<MultiImpl, IInterface, IInterface2>(instance);

			Assert.AreSame(instance, _installer.Resolve<IInterface>());
			Assert.AreSame(instance, _installer.Resolve<IInterface2>());
		}

		[Test]
		public void Bind_TripleInterface_ResolveAllInterfaces()
		{
			var instance = new TripleImpl();
			_installer.Bind<TripleImpl, IInterface, IInterface2, IInterface3>(instance);

			Assert.AreSame(instance, _installer.Resolve<IInterface>());
			Assert.AreSame(instance, _installer.Resolve<IInterface2>());
			Assert.AreSame(instance, _installer.Resolve<IInterface3>());
		}

		[Test]
		public void TryResolve_DirectInvocation_OutsValueWhenBound()
		{
			var instance = new Implementation();
			_installer.Bind<IInterface>(instance);

			var resolved = _installer.TryResolve<IInterface>(out var bound);
			var notFound = _installer.TryResolve<IInterface2>(out var unbound);

			Assert.IsTrue(resolved);
			Assert.AreSame(instance, bound);
			Assert.IsFalse(notFound);
			Assert.IsNull(unbound);
		}

		[Test]
		public void Clean_Generic_RemovesOnlyBoundInterface()
		{
			var first = new Implementation();
			var second = new MultiImpl();
			_installer.Bind<IInterface>(first);
			_installer.Bind<IInterface2>(second);

			_installer.Clean<IInterface>();

			Assert.Throws<ArgumentException>(() => _installer.Resolve<IInterface>());
			Assert.AreSame(second, _installer.Resolve<IInterface2>());
		}
	}
}