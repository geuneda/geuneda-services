using Geuneda.Services.Editor.Explorer;
using Geuneda.Services.Editor.Explorer.Tabs;
using UnityEditor;

namespace Geuneda.Services.AddressableIds.Editor
{
	/// <summary>
	/// Top-bar menu stubs for the Addressable Ids Generator under <c>Tools &gt; Geuneda &gt; Addressable Ids</c>.
	/// Settings live in the Services Explorer Addressable Ids tab.
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
