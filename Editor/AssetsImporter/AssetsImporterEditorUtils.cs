using Geuneda.Services.AssetsImporter;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services.AssetsImporter.Editor
{
	/// <summary>
	/// Encapsulates the data for a single discovered <see cref="IAssetConfigsImporter"/> instance.
	/// </summary>
	internal class ImportData
	{
		public Type Type;
		public IAssetConfigsImporter Importer;
		public string AssetsFolderPath;
	}

	/// <summary>
	/// Pure editor utility methods for the Assets Importer pipeline.
	/// Used by <see cref="AssetsImporterTab"/> and the <c>AssetsImporterMenu</c> stubs.
	/// Does not reference any <c>[CustomEditor]</c>, <c>[MenuItem]</c>, or Explorer types.
	/// </summary>
	internal static class AssetsImporterEditorUtils
	{
		/// <summary>
		/// Discovers all non-abstract, non-interface types in the loaded assemblies that implement
		/// <see cref="IAssetConfigsImporter"/> and returns one <see cref="ImportData"/> entry per type.
		/// </summary>
		public static List<ImportData> DiscoverImporters()
		{
			var importerInterface = typeof(IAssetConfigsImporter);
			var importers = new List<ImportData>();

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				foreach (var type in assembly.GetTypes())
				{
					if (type.IsAbstract || type.IsInterface || !importerInterface.IsAssignableFrom(type))
					{
						continue;
					}

					var importer = Activator.CreateInstance(type) as IAssetConfigsImporter;
					TryGetScriptableObject(importer.ScriptableObjectType, out var scriptableObject);

					importers.Add(new ImportData
					{
						Type = type,
						Importer = importer,
						AssetsFolderPath = scriptableObject?.AssetsFolderPath
					});
				}
			}

			return importers;
		}

		/// <summary>
		/// Imports a single importer, then saves and refreshes the <see cref="AssetDatabase"/>.
		/// </summary>
		public static void Import(IAssetConfigsImporter importer)
		{
			importer.Import();
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		/// <summary>
		/// Discovers all importers, imports each one, then saves and refreshes the <see cref="AssetDatabase"/>.
		/// </summary>
		public static void ImportAll()
		{
			var importers = DiscoverImporters();

			foreach (var data in importers)
			{
				data.Importer.Import();
			}

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		/// <summary>
		/// Imports a single importer from the supplied <paramref name="folderPath"/>, saves assets, and refreshes.
		/// </summary>
		public static void ImportWithPath(IAssetConfigsImporter importer, string folderPath)
		{
			importer.Import(folderPath);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		/// <summary>
		/// Attempts to find or create the ScriptableObject asset for the given <paramref name="type"/>.
		/// Returns <c>true</c> when a valid <see cref="AssetConfigsScriptableObject"/> was found or created.
		/// </summary>
		public static bool TryGetScriptableObject(Type type, out AssetConfigsScriptableObject scriptableObject)
		{
			var assets = AssetDatabase.FindAssets($"t:{type?.Name}");
			var obj = assets.Length > 0
				? AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(assets[0]), type)
				: ScriptableObject.CreateInstance(type);

			if (obj == null)
			{
				scriptableObject = null;
				return false;
			}

			scriptableObject = obj as AssetConfigsScriptableObject;

			if (assets.Length == 0 && type != null)
			{
				AssetDatabase.CreateAsset(scriptableObject, $"Assets/{type.Name}.asset");
				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();
			}

			return true;
		}
	}
}
