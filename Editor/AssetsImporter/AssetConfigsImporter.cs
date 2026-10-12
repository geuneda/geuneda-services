using Geuneda.Services.AssetsImporter;
using Geuneda.DataExtensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services.AssetsImporter.Editor
{
	/// <summary>
	/// 에셋 임포터는 프로젝트의 특정 에셋을 에디터 시점에 처리하는 커스텀 처리기를 만들 수 있게 합니다.
	/// 대응하는 에셋 타입을 임포트해 각 id 와 함께 컨테이너에 매핑합니다.
	/// </summary>
	public interface IAssetConfigsImporter
	{
		/// <summary>
		/// 저장될 ScriptableObject 의 타입입니다. 런타임에 ScriptableObject 타입으로 캐스팅할 때 쓰는 헬퍼입니다
		/// </summary>
		Type ScriptableObjectType { get; }

		/// <summary>
		/// 이 에셋 설정 범위에 속한 모든 에셋을 정해진 컨테이너로 임포트합니다
		/// </summary>
		void Import(string assetsFolderPath = null);
	}

	/// <summary>
	/// 에셋 설정 데이터를 생성하는 에셋 임포터용 인터페이스입니다.
	/// </summary>
	public interface IAssetConfigsGeneratorImporter : IAssetConfigsImporter
	{
		/// <summary>
		/// 식별자 타입의 이름을 가져옵니다.
		/// </summary>
		string TIdName { get; }

		/// <summary>
		/// ScriptableObject 타입의 이름을 가져옵니다.
		/// </summary>
		string TScriptableObjectName { get; }

		/// <summary>
		/// 기존 스크립트를 캐시(<c>_Old.cs</c> 로 보관)할지 여부를 가져옵니다.
		/// </summary>
		bool CacheScriptAsOld { get; }
	}

	/// <inheritdoc />
	public abstract class AssetsConfigsImporter<TId, TAsset, TScriptableObject> : AssetsConfigsImporterBase<TId, TAsset, TScriptableObject>
		where TId : Enum
		where TScriptableObject : AssetConfigsScriptableObject<TId, TAsset>
	{
		/// <inheritdoc />
		protected override TId[] Ids => Enum.GetValues(typeof(TId)) as TId[];
	}

	/// <inheritdoc cref="IAssetConfigsImporter"/>
	/// <remarks>
	/// 에셋 데이터를 <typeparamref name="TScriptableObject"/> 타입의 ScriptableObject 에 저장합니다
	/// </remarks>
	public abstract class AssetsConfigsImporterBase<TId, TAsset, TScriptableObject> : IAssetConfigsImporter
		where TScriptableObject : AssetConfigsScriptableObject<TId, TAsset>
	{
		/// <inheritdoc />
		public Type ScriptableObjectType => typeof(TScriptableObject);

		/// <inheritdoc />
		public virtual Type AssetType => typeof(TAsset);

		/// <summary>
		/// 임포트할 에셋의 식별자 배열을 가져옵니다.
		/// </summary>
		/// <remarks>
		/// 추상 프로퍼티이므로 파생 클래스에서 구현해야 합니다.
		/// 임포트할 에셋의 식별자를 얻는 데 사용됩니다.
		/// </remarks>
		protected abstract TId[] Ids { get; }

		/// <inheritdoc />
		public void Import(string assetsFolderPath = null)
		{
			var type = ScriptableObjectType;
			var soAssets = AssetDatabase.FindAssets($"t:{type.Name}");
			var scriptableObject = soAssets.Length > 0
									   ? AssetDatabase.LoadAssetAtPath<TScriptableObject>(AssetDatabase.GUIDToAssetPath(soAssets[0]))
									   : ScriptableObject.CreateInstance<TScriptableObject>();

			if(!string.IsNullOrEmpty(assetsFolderPath))
			{
				scriptableObject.AssetsFolderPath = assetsFolderPath;
			}

			if (soAssets.Length == 0)
			{
				AssetDatabase.CreateAsset(scriptableObject, $"Assets/{type.Name}.asset");
			}

			var assetGuids = new List<string>(AssetDatabase.FindAssets($"t:{AssetType.Name}", new[]
			{
				scriptableObject.AssetsFolderPath
			}));
			var assetsPaths = assetGuids.ConvertAll(a => AssetDatabase.GUIDToAssetPath(a));

			scriptableObject.Configs.Clear();
			scriptableObject.Configs.AddRange(OnImportIds(scriptableObject, assetGuids, assetsPaths));
			OnImportComplete(scriptableObject);
			EditorUtility.SetDirty(scriptableObject);

			Debug.Log($"Finished importing asset data of '{AssetType.Name}' type with '{typeof(TId).Name}' as identifier.\n" +
					  $"To: '{typeof(TScriptableObject).Name}' - From '{scriptableObject.AssetsFolderPath}' ");
		}

		protected virtual string IdPattern(TId id)
		{
			return id.ToString();
		}

		protected virtual List<Pair<TId, AssetReference>> OnImportIds(TScriptableObject scriptableObject,
																	  List<string> assetGuids,
																	  List<string> assetsPaths)
		{
			var ids = Ids;
			var list = new List<Pair<TId, AssetReference>>(ids.Length);

			for (var i = 0; i < ids.Length; i++)
			{
				var indexOf = IndexOfId(IdPattern(ids[i]), assetsPaths);

				if (indexOf < 0)
				{
					continue;
				}

				list.Add(new Pair<TId, AssetReference>(ids[i], new AssetReference(assetGuids[indexOf])));
			}

			return list;
		}

		protected int IndexOfId(string id, IList<string> assetsPath)
		{
			for (var i = 0; i < assetsPath.Count; i++)
			{
				if (assetsPath[i].Contains($"/{id}."))
				{
					return i;
				}
			}

			return -1;
		}

		protected virtual void OnImportComplete(TScriptableObject scriptableObject) { }
	}

	/// <inheritdoc />
	public abstract class AssetsConfigsGeneratorImporter<TAsset> : IAssetConfigsGeneratorImporter
	{
		public abstract string TIdName { get; }

		public abstract string TScriptableObjectName { get; }

		public virtual bool CacheScriptAsOld => true;

		/// <inheritdoc />
		public Type ScriptableObjectType => Type.GetType(TScriptableObjectName);

		/// <inheritdoc />
		public void Import(string assetsFolderPath)
		{
			if (assetsFolderPath == null)
			{
				Debug.LogWarning("Assets folder path is null");
				return;
			}

			var type = this.GetType();
			var assetType = typeof(TAsset);
			var thisAsset = AssetDatabase.FindAssets($"{type.Name}");

			if (thisAsset.Length == 0)
			{
				Debug.LogError($"AssetsConfigsGeneratorImporter '{type.Name}' not found to generate");
				return;
			}

			var thisAssetPath = AssetDatabase.GUIDToAssetPath(thisAsset[0]);

			var assetGuids = new List<string>(AssetDatabase.FindAssets($"t:{assetType.Name}", new[] { assetsFolderPath }));
			var assetsPaths = assetGuids.ConvertAll(a => AssetDatabase.GUIDToAssetPath(a));
			var idScript = GenerateIdsScript(assetsPaths);
			var scriptableObjectScript = GenerateScriptableObjectsScript(assetType);
			var importerScript = GenerateImporterScript(type, assetType);

			if (CacheScriptAsOld)
			{
				var obj = AssetDatabase.LoadAssetAtPath<TextAsset>(thisAssetPath);

				File.WriteAllText($"Assets/{type.Name}_Old.cs", $"/*\n{obj.text}\n*/");
			}

			File.WriteAllText($"Assets/{TIdName}.cs", idScript);
			File.WriteAllText($"Assets/{TScriptableObjectName}.cs", scriptableObjectScript);
			File.WriteAllText($"Assets/{type.Name}.cs", importerScript);
			AssetDatabase.DeleteAsset(thisAssetPath);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			Debug.Log($"Finished generating the ids '{TIdName}' and importer for '{type.Name}'data type");
		}

		private string GenerateIdsScript(IList<string> ids)
		{
			var stringBuilder = new StringBuilder();

			stringBuilder.AppendLine("using System.Collections.Generic;");
			stringBuilder.AppendLine("using System.Collections.ObjectModel;");
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine("/* AUTO GENERATED CODE */");
			stringBuilder.AppendLine("namespace Game.Ids");
			stringBuilder.AppendLine("{");

			stringBuilder.AppendLine($"\tpublic enum {TIdName}");
			stringBuilder.AppendLine("\t{");
			GenerateEnums(stringBuilder, ids);
			stringBuilder.AppendLine("\t}");

			stringBuilder.AppendLine("}");

			return stringBuilder.ToString();
		}

		private void GenerateEnums(StringBuilder stringBuilder, IList<string> list)
		{
			for (var i = 0; i < list.Count; i++)
			{
				var id = list[i].Substring(list[i].LastIndexOf('/') + 1);

				stringBuilder.Append("\t\t");
				stringBuilder.Append(id.Substring(0, id.IndexOf('.')).Replace(' ', '_'));
				stringBuilder.Append(i + 1 == list.Count ? "\n" : ",\n");
			}
		}

		private string GenerateScriptableObjectsScript(Type assetType)
		{
			var stringBuilder = new StringBuilder();

			stringBuilder.AppendLine("using Geuneda.Services.AssetsImporter;");
			stringBuilder.AppendLine("using Game.Ids;");
			stringBuilder.AppendLine("using UnityEngine;");
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine("/* AUTO GENERATED CODE */");
			stringBuilder.AppendLine("namespace Game.Configs");
			stringBuilder.AppendLine("{");

			stringBuilder.AppendLine($"\tpublic class {TScriptableObjectName} : AssetConfigsScriptableObject<{TIdName}, {assetType.Name}>");
			stringBuilder.AppendLine("\t{");
			stringBuilder.AppendLine("\t}");

			stringBuilder.AppendLine("}");

			return stringBuilder.ToString();
		}

		private string GenerateImporterScript(Type importerType, Type assetType)
		{
			var stringBuilder = new StringBuilder();

			stringBuilder.AppendLine("using Game.Configs;");
			stringBuilder.AppendLine("using Game.Ids;");
			stringBuilder.AppendLine("using UnityEngine;");
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine("/* AUTO GENERATED CODE */");
			stringBuilder.AppendLine($"namespace {importerType.Namespace}");
			stringBuilder.AppendLine("{");

			stringBuilder.AppendLine($"\tpublic class {importerType.Name} : AssetsConfigsImporter<{TIdName}, {assetType.Name}, {TScriptableObjectName}>");
			stringBuilder.AppendLine("\t{");
			stringBuilder.AppendLine("\t}");

			stringBuilder.AppendLine("}");

			return stringBuilder.ToString();
		}
	}
}