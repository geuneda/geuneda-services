using System;
using System.Threading.Tasks;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <summary>
	/// 애플리케이션 버전을 관리하는 서비스
	/// </summary>
	/// <remarks>
	/// 버전 비교 및 버전 확인에 활용할 수 있습니다
	/// </remarks>
	public static class VersionServices
	{
		public const string VersionDataFilename = "version-data";

		[Serializable]
		public struct VersionData
		{
			public string CommitHash;
			public string BranchName;
			public string BuildType;
			public string BuildNumber;
		}

		/// <summary>
		/// 공식 애플리케이션 버전 (M.m.p)
		/// </summary>
		public static string VersionExternal => Application.version;

		/// <summary>
		/// 내부 버전 (M.m.p-b.branch.commit). <see cref="Bootstrap"/> 훅이 아직 실행되지 않았다면
		/// 최초 접근 시 지연 로드됩니다(<see cref="Bootstrap"/>의 remarks 참고). <c>version-data</c>
		/// 리소스가 없거나 파싱에 실패하면 <see cref="Application.version"/>으로 폴백합니다.
		/// </summary>
		public static string VersionInternal
		{
			get
			{
				EnsureLoaded();
				return _loaded ? FormatInternalVersion(_versionData) : Application.version;
			}
		}

		/// <summary>
		/// 이 앱이 빌드된 git 브랜치 이름. 최초 접근 시 지연 로드됩니다. <c>version-data</c>
		/// 리소스가 없으면 <see cref="string.Empty"/>를 반환합니다.
		/// </summary>
		public static string Branch
		{
			get
			{
				EnsureLoaded();
				return _loaded ? _versionData.BranchName : string.Empty;
			}
		}

		/// <summary>
		/// 이 앱이 빌드된 커밋의 짧은 해시. 최초 접근 시 지연 로드됩니다. <c>version-data</c>
		/// 리소스가 없으면 <see cref="string.Empty"/>를 반환합니다.
		/// </summary>
		public static string Commit
		{
			get
			{
				EnsureLoaded();
				return _loaded ? _versionData.CommitHash : string.Empty;
			}
		}

		/// <summary>
		/// 이 앱 빌드의 빌드 번호. 최초 접근 시 지연 로드됩니다. <c>version-data</c>
		/// 리소스가 없으면 <see cref="string.Empty"/>를 반환합니다.
		/// </summary>
		public static string BuildNumber
		{
			get
			{
				EnsureLoaded();
				return _loaded ? _versionData.BuildNumber : string.Empty;
			}
		}

		private static VersionData _versionData;
		private static bool _loaded;

		/// <summary>
		/// 자동 부트스트랩 훅: Unity가 노출하는 가장 이른 런타임 단계에서 버전 메타데이터를 채웁니다.
		/// 모든 씬 <c>Awake</c>보다 먼저, 그리고 <see cref="VersionInternal"/> / <see cref="BuildNumber"/>를
		/// 읽는 벤더 SDK의 <c>SubsystemRegistration</c> 콜백(예: Sentry의 Option Config Script)보다 먼저
		/// 실행됩니다. 이제 기본 흐름에서는 사용처가 <see cref="LoadVersionData"/> /
		/// <see cref="LoadVersionDataAsync"/>를 명시적으로 호출할 필요가 없습니다.
		/// </summary>
		/// <remarks>
		/// 어셈블리 간 <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/> 콜백 사이의 순서는
		/// 정의되어 있지 않습니다. 형제 SDK의 훅이 이 훅보다 먼저 실행되더라도, 프로퍼티 접근자의
		/// 지연 로드 폴백(<see cref="EnsureLoaded"/>)이 이 경쟁 상태를 처리합니다.
		/// </remarks>
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Bootstrap()
		{
			LoadVersionData();
		}

		/// <summary>
		/// 리소스에서 내부 버전 문자열을 동기적으로 로드합니다. <see cref="Bootstrap"/>이
		/// <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/> 시점에 자동으로 호출하며,
		/// 명시적인 사전 예열(pre-warming)을 위해 직접 호출해도 안전합니다. 멱등(idempotent)하며,
		/// 버전 데이터가 이미 로드된 경우 <see cref="EnsureLoaded"/> 호출부를 통해 조기 반환합니다.
		/// 작은 페이로드(기본 <c>version-data.txt</c>는 수백 바이트)를 대상으로 하며,
		/// <see cref="VersionData"/>가 메인 스레드를 눈에 띄게 멈추게 할 만큼 큰 blob으로 확장된
		/// 경우에는 <see cref="LoadVersionDataAsync"/>를 사용하세요.
		/// </summary>
		public static void LoadVersionData()
		{
			try
			{
				var textAsset = Resources.Load<TextAsset>(VersionDataFilename);

				ApplyTextAsset(textAsset, asyncContext: false);
			}
			catch (Exception e)
			{
				Debug.LogError($"Error loading version data: {e.Message}");
				_loaded = false;
			}
		}

		/// <summary>
		/// 리소스에서 내부 버전 문자열을 비동기로 로드합니다. 동기 버전인
		/// <see cref="LoadVersionData"/>가 <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/>
		/// 시점에 자동으로 호출되므로, 이 비동기 변형은 메인 스레드 밖에서 명시적으로 사전 예열하거나
		/// <see cref="VersionData"/>가 메인 스레드를 눈에 띄게 멈추게 할 만큼 큰 blob으로 확장된 경우에만
		/// 필요합니다.
		/// </summary>
		public static async Task LoadVersionDataAsync()
		{
			try
			{
				var source = new TaskCompletionSource<TextAsset>();
				var request = Resources.LoadAsync<TextAsset>(VersionDataFilename);

				request.completed += _ => source.SetResult(request.asset as TextAsset);

				var textAsset = await source.Task;

				ApplyTextAsset(textAsset, asyncContext: true);
			}
			catch (Exception e)
			{
				Debug.LogError($"Error loading version data: {e.Message}");
				_loaded = false;
			}
		}

		private static void ApplyTextAsset(TextAsset textAsset, bool asyncContext)
		{
			if (!textAsset)
			{
				Debug.LogError($"Could not {(asyncContext ? "async " : string.Empty)}load version data from Resources.");
				_loaded = false;
				return;
			}

			_versionData = JsonUtility.FromJson<VersionData>(textAsset.text);
			_loaded = true;

			Resources.UnloadAsset(textAsset);
		}

		/// <summary>
		/// 제공된 버전이 로컬 앱 버전보다 최신인지 확인합니다
		/// </summary>
		public static bool IsOutdatedVersion(string version)
		{
			var appVersion = VersionExternal.Split('.');
			var otherVersion = version.Split('.');

			var majorApp = int.Parse(appVersion[0]);
			var majorOther = int.Parse(otherVersion[0]);

			var minorApp = int.Parse(appVersion[1]);
			var minorOther = int.Parse(otherVersion[1]);

			var patchApp = int.Parse(appVersion[2]);
			var patchOther = int.Parse(otherVersion[2]);

			if (majorApp != majorOther)
			{
				return majorOther > majorApp;
			}

			if (minorApp != minorOther)
			{
				return minorOther > minorApp;
			}

			return patchOther > patchApp;
		}

		/// <summary>
		/// VersionData를 앱의 전체 내부 버전 문자열로 포맷합니다.
		/// </summary>
		public static string FormatInternalVersion(VersionData data)
		{
			var version = $"{Application.version}-{data.BuildNumber}.{data.BranchName}.{data.CommitHash}";

			if (!string.IsNullOrEmpty(data.BuildType))
			{
				version += $".{data.BuildType}";
			}

			return version;
		}

		private static void EnsureLoaded()
		{
			if (_loaded) return;

			LoadVersionData();
		}
	}
}
