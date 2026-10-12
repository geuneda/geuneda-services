using Geuneda.Services.Editor.Explorer;
using Geuneda.Services.Editor.Explorer.Tabs;
using UnityEditor;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services.AssetsImporter.Editor
{
	/// <summary>
	/// <c>Tools &gt; Geuneda &gt; Assets Importer</c> 아래의 Assets Importer 파이프라인 상단 메뉴 스텁입니다.
	/// 설정과 임포터별 조작은 Services Explorer 의 Assets Importer 탭에 있습니다.
	/// </summary>
	internal static class AssetsImporterMenu
	{
		[MenuItem("Tools/Geuneda/Assets Importer/Import Assets Data", priority = 100)]
		private static void ImportAll() => AssetsImporterEditorUtils.ImportAll();

		[MenuItem("Tools/Geuneda/Assets Importer/Open in Explorer", priority = 200)]
		private static void Open() => ServicesExplorerWindow.OpenOnTab<AssetsImporterTab>();
	}
}
