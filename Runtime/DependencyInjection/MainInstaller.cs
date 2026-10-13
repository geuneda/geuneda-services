using System;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <inheritdoc cref="IInstaller"/>
	/// <remarks>
	/// 게임 전체 범위에서 사용 가능한 범용 바인딩 인터페이스에 이 인스톨러를 사용하세요.
	/// 바인딩은 static 이라 도메인 리로드를 끈 Enter Play Mode 에서도 이전 플레이 세션의 바인딩이 남지 않도록
	/// <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/> 시점에 비웁니다. 그 시점에 바인딩하는 코드는 지워질 수 있으므로
	/// 바인딩은 <see cref="RuntimeInitializeLoadType.AfterAssembliesLoaded"/> 이후(씬 로드, <c>Awake</c> 등)에 하세요.
	/// </remarks>
	public static class MainInstaller
	{
		private static readonly Installer _installer = new Installer();

		internal static Installer InstallerInstance => _installer;

		/// <summary>
		/// 플레이 세션이 시작될 때 이전 세션의 바인딩을 비웁니다. 도메인 리로드가 꺼져 있으면 static 필드가 초기화되지 않아
		/// 이전 세션에서 바인딩한(이미 파괴된 오브젝트를 가진) 서비스가 남고, 같은 타입을 다시 <see cref="Bind{T}"/> 하면 예외가 납니다.
		/// 이전 세션의 객체는 정리되었을 수 있으므로 Dispose 하지 않고 참조만 버립니다.
		/// </summary>
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetOnSubsystemRegistration()
		{
			_installer.Clean();
		}

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
			if (_installer.TryResolve<T>(out var instance) && instance != null)
			{
				instance.Dispose();
			}

			return _installer.Clean<T>();
		}

		/// <inheritdoc cref="IInstaller.Clean"/>
		public static void Clean()
		{
			_installer.Clean();
		}
	}
}
