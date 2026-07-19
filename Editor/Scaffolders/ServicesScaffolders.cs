using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace Geuneda.Services.Scaffolders.Editor
{
	/// <summary>
	/// Provides <c>Assets &gt; Create &gt; Geuneda Services &gt; …</c> menu items that scaffold
	/// common service types from templates. No test stubs are emitted.
	/// </summary>
	public static class ServicesScaffolders
	{
		private const string TemplatesFolder = "Editor/Scaffolders/Templates~";

		[MenuItem("Assets/Create/Geuneda Services/Message", priority = 81)]
		private static void CreateMessage()
		{
			CreateFromTemplate("NewMessage.cs.txt", "NewMessage.cs");
		}

		[MenuItem("Assets/Create/Geuneda Services/Command", priority = 82)]
		private static void CreateCommand()
		{
			CreateFromTemplate("NewCommand.cs.txt", "NewCommand.cs");
		}

		[MenuItem("Assets/Create/Geuneda Services/Service", priority = 83)]
		private static void CreateService()
		{
			CreateFromTemplate("NewService.cs.txt", "NewService.cs");
		}

		[MenuItem("Assets/Create/Geuneda Services/Pool Entity", priority = 84)]
		private static void CreatePoolEntity()
		{
			CreateFromTemplate("NewPoolEntity.cs.txt", "NewPoolEntity.cs");
		}

		private static void CreateFromTemplate(string templateFileName, string defaultName)
		{
			var templatePath = FindTemplatePath(templateFileName);

			if (templatePath == null)
			{
				Debug.LogError($"[ServicesScaffolders] Template not found: {templateFileName}");
				return;
			}

			var endAction = ScriptableObject.CreateInstance<ScriptNameEditAction>();
			endAction.TemplatePath = templatePath;

			ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
#if UNITY_6000_5_OR_NEWER
				EntityId.None,
#else
				0,
#endif
				endAction,
				defaultName,
				EditorGUIUtility.IconContent("cs Script Icon").image as Texture2D,
				null);
		}

		private static string FindTemplatePath(string fileName)
		{
			var guids = AssetDatabase.FindAssets("Geuneda.Services.Editor t:asmdef");

			foreach (var guid in guids)
			{
				var asmdefPath = AssetDatabase.GUIDToAssetPath(guid);
				var editorDir = Path.GetDirectoryName(asmdefPath);

				if (editorDir == null)
				{
					continue;
				}

				var packageRoot = Directory.GetParent(editorDir)?.FullName;

				if (packageRoot == null)
				{
					continue;
				}

				var candidate = Path.Combine(packageRoot, TemplatesFolder, fileName);

				if (File.Exists(candidate))
				{
					return candidate;
				}
			}

			return null;
		}

		/// <summary>
		/// Called by <see cref="ProjectWindowUtil"/> after the user confirms the file name.
		/// Reads the template, replaces placeholders, and writes the final .cs file.
		/// </summary>
		private class ScriptNameEditAction :
#if UNITY_6000_5_OR_NEWER
			AssetCreationEndAction
#else
			EndNameEditAction
#endif
		{
			public string TemplatePath;

#if UNITY_6000_5_OR_NEWER
			public override void Action(EntityId instanceId, string pathName, string resourceFile)
#else
			public override void Action(int instanceId, string pathName, string resourceFile)
#endif
			{
				var scriptName = Path.GetFileNameWithoutExtension(pathName);
				var ns = DeriveNamespace(pathName);
				var template = File.ReadAllText(TemplatePath, Encoding.UTF8);

				var content = template
					.Replace("$NAME$", scriptName)
					.Replace("$NAMESPACE$", ns);

				File.WriteAllText(pathName, content, Encoding.UTF8);
				AssetDatabase.ImportAsset(pathName);

				var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(pathName);
				ProjectWindowUtil.ShowCreatedAsset(asset);
			}

#if !UNITY_6000_5_OR_NEWER
			public override void Cancelled(int instanceId, string pathName, string resourceFile)
			{
			}
#endif

			private static string DeriveNamespace(string assetPath)
			{
				var rootNamespace = EditorSettings.projectGenerationRootNamespace;

				if (string.IsNullOrEmpty(rootNamespace))
				{
					rootNamespace = "Game";
				}

				var dir = Path.GetDirectoryName(assetPath);

				if (string.IsNullOrEmpty(dir))
				{
					return rootNamespace;
				}

				dir = dir.Replace('\\', '/');

				const string assetsPrefix = "Assets/";

				if (dir.StartsWith(assetsPrefix, StringComparison.Ordinal))
				{
					dir = dir.Substring(assetsPrefix.Length);
				}

				var parts = dir.Split('/');
				var nsBuilder = new StringBuilder(rootNamespace);

				foreach (var part in parts)
				{
					if (string.IsNullOrEmpty(part))
					{
						continue;
					}

					nsBuilder.Append('.');
					nsBuilder.Append(part);
				}

				return nsBuilder.ToString();
			}
		}
	}
}
