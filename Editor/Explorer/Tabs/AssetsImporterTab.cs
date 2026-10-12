using System.Collections.Generic;
using Geuneda.Services.AssetsImporter;
using Geuneda.Services.AssetsImporter.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Geuneda.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// Assets Importer 파이프라인용 Services Explorer 탭입니다.
	/// 예전 <c>AssetsImporter.asset</c> 커스텀 인스펙터를 대체합니다.
	/// 설정은 <see cref="AssetsImporterEditorSettings"/> 로 읽고 쓰며, 탐색/임포트는
	/// <see cref="AssetsImporterEditorUtils"/> 로 실행합니다.
	/// </summary>
	public class AssetsImporterTab : ServiceTab
	{
		public override string DisplayName => "Assets Importer";
		protected override int RefreshIntervalMs => 2000;

		private Toggle _autoImportToggle;
		private VisualElement _importerList;
		private List<ImportData> _cachedImporters;

		protected override void BuildUi()
		{
			var scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.AddToClassList("tab-scroll");

			// ---- Auto Import 섹션 ----
			scroll.Add(MakeSectionLabel("Auto Import"));

			_autoImportToggle = new Toggle("Refresh after script compilation");
			_autoImportToggle.tooltip = "When enabled, all importers run automatically after every script compilation.";
			_autoImportToggle.value = AssetsImporterEditorSettings.instance.AutoUpdateOnRefresh;
			_autoImportToggle.RegisterValueChangedCallback(e =>
			{
				AssetsImporterEditorSettings.instance.AutoUpdateOnRefresh = e.newValue;
			});
			scroll.Add(_autoImportToggle);

			// ---- Discovered Importers 섹션 ----
			scroll.Add(MakeSectionLabel("Discovered Importers"));

			_importerList = new VisualElement();
			scroll.Add(_importerList);

			Add(scroll);

			// ---- 액션 바 ----
			var bar = MakeActionBar();
			bar.Add(MakePrimaryButton("Import All", OnImportAll));
			bar.Add(new Button(OnRefreshImporters) { text = "Refresh Importers" });
			Add(bar);
		}

		protected override void Refresh()
		{
			_autoImportToggle.SetValueWithoutNotify(AssetsImporterEditorSettings.instance.AutoUpdateOnRefresh);
			RepopulateImporterList();
		}

		private void RepopulateImporterList()
		{
			if (_cachedImporters == null)
			{
				_cachedImporters = AssetsImporterEditorUtils.DiscoverImporters();
			}

			_importerList.Clear();

			if (_cachedImporters.Count == 0)
			{
				_importerList.Add(MakeEmptyLabel("No IAssetConfigsImporter implementations found."));
				return;
			}

			foreach (var importData in _cachedImporters)
			{
				var capturedData = importData;

				var container = new VisualElement();
				container.style.marginBottom = 4;

				var row = new VisualElement();
				row.AddToClassList("row");

			// 타입 이름
			var typeLabel = new Label(capturedData.Type.Name);
			typeLabel.AddToClassList("row-label");
			row.Add(typeLabel);

			// 경로 레이블(설정되지 않았으면 회색)
			var pathLabel = new Label();
			pathLabel.AddToClassList("row-label-mono");
			pathLabel.style.maxWidth = 160;

			if (string.IsNullOrEmpty(capturedData.AssetsFolderPath))
			{
				pathLabel.text = "< no path set >";
				pathLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
			}
			else
			{
				pathLabel.text = capturedData.AssetsFolderPath;
				pathLabel.style.color = new StyleColor(new Color(0.7f, 0.9f, 0.7f));
			}

			row.Add(pathLabel);

			// Set/Update Path 버튼
			var pathBtnText = string.IsNullOrEmpty(capturedData.AssetsFolderPath) ? "Set Path" : "Update Path";
			var pathBtn = MakeRowButton(pathBtnText, () => OnSetPath(capturedData, pathLabel));
			row.Add(pathBtn);

			if (capturedData.Importer is IAssetConfigsGeneratorImporter)
				{
					// 생성기 임포터는 경로가 있어야 동작합니다
					var noteLabel = new Label("Set path to generate scripts");
					noteLabel.AddToClassList("tab-empty-label");
					noteLabel.style.marginLeft = 4;
					row.Add(noteLabel);
				}
				else
				{
					// 일반 임포터: Import + Select 버튼
					var importBtn = MakeRowButton("Import", () =>
					{
						AssetsImporterEditorUtils.Import(capturedData.Importer);
						_cachedImporters = null;
						RepopulateImporterList();
					});
					row.Add(importBtn);

					var selectBtn = MakeRowButton("Select", () => OnSelectObject(capturedData));
					row.Add(selectBtn);
				}

				container.Add(row);
				_importerList.Add(container);
			}
		}

		private void OnSetPath(ImportData data, Label pathLabel)
		{
			var selected = EditorUtility.OpenFolderPanel("Select Assets Folder", Application.dataPath, "");

			if (string.IsNullOrEmpty(selected))
			{
				return;
			}

			// "Assets/" 로 시작하는 프로젝트 기준 상대 경로로 바꿉니다
			var assetsIndex = selected.IndexOf("Assets/", System.StringComparison.Ordinal);

			if (assetsIndex < 0)
			{
				EditorUtility.DisplayDialog("Invalid folder", "The selected folder must be inside the Assets/ directory.", "OK");
				return;
			}

			var relativePath = selected.Substring(assetsIndex);

			data.AssetsFolderPath = relativePath;
			pathLabel.text = relativePath;
			pathLabel.style.color = new StyleColor(new Color(0.7f, 0.9f, 0.7f));

			AssetsImporterEditorUtils.ImportWithPath(data.Importer, relativePath);
			_cachedImporters = null;
			RepopulateImporterList();
		}

		private static void OnSelectObject(ImportData data)
		{
			if (AssetsImporterEditorUtils.TryGetScriptableObject(data.Importer.ScriptableObjectType, out var so))
			{
				Selection.activeObject = so;
			}
		}

		private void OnImportAll()
		{
			AssetsImporterEditorUtils.ImportAll();
			_cachedImporters = null;
			RepopulateImporterList();
		}

		private void OnRefreshImporters()
		{
			_cachedImporters = null;
			RepopulateImporterList();
		}
	}
}
