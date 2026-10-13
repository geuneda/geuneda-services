using System.Collections;
using System.Globalization;
using Geuneda.Services;
using Geuneda.Services.Versioning.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	/// <summary>
	/// 빌드 시 기록하는 내부 버전 값(플랫폼별 빌드 번호)과 빌드 전후 기록·복원을 검증합니다.
	/// </summary>
	[TestFixture]
	public class VersionBuildTest
	{
		[TearDown]
		public void TearDown()
		{
			// 테스트가 빌드용 값을 기록했더라도 에디터 값으로 돌려 둔다
			VersionBuildProcessor.RestoreEditorVersion();
		}

		[Test]
		public void GetBuildNumber_Android_UsesBundleVersionCode()
		{
			// 프로젝트 설정을 바꾸면 에셋이 다시 저장되므로 현재 값을 읽기만 한다
			var androidCode = PlayerSettings.Android.bundleVersionCode.ToString(CultureInfo.InvariantCulture);
			var iosNumber = PlayerSettings.iOS.buildNumber;

			Assume.That(androidCode, Is.Not.EqualTo(iosNumber), "Android 와 iOS 빌드 번호가 같으면 어느 쪽을 읽는지 구분할 수 없습니다");

			Assert.AreEqual(androidCode, VersionEditorUtils.GetBuildNumber(BuildTarget.Android));
			Assert.AreEqual(iosNumber, VersionEditorUtils.GetBuildNumber(BuildTarget.iOS));
		}

		[Test]
		public void SetAndSaveInternalVersion_DoesNotWriteBuildType()
		{
			VersionEditorUtils.SetAndSaveInternalVersion();

			// 환경은 런타임에 AppEnv 로 정해지므로 version-data 에 기록하지 않는다
			StringAssert.DoesNotContain("BuildType", VersionEditorUtils.LoadVersionDataSerializedSync());
		}

		[Test]
		public void PrepareForBuild_Android_WritesAndroidBuildNumberThenRestoreWritesActiveTarget()
		{
			VersionBuildProcessor.PrepareForBuild(BuildTarget.Android);

			Assert.AreEqual(VersionEditorUtils.GetBuildNumber(BuildTarget.Android), ReadVersionData().BuildNumber);

			VersionBuildProcessor.RestoreEditorVersion();

			Assert.AreEqual(VersionEditorUtils.GetBuildNumber(EditorUserBuildSettings.activeBuildTarget), ReadVersionData().BuildNumber);
		}

		[Test]
		public void PrepareForBuild_iOS_WritesIosBuildNumber()
		{
			VersionBuildProcessor.PrepareForBuild(BuildTarget.iOS);

			Assert.AreEqual(VersionEditorUtils.GetBuildNumber(BuildTarget.iOS), ReadVersionData().BuildNumber);
		}

		[UnityTest]
		public IEnumerator PrepareForBuild_NoPostprocess_RestoresOnNextEditorUpdate()
		{
			// 빌드가 실패하거나 취소되면 후처리가 불리지 않는다. 빌드 중이 아닌 에디터 업데이트에서 되돌아와야 한다
			var editorNumber = VersionEditorUtils.GetBuildNumber(EditorUserBuildSettings.activeBuildTarget);
			var buildTarget = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ? BuildTarget.iOS : BuildTarget.Android;
			var buildNumber = VersionEditorUtils.GetBuildNumber(buildTarget);

			Assume.That(buildNumber, Is.Not.EqualTo(editorNumber), "빌드 대상과 활성 타깃의 빌드 번호가 같으면 복원을 구분할 수 없습니다");

			VersionBuildProcessor.PrepareForBuild(buildTarget);

			Assert.AreEqual(buildNumber, ReadVersionData().BuildNumber);

			for (var i = 0; i < 10 && ReadVersionData().BuildNumber != editorNumber; i++)
			{
				yield return null;
			}

			Assert.AreEqual(editorNumber, ReadVersionData().BuildNumber);
		}

		private static VersionServices.VersionData ReadVersionData()
		{
			return JsonUtility.FromJson<VersionServices.VersionData>(VersionEditorUtils.LoadVersionDataSerializedSync());
		}
	}
}
