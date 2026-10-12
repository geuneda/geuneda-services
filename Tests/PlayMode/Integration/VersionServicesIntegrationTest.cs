using System.Collections;
using System.Reflection;
using Geuneda.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	/// <summary>
	/// <see cref="VersionServices"/> 의 비동기 리소스 로드 파이프라인과 로드 이후의
	/// 모든 속성 접근자를 검증하는 통합 테스트입니다.
	/// 프로젝트에 Assets/Configs/Resources/version-data.txt 가 있어야 합니다.
	/// </summary>
	public class VersionServicesIntegrationTest
	{
		private static readonly FieldInfo LoadedField =
			typeof(VersionServices).GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static);
		private static readonly FieldInfo LoadAttemptedField =
			typeof(VersionServices).GetField("_loadAttempted", BindingFlags.NonPublic | BindingFlags.Static);

		[SetUp]
		public void ResetStaticState()
		{
			// Play 모드 진입 시 Bootstrap 이 이미 로드를 시도했으므로 시도 여부도 함께 초기화합니다
			LoadedField.SetValue(null, false);
			LoadAttemptedField.SetValue(null, false);
		}

		[UnityTest, Order(1)]
		public IEnumerator AccessBeforeLoad_AutoLoads()
		{
			Assert.IsFalse((bool)LoadedField.GetValue(null), "Precondition: SetUp resets _loaded to false");

			Assert.DoesNotThrow(() => { var _ = VersionServices.VersionInternal; });
			Assert.DoesNotThrow(() => { var _ = VersionServices.Branch; });
			Assert.DoesNotThrow(() => { var _ = VersionServices.Commit; });
			Assert.DoesNotThrow(() => { var _ = VersionServices.BuildNumber; });

			Assert.IsTrue((bool)LoadedField.GetValue(null), "Accessor should auto-trigger LoadVersionData via EnsureLoaded");

			yield return null;
		}

		[UnityTest, Order(2)]
		public IEnumerator LoadVersionDataAsync_Successfully()
		{
			var task = VersionServices.LoadVersionDataAsync();

			while (!task.IsCompleted)
			{
				yield return null;
			}

			Assert.IsTrue((bool)LoadedField.GetValue(null), "Version data should be loaded");
		}

		[UnityTest, Order(3)]
		public IEnumerator AfterLoad_VersionInternal_ContainsExpectedParts()
		{
			var task = VersionServices.LoadVersionDataAsync();
			while (!task.IsCompleted) yield return null;

			var version = VersionServices.VersionInternal;

			Assert.IsNotNull(version);
			Assert.IsNotEmpty(version);
			Assert.IsTrue(version.Contains("."), "VersionInternal should contain version separators");
		}

		[UnityTest, Order(4)]
		public IEnumerator AfterLoad_Branch_ReturnsNonEmptyString()
		{
			var expected = ReadResourceVersionData();

			if (string.IsNullOrEmpty(expected.BranchName))
			{
				Assert.Inconclusive("version-data 의 BranchName 이 비어 있습니다. git 저장소가 아닌 프로젝트에서는 " +
					"VersionEditorUtils 가 브랜치를 기록하지 않으므로 이 테스트의 전제가 성립하지 않습니다.");
			}

			var task = VersionServices.LoadVersionDataAsync();
			while (!task.IsCompleted) yield return null;

			var branch = VersionServices.Branch;

			Assert.IsNotNull(branch);
			Assert.IsNotEmpty(branch);
			Assert.AreEqual(expected.BranchName, branch);
		}

		[UnityTest, Order(5)]
		public IEnumerator AfterLoad_Commit_ReturnsNonEmptyString()
		{
			var expected = ReadResourceVersionData();

			if (string.IsNullOrEmpty(expected.CommitHash))
			{
				Assert.Inconclusive("version-data 의 CommitHash 가 비어 있습니다. git 저장소가 아닌 프로젝트에서는 " +
					"VersionEditorUtils 가 커밋을 기록하지 않으므로 이 테스트의 전제가 성립하지 않습니다.");
			}

			var task = VersionServices.LoadVersionDataAsync();
			while (!task.IsCompleted) yield return null;

			var commit = VersionServices.Commit;

			Assert.IsNotNull(commit);
			Assert.IsNotEmpty(commit);
			Assert.AreEqual(expected.CommitHash, commit);
		}

		[UnityTest, Order(6)]
		public IEnumerator AfterLoad_BuildNumber_ReturnsNonEmptyString()
		{
			var task = VersionServices.LoadVersionDataAsync();
			while (!task.IsCompleted) yield return null;

			var buildNumber = VersionServices.BuildNumber;

			Assert.IsNotNull(buildNumber);
			Assert.IsNotEmpty(buildNumber);
		}

		[UnityTest, Order(7)]
		public IEnumerator VersionExternal_AlwaysAccessible_WithoutLoad()
		{
			var external = VersionServices.VersionExternal;

			Assert.IsNotNull(external);
			Assert.IsNotEmpty(external);

			yield return null;
		}

		/// <summary>
		/// <see cref="VersionServices"/> 를 거치지 않고 version-data 리소스를 직접 읽습니다.
		/// 리소스가 없으면 Inconclusive 로 끝냅니다.
		/// </summary>
		private static VersionServices.VersionData ReadResourceVersionData()
		{
			var textAsset = Resources.Load<TextAsset>(VersionServices.VersionDataFilename);

			if (!textAsset)
			{
				Assert.Inconclusive($"Resources 에 '{VersionServices.VersionDataFilename}' 리소스가 없습니다. " +
					"VersionEditorUtils 가 도메인 리로드 때 생성합니다.");
			}

			var data = JsonUtility.FromJson<VersionServices.VersionData>(textAsset.text);

			Resources.UnloadAsset(textAsset);

			return data;
		}
	}
}
