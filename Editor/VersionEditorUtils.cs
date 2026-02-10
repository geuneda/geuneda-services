using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Geuneda.Services;

namespace Geuneda.Services.Editor
{
	/// <summary>
	/// 빌드 전 및 프로젝트 로드 시 프로젝트의 VersionService 인스턴스에 내부 버전을 설정합니다.
	/// </summary>
	public static class VersionEditorUtils
	{
		private const int ShortenedCommitLength = 8;
		private const string AssetsPath = "Assets";
		private const string FilePath = "Configs/Resources";

		/// <summary>
		/// 앱 빌드 전에 내부 버전을 설정합니다.
		/// </summary>
		public static void SetAndSaveInternalVersion(bool isStoreBuild)
		{
			var newVersionData = GenerateInternalVersionSuffix(isStoreBuild);
			var newVersionDataSerialized = JsonUtility.ToJson(newVersionData);
			var oldVersionDataSerialized = LoadVersionDataSerializedSync();
			if (newVersionDataSerialized.Equals(oldVersionDataSerialized, StringComparison.Ordinal))
			{
				return;
			}

			Debug.Log($"Saving new version data: {newVersionDataSerialized}");
			SaveVersionData(newVersionDataSerialized);
		}

		/// <summary>
		/// 디스크에 저장된 게임 버전을 문자열 형식으로 로드합니다
		/// </summary>
		public static string LoadVersionDataSerializedSync()
		{
			var textAsset = Resources.Load<TextAsset>(VersionServices.VersionDataFilename);
			if (!textAsset)
			{
				Debug.LogError("Could not load internal version from Resources.");
				return string.Empty;
			}

			var serialized = textAsset.text;
			Resources.UnloadAsset(textAsset);
			return serialized;
		}

		/// <summary>
		/// 에디터에서 앱 실행 시 내부 버전을 설정합니다.
		/// </summary>
		[InitializeOnLoadMethod]
		private static void OnEditorLoad()
		{
			SetAndSaveInternalVersion(false);
		}

		private static VersionServices.VersionData GenerateInternalVersionSuffix(bool isStoreBuild)
		{
			var data = new VersionServices.VersionData();

			using (var repo = new GitEditorProcess(Application.dataPath))
			{
				try
				{
					if (!repo.IsValidRepo())
					{
						Debug.LogWarning("Project is not a git repo. Internal version not set.");
					}
					else
					{
						var branch = repo.GetBranch();
						if (string.IsNullOrEmpty(branch))
						{
							Debug.LogWarning("Could not get git branch for internal version");
						}
						else
						{
							data.BranchName = branch;
						}

						var commitHash = repo.GetCommitHash();
						if (string.IsNullOrEmpty(commitHash))
						{
							Debug.LogWarning("Could not get git commit for internal version");
						}
						else
						{
							data.CommitHash = commitHash.Substring(0, ShortenedCommitLength);
						}
					}
				}
				catch (Exception e)
				{
					Debug.LogException(e);
					Debug.LogWarning("Could not execute git commands. Internal version not set.");
				}
			}

			data.BuildNumber = PlayerSettings.iOS.buildNumber;
			data.BuildType = isStoreBuild ? "prod" : "dev";

			return data;
		}

		/// <summary>
		/// 이 애플리케이션의 내부 버전을 설정하고 리소스에 저장합니다.
		/// 편집/빌드 시에 호출해야 합니다.
		/// </summary>
		private static void SaveVersionData(string serializedData)
		{
			var absDirPath = Path.Combine(Application.dataPath, FilePath);
			if (!Directory.Exists(absDirPath))
			{
				Directory.CreateDirectory(absDirPath);
			}

			// 잘못된 확장자의 이전 파일 삭제
			const string assetExtension = ".asset";
			var absFilePath = Path.Combine(absDirPath, VersionServices.VersionDataFilename);
			if (File.Exists(Path.ChangeExtension(absFilePath, assetExtension)))
			{
				AssetDatabase.DeleteAsset(
					Path.Combine(AssetsPath, FilePath,
						Path.ChangeExtension(VersionServices.VersionDataFilename, assetExtension)));
			}

			// 새 텍스트 파일 생성
			const string textExtension = ".txt";
			File.WriteAllText(Path.ChangeExtension(absFilePath, textExtension), serializedData);

			AssetDatabase.ImportAsset(
				Path.Combine(AssetsPath, FilePath,
					Path.ChangeExtension(VersionServices.VersionDataFilename, textExtension)));
		}
	}
}
