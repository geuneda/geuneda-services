using System.Collections.Generic;
using Geuneda.Services.AssetsImporter;
using Geuneda.Services.AssetsImporter.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Geuneda.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// Services Explorer tab for the Assets Importer pipeline.
	/// Replaces the old <c>AssetsImporter.asset</c> custom inspector.
	/// Reads/writes settings via <see cref="AssetsImporterEditorSettings"/> and invokes
	/// discovery/import via <see cref="AssetsImporterEditorUtils"/>.
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

			// ---- Auto Import section ----
			scroll.Add(MakeSectionLabel("Auto Import"));

			_autoImportToggle = new Toggle("Refresh after script compilation");
			_autoImportToggle.tooltip = "When enabled, all importers run automatically after every script compilation.";
			_autoImportToggle.value = AssetsImporterEditorSettings.instance.AutoUpdateOnRefresh;
			_autoImportToggle.RegisterValueChangedCallback(e =>
			{
				AssetsImporterEditorSettings.instance.AutoUpdateOnRefresh = e.newValue;
			});
			scroll.Add(_autoImportToggle);

			// ---- Discovered Importers section ----
			scroll.Add(MakeSectionLabel("Discovered Importers"));

			_importerList = new VisualElement();
			scroll.Add(_importerList);

			Add(scroll);

			// ---- Action bar ----
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

			// Type name
			var typeLabel = new Label(capturedData.Type.Name);
			typeLabel.AddToClassList("row-label");
			row.Add(typeLabel);

			// Path label (italic if unset)
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

			// Set/Update Path button
			var pathBtnText = string.IsNullOrEmpty(capturedData.AssetsFolderPath) ? "Set Path" : "Update Path";
			var pathBtn = MakeRowButton(pathBtnText, () => OnSetPath(capturedData, pathLabel));
			row.Add(pathBtn);

			if (capturedData.Importer is IAssetConfigsGeneratorImporter)
				{
					// Generator importers need a path before they can do anything
					var noteLabel = new Label("Set path to generate scripts");
					noteLabel.AddToClassList("tab-empty-label");
					noteLabel.style.marginLeft = 4;
					row.Add(noteLabel);
				}
				else
				{
					// Regular importers: Import + Select buttons
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

			// Convert to project-relative path starting with "Assets/"
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
