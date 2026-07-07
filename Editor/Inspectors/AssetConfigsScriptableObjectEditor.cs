using System;
using System.Collections.Generic;
using Geuneda.Services.AssetsImporter;
using Geuneda.Services.AddressableIds.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Geuneda.Services.Inspectors.Editor
{
	/// <summary>
	/// UIToolkit custom inspector for all <see cref="AssetConfigsScriptableObject"/> subclasses.
	/// Adds a diagnostics panel (duplicate keys, null / empty-GUID asset references) above the
	/// default property fields, and a "Regenerate Addressable Ids" shortcut button at the bottom.
	/// </summary>
	[CustomEditor(typeof(AssetConfigsScriptableObject), editorForChildClasses: true)]
	public class AssetConfigsScriptableObjectEditor : UnityEditor.Editor
	{
		private VisualElement _diagnosticsPanel;
		private Label _diagnosticsLabel;

		public override VisualElement CreateInspectorGUI()
		{
			var root = new VisualElement();

			// ---- Diagnostics panel ----
			_diagnosticsPanel = new VisualElement();
			_diagnosticsPanel.style.marginBottom = 8;
			_diagnosticsLabel = new Label();
			_diagnosticsLabel.style.whiteSpace = WhiteSpace.Normal;
			_diagnosticsLabel.style.fontSize = 11;
			_diagnosticsPanel.Add(_diagnosticsLabel);
			root.Add(_diagnosticsPanel);

			// ---- Default inspector (manual SerializedProperty iteration) ----
			// We deliberately avoid `new InspectorElement(serializedObject)` here. Because
			// it would re-resolve to this same editor (editorForChildClasses: true) and recurse
			// infinitely → stack overflow on selection of any concrete subclass.
			var iterator = serializedObject.GetIterator();
			if (iterator.NextVisible(enterChildren: true))
			{
				do
				{
					var field = new PropertyField(iterator.Copy());
					field.SetEnabled(iterator.propertyPath != "m_Script");
					root.Add(field);
				} while (iterator.NextVisible(enterChildren: false));
			}
			root.Bind(serializedObject);

			// ---- Regenerate button ----
			var spacer = new VisualElement();
			spacer.style.height = 8;
			root.Add(spacer);

			var regenBtn = new Button(OnRegenerateIds) { text = "Regenerate Addressable Ids" };
			regenBtn.style.height = 26;
			root.Add(regenBtn);

			// ---- Sample-scoped: AssetResolver Sample auto-setup shortcut ----
			// Shown only when the inspected asset is the AssetResolver sample's SpriteConfigs.asset.
			// Decoupled from the sample's editor assembly via menu-item invocation; if the sample
			// or its editor scripts are absent, the menu will be missing and this button hides.
			if (IsAssetResolverSampleConfigs())
			{
				var sampleSpacer = new VisualElement();
				sampleSpacer.style.height = 6;
				root.Add(sampleSpacer);

				var sampleBtn = new Button(OnRefreshAssetResolverSample)
				{
					text = "Refresh AssetResolver Sample Addressables"
				};
				sampleBtn.style.height = 26;
				sampleBtn.tooltip =
					"Marks every PNG in this sample's Sprites/ folder as Addressable in a dedicated " +
					"group, renames non-canonical files to Hero/Coin/Enemy, and wires SpriteConfigs.";
				root.Add(sampleBtn);
			}

			RunDiagnostics();

			return root;
		}

		private void OnEnable()
		{
			RunDiagnostics();
		}

		private void RunDiagnostics()
		{
			if (_diagnosticsPanel == null)
			{
				return;
			}

			var issues = new List<string>();
			var configs = (AssetConfigsScriptableObject)target;

			// Use reflection to read the generic Configs list
			var configsProp = serializedObject.FindProperty("_configs");

			if (configsProp == null || !configsProp.isArray)
			{
				_diagnosticsPanel.style.display = DisplayStyle.None;
				return;
			}

			var seenKeys = new HashSet<string>();

			for (var i = 0; i < configsProp.arraySize; i++)
			{
				var element = configsProp.GetArrayElementAtIndex(i);
				var keyProp = element.FindPropertyRelative("Key");
				var valueProp = element.FindPropertyRelative("Value");

				var keyStr = GetPropertyValueString(keyProp, i);

				if (keyProp != null && !seenKeys.Add(keyStr))
				{
					issues.Add($"Duplicate key: {keyStr}");
				}

				if (valueProp != null)
				{
					var guidProp = valueProp.FindPropertyRelative("m_AssetGUID");

					if (guidProp != null && string.IsNullOrEmpty(guidProp.stringValue))
					{
						issues.Add($"Empty GUID at key: {keyStr}");
					}
				}
			}

			if (issues.Count == 0)
			{
				_diagnosticsPanel.style.display = DisplayStyle.None;
				return;
			}

			_diagnosticsPanel.style.display = DisplayStyle.Flex;
			_diagnosticsPanel.style.backgroundColor = new StyleColor(new Color(0.6f, 0.1f, 0.1f, 0.25f));
			_diagnosticsPanel.style.borderTopWidth = 1;
			_diagnosticsPanel.style.borderBottomWidth = 1;
			_diagnosticsPanel.style.borderLeftWidth = 1;
			_diagnosticsPanel.style.borderRightWidth = 1;
			_diagnosticsPanel.style.borderTopColor = new StyleColor(new Color(0.8f, 0.3f, 0.3f, 0.5f));
			_diagnosticsPanel.style.borderBottomColor = new StyleColor(new Color(0.8f, 0.3f, 0.3f, 0.5f));
			_diagnosticsPanel.style.borderLeftColor = new StyleColor(new Color(0.8f, 0.3f, 0.3f, 0.5f));
			_diagnosticsPanel.style.borderRightColor = new StyleColor(new Color(0.8f, 0.3f, 0.3f, 0.5f));
			_diagnosticsPanel.style.borderTopLeftRadius = 3;
			_diagnosticsPanel.style.borderTopRightRadius = 3;
			_diagnosticsPanel.style.borderBottomLeftRadius = 3;
			_diagnosticsPanel.style.borderBottomRightRadius = 3;
			_diagnosticsPanel.style.paddingTop = 4;
			_diagnosticsPanel.style.paddingBottom = 4;
			_diagnosticsPanel.style.paddingLeft = 6;
			_diagnosticsPanel.style.paddingRight = 6;

			_diagnosticsLabel.text = "Issues:\n" + string.Join("\n", issues);
			_diagnosticsLabel.style.color = new StyleColor(new Color(1f, 0.6f, 0.5f));
		}

		private static string GetPropertyValueString(SerializedProperty prop, int fallbackIndex)
		{
			if (prop == null)
			{
				return $"[{fallbackIndex}]";
			}

			switch (prop.propertyType)
			{
				case SerializedPropertyType.Enum:
					return prop.enumNames.Length > prop.enumValueIndex && prop.enumValueIndex >= 0
						? prop.enumNames[prop.enumValueIndex]
						: prop.enumValueIndex.ToString();
				case SerializedPropertyType.Integer:
					return prop.intValue.ToString();
				case SerializedPropertyType.String:
					return prop.stringValue;
				default:
					return prop.propertyPath.Split('.')[^1];
			}
		}

		private static void OnRegenerateIds()
		{
			AddressableIdsGeneratorUtils.Generate(AddressableIdsEditorSettings.instance);
		}

		private bool IsAssetResolverSampleConfigs()
		{
			var path = AssetDatabase.GetAssetPath(target);
			if (string.IsNullOrEmpty(path))
			{
				return false;
			}

			// Match the sample's canonical layout regardless of the imported version folder.
			return path.Replace('\\', '/').EndsWith("/Asset Resolver/SpriteConfigs.asset",
				StringComparison.Ordinal);
		}

		private static void OnRefreshAssetResolverSample()
		{
			const string menuPath = "Tools/Geuneda/Samples/Asset Resolver/Refresh Addressables";
			if (!EditorApplication.ExecuteMenuItem(menuPath))
			{
				Debug.LogWarning(
					$"[AssetResolverSample] Menu '{menuPath}' is unavailable. Re-import the sample " +
					"via Package Manager so its editor scripts compile.");
			}
		}
	}
}
