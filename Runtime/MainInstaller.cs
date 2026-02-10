using System;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <inheritdoc cref="IInstaller"/>
	/// <remarks>
	/// 게임 전체 범위에서 사용 가능한 범용 바인딩 인터페이스에 이 인스톨러를 사용하세요
	/// </remarks>
	public static class MainInstaller
	{
		private static readonly IInstaller _installer = new Installer();

		/// <inheritdoc cref="IInstaller.Bind{T}"/>
		public static void Bind<T>(T instance) where T : class
		{
			_installer.Bind(instance);
		}

		/// <inheritdoc cref="IInstaller.TryResolve{T}"/>
		public static bool TryResolve<T>(out T instance)
		{
			return _installer.TryResolve(out instance);
		}

		/// <inheritdoc cref="IInstaller.Resolve{T}"/>
		public static T Resolve<T>()
		{
			return _installer.Resolve<T>();
		}

		/// <inheritdoc cref="IInstaller.Clean"/>
		public static bool Clean<T>() where T : class
		{
			return _installer.Clean<T>();
		}

		/// <inheritdoc cref="IInstaller.Clean"/>
		public static bool CleanDispose<T>() where T : class, IDisposable
		{
			_installer.Resolve<T>().Dispose();

			return _installer.Clean<T>();
		}

		/// <inheritdoc cref="IInstaller.Clean"/>
		public static void Clean()
		{
			_installer.Clean();
		}
	}
}
