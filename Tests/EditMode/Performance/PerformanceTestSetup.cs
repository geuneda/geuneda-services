using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.PerformanceTesting.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

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
		// Mirrors Unity.PerformanceTesting.Runtime.Utils.PlayerPrefKeyRunJSON / PlayerPrefKeySettingsJSON.
		// Both keys are reproduced here because Utils is `internal` to com.unity.test-framework.performance.
		private const string PlayerPrefKeyRunJSON = "PT_Run";
		private const string PlayerPrefKeySettingsJSON = "PT_Settings";

		// Default RunSettings JSON. MeasurementCount = -1 is the package's "no override" sentinel —
		// MethodMeasurement.SettingsOverride() early-returns when count < 0, preserving the per-test
		// .WarmupCount(...) / .MeasurementCount(...) configuration.
		private const string DefaultRunSettingsJson = "{\"MeasurementCount\":-1}";

		public void Setup()
		{
			InitializePerformanceTestMetadata();
		}

		/// <summary>
		/// 성능 테스트 메타데이터를 초기화합니다. 테스트가 실행되기 전에 메타데이터가 준비되도록
		/// 테스트 픽스처의 [OneTimeSetUp]에서 호출하세요.
		/// </summary>
		/// <remarks>
		/// EditMode에서 `Measure.Method(...).Run()` 이 성공하려면 두 개의 PlayerPrefs 항목이 필요합니다:
		///   - PT_Run      — 전체 Run 메타데이터(에디터 정보, 의존성, 빌드 설정)이며, 결과가 방출될 때
		///                   Metadata.SetRuntimeSettings()에서 사용됩니다.
		///   - PT_Settings — RunSettings(측정 횟수 오버라이드)이며, 첫 워밍업이 실행되기 *전에*
		///                   MethodMeasurement.SettingsOverride()에서 사용됩니다.
		/// PT_Settings를 생략하면 RunSettings.Instance가 빈 JSON 문자열에서 지연 로드되고,
		/// JsonUtility가 예외를 던지며, ResourcesLoader가 그 예외를 삼키고 null을 반환하여,
		/// SettingsOverride()가 `RunSettings.Instance.MeasurementCount`에서 NullReferenceException을 일으킵니다.
		/// </remarks>
		public static void InitializePerformanceTestMetadata()
		{
			var run = CreateRunInfo();
			SaveToPrefs(run, PlayerPrefKeyRunJSON);

			PlayerPrefs.SetString(PlayerPrefKeySettingsJSON, DefaultRunSettingsJson);

			PlayerPrefs.Save();

			Debug.Log("[PerformanceTestSetup] Performance test metadata initialized.");
		}

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
	}
}
