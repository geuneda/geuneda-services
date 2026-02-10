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
		/// 내부 버전 (M.m.p-b.branch.commit)
		/// </summary>
		public static string VersionInternal => IsLoaded()
			? FormatInternalVersion(_versionData)
			: Application.version;

		/// <summary>
		/// 이 앱이 빌드된 git 브랜치 이름
		/// </summary>
		public static string Branch => IsLoaded() ? _versionData.BranchName : string.Empty;

		/// <summary>
		/// 이 앱이 빌드된 커밋의 짧은 해시
		/// </summary>
		public static string Commit => IsLoaded() ? _versionData.CommitHash : string.Empty;

		/// <summary>
		/// 이 앱 빌드의 빌드 번호
		/// </summary>
		public static string BuildNumber => IsLoaded() ? _versionData.BuildNumber : string.Empty;

		private static VersionData _versionData;
		private static bool _loaded;

		/// <summary>
		/// 리소스에서 내부 버전 문자열을 비동기로 로드합니다.
		/// 앱 시작 시 한 번 호출해야 합니다.
		/// </summary>
		public static async Task LoadVersionDataAsync()
		{
			try
			{
				var source = new TaskCompletionSource<TextAsset>();
				var request = Resources.LoadAsync<TextAsset>(VersionDataFilename);

				request.completed += _ => source.SetResult(request.asset as TextAsset);

				var textAsset = await source.Task;

				if (!textAsset)
				{
					Debug.LogError("Could not async load version data from Resources.");
					_loaded = false;
					return;
				}

				_versionData = JsonUtility.FromJson<VersionData>(textAsset.text);
				_loaded = true;

				Resources.UnloadAsset(textAsset);
			}
			catch (Exception e)
			{
				Debug.LogError($"Error loading version data: {e.Message}");
				_loaded = false;
			}
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

		private static bool IsLoaded()
		{
			return _loaded ? true : throw new Exception("Version Data not loaded.");
		}
	}
}
