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
	/// Prebuild setup for performance tests.
	/// This ensures the Unity Performance Testing Package has the required metadata
	/// before tests run in EditMode.
	/// </summary>
	public class PerformanceTestSetup : IPrebuildSetup
	{
		private const string PlayerPrefKeyRunJSON = "PT_Run";

		public void Setup()
		{
#if UNITY_EDITOR
			// Create and save run info to PlayerPrefs (required by Performance Testing Package in EditMode)
			// Note: RunSettings is internal to the package, but only Run metadata is required to prevent
			// the NullReferenceException in Metadata.SetRuntimeSettings()
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
