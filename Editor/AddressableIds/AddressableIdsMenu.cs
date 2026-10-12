using Geuneda.Services.Editor.Explorer;
using Geuneda.Services.Editor.Explorer.Tabs;
using UnityEditor;

namespace Geuneda.Services.AddressableIds.Editor
{
	/// <summary>
	/// <c>Tools &gt; Geuneda &gt; Addressable Ids</c> 아래의 Addressable Ids 생성기 상단 메뉴 스텁입니다.
	/// 설정은 Services Explorer 의 Addressable Ids 탭에 있습니다.
	/// </summary>
	internal static class AddressableIdsMenu
	{
		[MenuItem("Tools/Geuneda/Addressable Ids/Generate Addressable Ids", priority = 100)]
		private static void Generate() =>
			AddressableIdsGeneratorUtils.Generate(AddressableIdsEditorSettings.instance);

		[MenuItem("Tools/Geuneda/Addressable Ids/Open in Explorer", priority = 200)]
		private static void Open() => ServicesExplorerWindow.OpenOnTab<AddressableIdsTab>();
	}
}
