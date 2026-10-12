using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Geuneda.Services.Versioning.Editor
{
	/// <summary>
	/// 버전 관리 파이프라인의 에디터 전용 프로젝트 설정입니다.
	/// <see cref="ScriptableSingleton{T}"/> 로 <c>ProjectSettings/VersioningEditorSettings.asset</c> 에 저장됩니다.
	/// </summary>
	[FilePath("ProjectSettings/VersioningEditorSettings.asset", FilePathAttribute.Location.ProjectFolder)]
	internal sealed class VersioningEditorSettings : ScriptableSingleton<VersioningEditorSettings>
	{
		/// <summary><c>version-data.txt</c> 를 쓰는 기본 폴더. <c>Resources</c> 세그먼트를 포함해야 합니다.</summary>
		public const string DefaultFolderPath = "Assets/Configs/Resources";

		[SerializeField] private string _resourcesFolderPath = DefaultFolderPath;

		/// <summary>
		/// 설정된 프로젝트 기준 상대 폴더 경로를 반환합니다. 설정되지 않았으면 <see cref="DefaultFolderPath"/> 를 반환합니다.
		/// </summary>
		public string ResourcesFolderPath =>
			string.IsNullOrWhiteSpace(_resourcesFolderPath) ? DefaultFolderPath : _resourcesFolderPath;

		/// <summary>
		/// 새 Resources 폴더 경로를 저장합니다. 저장 전에 구분자를 슬래시(/)로 정규화합니다.
		/// 값이 유효한지 먼저 <see cref="IsValidResourcesPath"/> 로 확인하세요.
		/// </summary>
		public void SetResourcesFolderPath(string relativePath)
		{
			_resourcesFolderPath = relativePath.Trim().Replace('\\', '/');
			Save(true);
		}

		/// <summary>
		/// <paramref name="relativePath"/> 가 version-data 쓰기 위치로 쓸 수 있는지 검증합니다.
		/// 유효하면 <c>true</c> 를 반환하고, 실패하면 <paramref name="error"/> 에 사람이 읽을 수 있는 메시지를 채웁니다.
		/// </summary>
		/// <remarks>
		/// 검사 규칙:
		/// <list type="bullet">
		///   <item><c>Assets/</c> 로 시작해야 합니다</item>
		///   <item><c>..</c> 경로 세그먼트를 포함하면 안 됩니다</item>
		///   <item>이름이 정확히 <c>Resources</c> 인 경로 세그먼트를 포함해야 합니다</item>
		///   <item>Unity 프로젝트 루트 안의 경로로 해석되어야 합니다</item>
		/// </list>
		/// </remarks>
		public static bool IsValidResourcesPath(string relativePath, out string error)
		{
			error = null;

			if (string.IsNullOrWhiteSpace(relativePath))
			{
				error = "Path cannot be empty.";
				return false;
			}

			var normalised = relativePath.Trim().Replace('\\', '/');

			if (!normalised.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
			{
				error = "Path must start with \"Assets/\".";
				return false;
			}

			var segments = normalised.Split('/');
			foreach (var segment in segments)
			{
				if (segment == "..")
				{
					error = "Path must not contain \"..\" segments.";
					return false;
				}
			}

			var containsResources = false;
			foreach (var segment in segments)
			{
				if (string.Equals(segment, "Resources", StringComparison.Ordinal))
				{
					containsResources = true;
					break;
				}
			}

			if (!containsResources)
			{
				error = "Path must contain a folder segment named exactly \"Resources\" so that " +
				        "Resources.Load<TextAsset>(\"version-data\") can find the file at runtime.";
				return false;
			}

			// 해석된 절대 경로가 프로젝트 루트 안에 있는지 확인합니다.
			var projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
			if (projectRoot == null)
			{
				error = "Could not determine the project root directory.";
				return false;
			}

			var absPath = Path.GetFullPath(Path.Combine(projectRoot, normalised));
			if (!absPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
			{
				error = "Path must resolve to a location inside the project directory.";
				return false;
			}

			return true;
		}
	}
}
