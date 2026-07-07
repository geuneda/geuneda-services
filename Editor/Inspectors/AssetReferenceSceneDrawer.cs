using Geuneda.Services.AssetsImporter;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Geuneda.Services.Inspectors.Editor
{
	/// <summary>
	/// UIToolkit property drawer for <see cref="AssetReferenceScene"/>.
	/// Shows the default GUID picker, a read-only scene path label, and
	/// an "Open in Addressables Groups" button that focuses the entry.
	/// </summary>
	[CustomPropertyDrawer(typeof(AssetReferenceScene), useForChildren: true)]
	public class AssetReferenceSceneDrawer : PropertyDrawer
	{
		public override VisualElement CreatePropertyGUI(SerializedProperty property)
		{
			var container = new VisualElement();
			container.style.marginBottom = 2;

			var guidProp = property.FindPropertyRelative("m_AssetGUID");
			var defaultField = new PropertyField(property);
			container.Add(defaultField);

			var infoRow = new VisualElement();
			infoRow.style.flexDirection = FlexDirection.Row;
			infoRow.style.alignItems = Align.Center;
			infoRow.style.marginTop = 2;
			infoRow.style.marginLeft = 2;

			var pathLabel = new Label(GetScenePath(guidProp?.stringValue));
			pathLabel.style.flexGrow = 1;
			pathLabel.style.fontSize = 10;
			pathLabel.style.color = new UnityEngine.UIElements.StyleColor(new UnityEngine.Color(0.6f, 0.8f, 0.6f));
			pathLabel.style.overflow = Overflow.Hidden;
			infoRow.Add(pathLabel);

			var openBtn = new Button(() => OpenInAddressables(guidProp?.stringValue)) { text = "Open in Addressables" };
			openBtn.style.fontSize = 10;
			openBtn.style.height = 18;
			infoRow.Add(openBtn);

			container.Add(infoRow);

			if (guidProp != null)
			{
				container.TrackPropertyValue(guidProp, _ =>
				{
					pathLabel.text = GetScenePath(guidProp.stringValue);
				});
			}

			return container;
		}

		private static string GetScenePath(string guid)
		{
			if (string.IsNullOrEmpty(guid))
			{
				return "— no scene assigned —";
			}

			var path = AssetDatabase.GUIDToAssetPath(guid);
			return string.IsNullOrEmpty(path) ? $"GUID: {guid}" : path;
		}

		private static void OpenInAddressables(string guid)
		{
			if (string.IsNullOrEmpty(guid))
			{
				return;
			}

			var settings = AddressableAssetSettingsDefaultObject.Settings;

			if (settings == null)
			{
				return;
			}

			var entry = settings.FindAssetEntry(guid);

			if (entry == null)
			{
				UnityEngine.Debug.LogWarning($"[AssetReferenceSceneDrawer] GUID {guid} is not an Addressable entry.");
				return;
			}

			EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
		}
	}
}
