using System.Reflection;
using Geuneda.Services;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	/// <summary>
	/// EditMode unit coverage for <see cref="VersionServices.LoadVersionData"/> (synchronous).
	/// The async sibling is covered in PlayMode/Integration/VersionServicesIntegrationTest.cs;
	/// the sync path needs no Unity runtime, so it lives here. Requires
	/// Assets/Configs/Resources/version-data.txt to exist in the host project (written
	/// automatically by VersionEditorUtils on every domain reload).
	/// </summary>
	/// <remarks>
	/// Branch / Commit 은 프로젝트가 git 저장소일 때만 version-data 에 채워집니다. git 저장소가 아닌
	/// 프로젝트에서는 해당 테스트가 리소스 값을 확인한 뒤 Inconclusive 로 끝납니다.
	/// </remarks>
	[TestFixture]
	public class VersionServicesSyncLoadTest
	{
		private static readonly FieldInfo LoadedField =
			typeof(VersionServices).GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static);
		private static readonly FieldInfo LoadAttemptedField =
			typeof(VersionServices).GetField("_loadAttempted", BindingFlags.NonPublic | BindingFlags.Static);

		[SetUp]
		public void ResetStaticState()
		{
			LoadedField.SetValue(null, false);
			LoadAttemptedField.SetValue(null, false);
		}

		[Test]
		public void AccessBeforeLoad_AutoLoads()
		{
			Assert.IsFalse((bool)LoadedField.GetValue(null), "Precondition: SetUp resets _loaded to false");

			Assert.DoesNotThrow(() => { var _ = VersionServices.VersionInternal; });
			Assert.DoesNotThrow(() => { var _ = VersionServices.Branch; });
			Assert.DoesNotThrow(() => { var _ = VersionServices.Commit; });
			Assert.DoesNotThrow(() => { var _ = VersionServices.BuildNumber; });

			Assert.IsTrue((bool)LoadedField.GetValue(null), "Accessor should auto-trigger LoadVersionData via EnsureLoaded");
		}

		[Test]
		public void Accessor_AfterLoadAttempt_DoesNotReload()
		{
			// 리소스 로드에 실패한 직후 상태를 흉내 냅니다: 시도는 했지만 로드되지 않음
			LoadAttemptedField.SetValue(null, true);

			Assert.AreEqual(string.Empty, VersionServices.Branch);
			Assert.AreEqual(string.Empty, VersionServices.Commit);
			Assert.IsFalse((bool)LoadedField.GetValue(null), "EnsureLoaded should not retry after a load attempt");
		}

		[Test]
		public void LoadVersionData_Successfully_FlipsLoadedFlag()
		{
			VersionServices.LoadVersionData();

			Assert.IsTrue((bool)LoadedField.GetValue(null), "Version data should be loaded after sync call");
		}

		[Test]
		public void LoadVersionData_DoesNotThrow()
		{
			Assert.DoesNotThrow(() => VersionServices.LoadVersionData());
		}

		[Test]
		public void AfterLoad_VersionInternal_ContainsExpectedParts()
		{
			VersionServices.LoadVersionData();

			var version = VersionServices.VersionInternal;

			Assert.IsNotNull(version);
			Assert.IsNotEmpty(version);
			Assert.IsTrue(version.Contains("."), "VersionInternal should contain version separators");
		}

		[Test]
		public void AfterLoad_Branch_ReturnsNonEmptyString()
		{
			var expected = ReadResourceVersionData();

			if (string.IsNullOrEmpty(expected.BranchName))
			{
				Assert.Inconclusive("version-data 의 BranchName 이 비어 있습니다. git 저장소가 아닌 프로젝트에서는 " +
					"VersionEditorUtils 가 브랜치를 기록하지 않으므로 이 테스트의 전제가 성립하지 않습니다.");
			}

			VersionServices.LoadVersionData();

			var branch = VersionServices.Branch;

			Assert.IsNotNull(branch);
			Assert.IsNotEmpty(branch);
			Assert.AreEqual(expected.BranchName, branch);
		}

		[Test]
		public void AfterLoad_Commit_ReturnsNonEmptyString()
		{
			var expected = ReadResourceVersionData();

			if (string.IsNullOrEmpty(expected.CommitHash))
			{
				Assert.Inconclusive("version-data 의 CommitHash 가 비어 있습니다. git 저장소가 아닌 프로젝트에서는 " +
					"VersionEditorUtils 가 커밋을 기록하지 않으므로 이 테스트의 전제가 성립하지 않습니다.");
			}

			VersionServices.LoadVersionData();

			var commit = VersionServices.Commit;

			Assert.IsNotNull(commit);
			Assert.IsNotEmpty(commit);
			Assert.AreEqual(expected.CommitHash, commit);
		}

		[Test]
		public void AfterLoad_BuildNumber_ReturnsNonEmptyString()
		{
			VersionServices.LoadVersionData();

			var buildNumber = VersionServices.BuildNumber;

			Assert.IsNotNull(buildNumber);
			Assert.IsNotEmpty(buildNumber);
		}

		[Test]
		public void VersionExternal_AlwaysAccessible_WithoutLoad()
		{
			var external = VersionServices.VersionExternal;

			Assert.IsNotNull(external);
			Assert.IsNotEmpty(external);
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
