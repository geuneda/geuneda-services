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
	/// 모든 <see cref="AssetConfigsScriptableObject"/> 파생 클래스용 UIToolkit 커스텀 인스펙터입니다.
	/// 기본 프로퍼티 필드 위에 진단 패널(중복 키, null / 빈 GUID 에셋 참조)을, 아래에
	/// "Regenerate Addressable Ids" 바로가기 버튼을 추가합니다.
	/// </summary>
	[CustomEditor(typeof(AssetConfigsScriptableObject), editorForChildClasses: true)]
	public class AssetConfigsScriptableObjectEditor : UnityEditor.Editor
	{
		private VisualElement _diagnosticsPanel;
		private Label _diagnosticsLabel;

		public override VisualElement CreateInspectorGUI()
		{
			var root = new VisualElement();

			// ---- 진단 패널 ----
			_diagnosticsPanel = new VisualElement();
			_diagnosticsPanel.style.marginBottom = 8;
			_diagnosticsLabel = new Label();
			_diagnosticsLabel.style.whiteSpace = WhiteSpace.Normal;
			_diagnosticsLabel.style.fontSize = 11;
			_diagnosticsPanel.Add(_diagnosticsLabel);
			root.Add(_diagnosticsPanel);

			// ---- 기본 인스펙터 (SerializedProperty 수동 순회) ----
			// 여기서는 일부러 `new InspectorElement(serializedObject)` 를 쓰지 않습니다. 이 에디터
			// (editorForChildClasses: true)로 다시 해석되어 무한 재귀하므로, 구체 파생 클래스를
			// 선택하면 스택 오버플로가 납니다.
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

			// ---- 재생성 버튼 ----
			var spacer = new VisualElement();
			spacer.style.height = 8;
			root.Add(spacer);

			var regenBtn = new Button(OnRegenerateIds) { text = "Regenerate Addressable Ids" };
			regenBtn.style.height = 26;
			root.Add(regenBtn);

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

			// 제네릭 Configs 리스트는 SerializedProperty 로 읽습니다
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
	}
}
