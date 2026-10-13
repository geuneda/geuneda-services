using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Geuneda.Services.Versioning.Editor
{
	/// <summary>
	/// 플레이어 빌드 직전에 빌드용 내부 버전을 기록하고, 빌드가 끝나면 에디터 실행용 값으로 되돌립니다.
	/// </summary>
	/// <remarks>
	/// 개발 빌드(<see cref="BuildOptions.Development"/>)가 아니면 스토어 빌드로 보고 빌드 타입을 <c>prod</c> 로 기록합니다.
	/// 빌드 번호는 빌드 대상 플랫폼 기준입니다. 빌드가 성공하면 후처리에서, 실패하거나 취소되어 후처리가 불리지 않으면
	/// 빌드가 끝난 뒤 첫 에디터 업데이트에서 에디터 값(<c>dev</c>)으로 되돌립니다.
	/// </remarks>
	internal sealed class VersionBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
	{
		// 빌드 후 복원을 기다리는 중인지. 실패·취소된 빌드는 후처리가 불리지 않으므로 에디터 업데이트에서 복원한다
		private static bool _restorePending;

		public int callbackOrder => 0;

		public void OnPreprocessBuild(BuildReport report)
		{
			PrepareForBuild(report.summary.options, report.summary.platform);
		}

		public void OnPostprocessBuild(BuildReport report)
		{
			RestoreEditorVersion();
		}

		/// <summary>
		/// 개발 빌드가 아니면 스토어 빌드입니다.
		/// </summary>
		internal static bool IsStoreBuild(BuildOptions options)
		{
			return (options & BuildOptions.Development) == 0;
		}

		/// <summary>
		/// 빌드용 내부 버전을 기록하고, 빌드가 끝나면 에디터 값으로 되돌리도록 예약합니다.
		/// </summary>
		internal static void PrepareForBuild(BuildOptions options, BuildTarget buildTarget)
		{
			VersionEditorUtils.SetAndSaveInternalVersion(IsStoreBuild(options), buildTarget);

			if (!_restorePending)
			{
				_restorePending = true;
				EditorApplication.update += RestoreWhenBuildEnds;
			}
		}

		/// <summary>
		/// 복원 예약을 풀고 에디터 실행용 내부 버전(<c>dev</c>, 활성 빌드 타깃)을 다시 기록합니다.
		/// </summary>
		internal static void RestoreEditorVersion()
		{
			if (_restorePending)
			{
				_restorePending = false;
				EditorApplication.update -= RestoreWhenBuildEnds;
			}

			VersionEditorUtils.SetAndSaveInternalVersion(false);
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
