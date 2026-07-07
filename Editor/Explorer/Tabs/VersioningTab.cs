using System.IO;
using Geuneda.Services.Versioning.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Geuneda.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// Shows build version information, exposes the configurable write location for
	/// <c>version-data.txt</c>, and provides a "Reveal file" action.
	/// Works in both Edit and Play mode (reads the file directly on the Editor side).
	/// Regeneration runs automatically on every domain reload via <see cref="VersionEditorUtils"/>.
	/// </summary>
	public class VersioningTab : ServiceTab
	{
		public override string DisplayName => "Versioning";
		protected override int RefreshIntervalMs => 2000;

		private Label _externalLabel;
		private Label _internalLabel;
		private Label _branchLabel;
		private Label _commitLabel;
		private Label _buildNumberLabel;
		private Label _filePreviewLabel;
		private Label _fileStatusLabel;
		private TextField _folderPathField;

		private static string VersionDataFilePath
		{
			get
			{
				var relFolder = VersioningEditorSettings.instance.ResourcesFolderPath;
				var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
				return Path.Combine(projectRoot, relFolder, VersionServices.VersionDataFilename + ".txt");
			}
		}

		protected override void BuildUi()
		{
			var scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.AddToClassList("tab-scroll");

			// ---- Write Location section ----
			scroll.Add(MakeSectionLabel("Version File Path"));
			scroll.Add(BuildFolderRow());

			// ---- Runtime Values section ----
			scroll.Add(MakeSectionLabel("Runtime Values"));

			_externalLabel = AddValueRow(scroll, "VersionExternal");
			_internalLabel = AddValueRow(scroll, "VersionInternal");
			_branchLabel = AddValueRow(scroll, "Branch");
			_commitLabel = AddValueRow(scroll, "Commit");
			_buildNumberLabel = AddValueRow(scroll, "BuildNumber");

			// ---- File preview section ----
			scroll.Add(MakeSectionLabel("version-data.txt"));

			_fileStatusLabel = new Label();
			_fileStatusLabel.style.fontSize = 10;
			_fileStatusLabel.style.marginBottom = 2;
			scroll.Add(_fileStatusLabel);

			_filePreviewLabel = new Label();
			_filePreviewLabel.AddToClassList("json-preview");
			scroll.Add(_filePreviewLabel);

		var bar = MakeActionBar();
		bar.Add(MakePrimaryButton("Reveal version-data.txt", OnRevealFile));
		scroll.Add(bar);

			Add(scroll);
		}

		protected override void Refresh()
		{
			_externalLabel.text = VersionServices.VersionExternal;

			try { _internalLabel.text = VersionServices.VersionInternal; }
			catch { _internalLabel.text = "— (not loaded)"; }

			try { _branchLabel.text = VersionServices.Branch; }
			catch { _branchLabel.text = "— (not loaded)"; }

			try { _commitLabel.text = VersionServices.Commit; }
			catch { _commitLabel.text = "— (not loaded)"; }

			try { _buildNumberLabel.text = VersionServices.BuildNumber; }
			catch { _buildNumberLabel.text = "— (not loaded)"; }

			RefreshFilePreview();
		}

		private VisualElement BuildFolderRow()
		{
			var row = new VisualElement();
			row.AddToClassList("row");

			_folderPathField = new TextField();
			_folderPathField.isReadOnly = true;
			_folderPathField.style.flexGrow = 1;
			_folderPathField.value = VersioningEditorSettings.instance.ResourcesFolderPath;
			row.Add(_folderPathField);

			row.Add(new Button(OnBrowseFolder) { text = "Browse…" });
			row.Add(new Button(OnResetFolder) { text = "Reset" });

			return row;
		}

		private void RefreshFilePreview()
		{
			var path = VersionDataFilePath;

			if (File.Exists(path))
			{
				_fileStatusLabel.text = path;
				_fileStatusLabel.style.color = new StyleColor(new Color(0.6f, 0.9f, 0.6f));
				_filePreviewLabel.text = File.ReadAllText(path);
			}
			else
			{
				_fileStatusLabel.text = "File not found: " + path;
				_fileStatusLabel.style.color = new StyleColor(new Color(0.9f, 0.5f, 0.4f));
				_filePreviewLabel.text = "";
			}
		}

		private static Label AddValueRow(VisualElement parent, string label)
		{
			var row = new VisualElement();
			row.AddToClassList("row");

			var lbl = new Label(label);
			lbl.AddToClassList("row-label");
			lbl.style.minWidth = 130;
			row.Add(lbl);

			var val = new Label("—");
			val.AddToClassList("row-value");
			row.Add(val);

			parent.Add(row);
			return val;
		}

		private void OnBrowseFolder()
		{
			var selectedAbs = EditorUtility.OpenFolderPanel(
				"Select Resources folder",
				Application.dataPath,
				"");

			if (string.IsNullOrEmpty(selectedAbs))
			{
				return;
			}

			// Convert absolute path to project-relative (Assets/...).
			var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
			var relative = GetRelativePath(projectRoot, selectedAbs);

			if (!VersioningEditorSettings.IsValidResourcesPath(relative, out var error))
			{
				EditorUtility.DisplayDialog("Invalid folder", error, "OK");
				return;
			}

			VersioningEditorSettings.instance.SetResourcesFolderPath(relative);
			_folderPathField.value = VersioningEditorSettings.instance.ResourcesFolderPath;
			RefreshFilePreview();
		}

		private void OnResetFolder()
		{
			VersioningEditorSettings.instance.SetResourcesFolderPath(VersioningEditorSettings.DefaultFolderPath);
			_folderPathField.value = VersioningEditorSettings.DefaultFolderPath;
			RefreshFilePreview();
		}

		private void OnRevealFile()
		{
			var path = VersionDataFilePath;

			if (File.Exists(path))
			{
				EditorUtility.RevealInFinder(path);
			}
			else
			{
				Debug.LogWarning($"[ServicesExplorer] version-data.txt not found at: {path}");
			}
		}

		/// <summary>
		/// Returns a forward-slash project-relative path (e.g. <c>Assets/Configs/Resources</c>)
		/// from an absolute path that lives under <paramref name="baseDir"/>.
		/// Returns the original string unchanged if it does not start with <paramref name="baseDir"/>.
		/// </summary>
		private static string GetRelativePath(string baseDir, string fullPath)
		{
			var normalBase = baseDir.Replace('\\', '/').TrimEnd('/') + '/';
			var normalFull = fullPath.Replace('\\', '/');

			if (normalFull.StartsWith(normalBase, System.StringComparison.OrdinalIgnoreCase))
			{
				return normalFull.Substring(normalBase.Length).TrimEnd('/');
			}

			return normalFull;
		}
	}
}
