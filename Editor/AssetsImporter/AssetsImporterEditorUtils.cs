using Geuneda.Services.AssetsImporter;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services.AssetsImporter.Editor
{
	/// <summary>
	/// 탐색된 <see cref="IAssetConfigsImporter"/> 인스턴스 하나의 데이터를 담습니다.
	/// </summary>
	internal class ImportData
	{
		public Type Type;
		public IAssetConfigsImporter Importer;
		public string AssetsFolderPath;
	}

	/// <summary>
	/// Assets Importer 파이프라인용 순수 에디터 유틸리티 메서드 모음입니다.
	/// <see cref="AssetsImporterTab"/> 과 <c>AssetsImporterMenu</c> 스텁에서 사용합니다.
	/// <c>[CustomEditor]</c>, <c>[MenuItem]</c>, Explorer 타입을 참조하지 않습니다.
	/// </summary>
	internal static class AssetsImporterEditorUtils
	{
		/// <summary>
		/// 로드된 어셈블리에서 <see cref="IAssetConfigsImporter"/> 를 구현하는 추상/인터페이스가 아닌 타입을 모두 찾아
		/// 타입마다 <see cref="ImportData"/> 항목 하나를 반환합니다.
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
		/// 임포터 하나를 임포트한 뒤 <see cref="AssetDatabase"/> 를 저장하고 갱신합니다.
		/// </summary>
		public static void Import(IAssetConfigsImporter importer)
		{
			importer.Import();
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		/// <summary>
		/// 모든 임포터를 찾아 각각 임포트한 뒤 <see cref="AssetDatabase"/> 를 저장하고 갱신합니다.
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
		/// 주어진 <paramref name="folderPath"/> 에서 임포터 하나를 임포트한 뒤 에셋을 저장하고 갱신합니다.
		/// </summary>
		public static void ImportWithPath(IAssetConfigsImporter importer, string folderPath)
		{
			importer.Import(folderPath);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		/// <summary>
		/// <see cref="AssetsImporterEditorSettings.AutoUpdateOnRefresh"/> 가 켜져 있으면 다음 에디터 업데이트에서
		/// <see cref="ImportAll"/> 이 실행되도록 예약합니다. Play 모드 진입에 따른 리로드에서는 예약하지 않습니다.
		/// 예약했으면 <c>true</c> 를 반환합니다.
		/// </summary>
		internal static bool TryScheduleAutoImport()
		{
			if (!AssetsImporterEditorSettings.instance.AutoUpdateOnRefresh || EditorApplication.isPlayingOrWillChangePlaymode)
			{
				return false;
			}

			EditorApplication.delayCall -= ImportAll;
			EditorApplication.delayCall += ImportAll;

			return true;
		}

		/// <summary>
		/// 주어진 <paramref name="type"/> 의 ScriptableObject 에셋을 찾거나 새로 만듭니다.
		/// 유효한 <see cref="AssetConfigsScriptableObject"/> 를 찾았거나 만들었으면 <c>true</c> 를 반환합니다.
		/// </summary>
		public static bool TryGetScriptableObject(Type type, out AssetConfigsScriptableObject scriptableObject)
		{
			// 생성기 임포터는 설정 타입이 아직 생성되지 않아 null 일 수 있다. Unity API 에 넘기기 전에 거부해
			// 탐색(DiscoverImporters)과 자동 임포트가 다른 임포터까지 멈추지 않게 한다
			if (type == null || !typeof(AssetConfigsScriptableObject).IsAssignableFrom(type))
			{
				scriptableObject = null;
				return false;
			}

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

		[DidReloadScripts]
		private static void OnScriptsReloaded()
		{
			TryScheduleAutoImport();
		}
	}
}
