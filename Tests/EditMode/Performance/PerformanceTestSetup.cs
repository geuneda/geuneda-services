using UnityEngine;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
using Unity.PerformanceTesting.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
#endif

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	/// <summary>
	/// 성능 테스트를 위한 사전 빌드 설정입니다.
	/// EditMode에서 테스트가 실행되기 전에 Unity Performance Testing Package에
	/// 필요한 메타데이터가 있는지 확인합니다.
	/// </summary>
	public class PerformanceTestSetup : IPrebuildSetup
	{
		private const string PlayerPrefKeyRunJSON = "PT_Run";

		public void Setup()
		{
#if UNITY_EDITOR
			// PlayerPrefs에 실행 정보를 생성하고 저장합니다 (EditMode에서 Performance Testing Package에 필요)
			// 참고: RunSettings는 패키지 내부이지만, Metadata.SetRuntimeSettings()에서
			// NullReferenceException을 방지하기 위해 Run 메타데이터만 필요합니다
			var run = CreateRunInfo();
			SaveToPrefs(run, PlayerPrefKeyRunJSON);

			Debug.Log("[PerformanceTestSetup] Performance test metadata initialized.");
#endif
		}

#if UNITY_EDITOR
		private static Run CreateRunInfo()
		{
			var run = new Run
			{
				Editor = GetEditorInfo(),
				Dependencies = GetPackageDependencies(),
				Date = ConvertToUnixTimestamp(DateTime.Now),
				Player = new Player()
			};

			SetBuildSettings(run);
			return run;
		}

		private static Unity.PerformanceTesting.Data.Editor GetEditorInfo()
		{
			var fullVersion = UnityEditorInternal.InternalEditorUtility.GetFullUnityVersion();
			const string pattern = @"(.+\.+.+\.\w+)|((?<=\().+(?=\)))";
			var matches = Regex.Matches(fullVersion, pattern);

			return new Unity.PerformanceTesting.Data.Editor
			{
				Branch = GetEditorBranch(),
				Version = matches.Count > 0 ? matches[0].Value : "unknown",
				Changeset = matches.Count > 1 ? matches[1].Value : "unknown",
				Date = UnityEditorInternal.InternalEditorUtility.GetUnityVersionDate(),
			};
		}

		private static string GetEditorBranch()
		{
			foreach (var method in typeof(UnityEditorInternal.InternalEditorUtility).GetMethods())
			{
				if (method.Name.Contains("GetUnityBuildBranch"))
				{
					return (string)method.Invoke(null, null);
				}
			}
			return "null";
		}

		private static List<string> GetPackageDependencies()
		{
			var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
			return packages.Select(p => $"{p.name}@{p.version}").ToList();
		}

		private static void SetBuildSettings(Run run)
		{
			run.Player.GpuSkinning = PlayerSettings.gpuSkinning;
			run.Player.ScriptingBackend = PlayerSettings
				.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup))
				.ToString();
			run.Player.RenderThreadingMode = PlayerSettings.graphicsJobs
				? PlayerSettings.graphicsJobMode.ToString()
				: PlayerSettings.MTRendering ? "MultiThreaded" : "SingleThreaded";
			run.Player.AndroidTargetSdkVersion = PlayerSettings.Android.targetSdkVersion.ToString();
			run.Player.AndroidBuildSystem = EditorUserBuildSettings.androidBuildSystem.ToString();
			run.Player.BuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
			run.Player.StereoRenderingPath = PlayerSettings.stereoRenderingPath.ToString();
		}

		private static long ConvertToUnixTimestamp(DateTime date)
		{
			var origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
			var diff = date.ToUniversalTime() - origin;
			return (long)Math.Floor(diff.TotalSeconds);
		}

		private static void SaveToPrefs(object obj, string key)
		{
			var json = JsonUtility.ToJson(obj, true);
			PlayerPrefs.SetString(key, json);
		}
#endif
	}
}
