using Geuneda.Services.Editor.Explorer;
using Geuneda.Services.Editor.Explorer.Tabs;
using UnityEditor;

namespace Geuneda.Services.Versioning.Editor
{
	/// <summary>
	/// <c>Tools &gt; Geuneda &gt; Versioning</c> 아래의 버전 관리 파이프라인 상단 메뉴 스텁입니다.
	/// 설정은 Services Explorer 의 Versioning 탭에 있습니다.
	/// </summary>
	internal static class VersioningMenu
	{
		/// <summary>
		/// 현재 git 상태로 <c>version-data.txt</c> 를 다시 생성합니다(스토어 빌드 아님).
		/// 도메인 리로드 때의 동작과 같으며, 전체 리로드 없이 브랜치를 바꾼 뒤에 유용합니다.
		/// </summary>
		[MenuItem("Tools/Geuneda/Versioning/Refresh Version Data", priority = 100)]
		private static void Refresh() => VersionEditorUtils.SetAndSaveInternalVersion(false);

		[MenuItem("Tools/Geuneda/Versioning/Open in Explorer", priority = 200)]
		private static void Open() => ServicesExplorerWindow.OpenOnTab<VersioningTab>();
	}
}
