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
	/// 빌드 시 기록하는 내부 버전 값(빌드 타입, 플랫폼별 빌드 번호)과 빌드 전후 기록·복원을 검증합니다.
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
		public void IsStoreBuild_ReleaseBuild_IsTrue()
		{
			Assert.IsTrue(VersionBuildProcessor.IsStoreBuild(BuildOptions.None));
		}

		[Test]
		public void IsStoreBuild_DevelopmentBuild_IsFalse()
		{
			Assert.IsFalse(VersionBuildProcessor.IsStoreBuild(BuildOptions.Development));
			Assert.IsFalse(VersionBuildProcessor.IsStoreBuild(BuildOptions.Development | BuildOptions.AllowDebugging));
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
		public void PrepareForBuild_ReleaseAndroid_WritesProdAndAndroidBuildNumberThenRestoreWritesDev()
		{
			VersionBuildProcessor.PrepareForBuild(BuildOptions.None, BuildTarget.Android);

			var built = ReadVersionData();

			Assert.AreEqual("prod", built.BuildType);
			Assert.AreEqual(VersionEditorUtils.GetBuildNumber(BuildTarget.Android), built.BuildNumber);

			VersionBuildProcessor.RestoreEditorVersion();

			var restored = ReadVersionData();

			Assert.AreEqual("dev", restored.BuildType);
			Assert.AreEqual(VersionEditorUtils.GetBuildNumber(EditorUserBuildSettings.activeBuildTarget), restored.BuildNumber);
		}

		[Test]
		public void PrepareForBuild_DevelopmentBuild_WritesDev()
		{
			VersionBuildProcessor.PrepareForBuild(BuildOptions.Development, BuildTarget.iOS);

			var built = ReadVersionData();

			Assert.AreEqual("dev", built.BuildType);
			Assert.AreEqual(VersionEditorUtils.GetBuildNumber(BuildTarget.iOS), built.BuildNumber);
		}

		[UnityTest]
		public IEnumerator PrepareForBuild_NoPostprocess_RestoresDevOnNextEditorUpdate()
		{
			// 빌드가 실패하거나 취소되면 후처리가 불리지 않는다. 빌드 중이 아닌 에디터 업데이트에서 되돌아와야 한다
			VersionBuildProcessor.PrepareForBuild(BuildOptions.None, BuildTarget.Android);

			Assert.AreEqual("prod", ReadVersionData().BuildType);

			for (var i = 0; i < 10 && ReadVersionData().BuildType != "dev"; i++)
			{
				yield return null;
			}

			Assert.AreEqual("dev", ReadVersionData().BuildType);
		}

		private static VersionServices.VersionData ReadVersionData()
		{
			return JsonUtility.FromJson<VersionServices.VersionData>(VersionEditorUtils.LoadVersionDataSerializedSync());
		}
	}
}
