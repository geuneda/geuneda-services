using System.IO;
using Geuneda.Services.Versioning.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Geuneda.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// 빌드 버전 정보를 보여 주고, <c>version-data.txt</c> 를 쓸 위치를 설정할 수 있게 하며,
	/// 파일을 Finder/탐색기에서 보여 주는 동작을 제공합니다.
	/// Edit 모드와 Play 모드 모두에서 동작합니다(파일 미리 보기는 에디터에서 파일을 직접 읽음).
	/// 재생성은 <see cref="VersionEditorUtils"/> 가 도메인 리로드마다 자동으로 실행합니다.
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

			// ---- 쓰기 위치 섹션 ----
			scroll.Add(MakeSectionLabel("Version File Path"));
			scroll.Add(BuildFolderRow());

			// ---- Runtime Values 섹션 ----
			scroll.Add(MakeSectionLabel("Runtime Values"));

			_externalLabel = AddValueRow(scroll, "VersionExternal");
			_internalLabel = AddValueRow(scroll, "VersionInternal");
			_branchLabel = AddValueRow(scroll, "Branch");
			_commitLabel = AddValueRow(scroll, "Commit");
			_buildNumberLabel = AddValueRow(scroll, "BuildNumber");

			// ---- 파일 미리 보기 섹션 ----
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

			// 절대 경로를 프로젝트 기준 상대 경로(Assets/...)로 바꿉니다.
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
		/// <paramref name="baseDir"/> 아래에 있는 절대 경로를 슬래시(/)를 쓰는 프로젝트 기준 상대 경로
		/// (예: <c>Assets/Configs/Resources</c>)로 바꿔 반환합니다.
		/// <paramref name="baseDir"/> 로 시작하지 않으면 원래 문자열을 그대로 반환합니다.
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
