using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Geuneda.Services.Versioning.Editor
{
	/// <summary>
	/// 플레이어 빌드 직전에 빌드 대상 플랫폼의 빌드 번호로 내부 버전을 기록하고, 빌드가 끝나면 에디터 실행용 값으로 되돌립니다.
	/// </summary>
	/// <remarks>
	/// 빌드가 성공하면 후처리에서, 실패하거나 취소되어 후처리가 불리지 않으면 빌드가 끝난 뒤 첫 에디터 업데이트에서
	/// 활성 빌드 타깃의 빌드 번호로 되돌립니다. 환경(dev/qa/prod)은 기록하지 않고 런타임에 <c>Geuneda.Core.AppEnv</c> 로 정해집니다.
	/// </remarks>
	internal sealed class VersionBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
	{
		// 빌드 후 복원을 기다리는 중인지. 실패·취소된 빌드는 후처리가 불리지 않으므로 에디터 업데이트에서 복원한다
		private static bool _restorePending;

		public int callbackOrder => 0;

		public void OnPreprocessBuild(BuildReport report)
		{
			PrepareForBuild(report.summary.platform);
		}

		public void OnPostprocessBuild(BuildReport report)
		{
			RestoreEditorVersion();
		}

		/// <summary>
		/// 빌드용 내부 버전을 기록하고, 빌드가 끝나면 에디터 값으로 되돌리도록 예약합니다.
		/// </summary>
		internal static void PrepareForBuild(BuildTarget buildTarget)
		{
			VersionEditorUtils.SetAndSaveInternalVersion(buildTarget);

			if (!_restorePending)
			{
				_restorePending = true;
				EditorApplication.update += RestoreWhenBuildEnds;
			}
		}

		/// <summary>
		/// 복원 예약을 풀고 에디터 실행용 내부 버전(활성 빌드 타깃의 빌드 번호)을 다시 기록합니다.
		/// </summary>
		internal static void RestoreEditorVersion()
		{
			if (_restorePending)
			{
				_restorePending = false;
				EditorApplication.update -= RestoreWhenBuildEnds;
			}

			VersionEditorUtils.SetAndSaveInternalVersion();
		}

		private static void RestoreWhenBuildEnds()
		{
			// 빌드 도중에 불리는 업데이트에서는 되돌리지 않는다
			if (BuildPipeline.isBuildingPlayer)
			{
				return;
			}

			RestoreEditorVersion();
		}
	}
}
