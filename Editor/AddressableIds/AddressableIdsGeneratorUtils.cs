using Geuneda.Services.AssetsImporter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.Assertions;

namespace Geuneda.Services.AddressableIds.Editor
{
	/// <summary>
	/// Services Explorer 의 Addressable Ids 탭에 표시하려고 <see cref="AddressableIdsGeneratorUtils.Generate"/> 가
	/// 반환하는 결과.
	/// </summary>
	internal readonly struct GenerationResult
	{
		public readonly int IdCount;
		public readonly int LabelCount;
		public readonly string OutputPath;

		public GenerationResult(int idCount, int labelCount, string outputPath)
		{
			IdCount = idCount;
			LabelCount = labelCount;
			OutputPath = outputPath;
		}
	}

	/// <summary>
	/// 현재 Addressables 상태가 <see cref="AddressableIdsEditorSettings"/> 에 기록된 마지막 생성 스냅샷과
	/// 어떻게 다른지 설명하는 <see cref="AddressableIdsGeneratorUtils.Diff"/> 의 결과. 목록은 서수(ordinal) 순으로
	/// 정렬됩니다.
	/// </summary>
	internal readonly struct DiffResult
	{
		public readonly bool HasSnapshot;
		public readonly bool FilenameChangedSinceSnapshot;
		public readonly bool LabelFilterChangedSinceSnapshot;
		public readonly int CurrentIdCount;
		public readonly int CurrentLabelCount;
		public readonly IReadOnlyList<string> AddedAddresses;
		public readonly IReadOnlyList<string> RemovedAddresses;
		public readonly IReadOnlyList<string> AddedLabels;
		public readonly IReadOnlyList<string> RemovedLabels;
		public readonly IReadOnlyList<string> SanitizedNameCollisions;
		public readonly IReadOnlyList<string> NullAssetTypeAddresses;

		public DiffResult(bool hasSnapshot, bool filenameChangedSinceSnapshot, bool labelFilterChangedSinceSnapshot,
		                  int currentIdCount, int currentLabelCount,
		                  IReadOnlyList<string> addedAddresses, IReadOnlyList<string> removedAddresses,
		                  IReadOnlyList<string> addedLabels, IReadOnlyList<string> removedLabels,
		                  IReadOnlyList<string> sanitizedNameCollisions,
		                  IReadOnlyList<string> nullAssetTypeAddresses)
		{
			HasSnapshot = hasSnapshot;
			FilenameChangedSinceSnapshot = filenameChangedSinceSnapshot;
			LabelFilterChangedSinceSnapshot = labelFilterChangedSinceSnapshot;
			CurrentIdCount = currentIdCount;
			CurrentLabelCount = currentLabelCount;
			AddedAddresses = addedAddresses;
			RemovedAddresses = removedAddresses;
			AddedLabels = addedLabels;
			RemovedLabels = removedLabels;
			SanitizedNameCollisions = sanitizedNameCollisions;
			NullAssetTypeAddresses = nullAssetTypeAddresses;
		}
	}

	/// <summary>
	/// <see cref="AddressableIdsGeneratorUtils.ComputeFreshness"/> 가 반환하는, 파일 스탯만 쓰는 가벼운 최신 여부 검사 결과.
	/// 생성된 스크립트의 마지막 수정 시각을 <c>AddressableAssetSettings</c> 와 읽기 전용이 아닌 각 그룹 에셋의
	/// 가장 최근 수정 시각과 비교합니다.
	/// </summary>
	internal readonly struct FreshnessResult
	{
		public readonly bool IsStale;
		public readonly string ScriptPath;
		public readonly bool ScriptExists;
		public readonly DateTime LatestSourceWriteTime;
		public readonly DateTime ScriptWriteTime;

		public FreshnessResult(bool isStale, string scriptPath, bool scriptExists,
		                       DateTime latestSourceWriteTime, DateTime scriptWriteTime)
		{
			IsStale = isStale;
			ScriptPath = scriptPath;
			ScriptExists = scriptExists;
			LatestSourceWriteTime = latestSourceWriteTime;
			ScriptWriteTime = scriptWriteTime;
		}
	}

	/// <summary>
	/// Addressable Ids C# 스크립트를 생성하는 순수 에디터 유틸리티입니다.
	/// <see cref="AddressableIdsTab"/> 과 <c>AddressableIdsMenu</c> 스텁에서 사용합니다.
	/// <c>[CustomEditor]</c>, <c>[MenuItem]</c>, Explorer 타입을 참조하지 않습니다.
	/// </summary>
	internal static class AddressableIdsGeneratorUtils
	{
		/// <summary>
		/// <paramref name="settings"/> 로 Addressable Ids 스크립트를 생성하고 AssetDatabase 를 갱신합니다.
		/// 나중에 비교할 수 있도록 생성한 주소/라벨 집합을 <see cref="AddressableIdsEditorSettings.RecordGeneration"/> 으로
		/// <paramref name="settings"/> 에 기록합니다.
		/// 생성 결과를 설명하는 <see cref="GenerationResult"/> 를 반환합니다.
		/// </summary>
		public static GenerationResult Generate(AddressableIdsEditorSettings settings)
		{
			var assetList = GetAssetList();

			ProcessData(assetList, settings, out var labelMap, out var paths);
			GenerateScript(assetList, settings, labelMap, paths, out var outputPath);

			AssetDatabase.Refresh();

			settings.RecordGeneration(ExtractAddresses(assetList), new List<string>(labelMap.Keys));

			return new GenerationResult(assetList.Count, labelMap.Count, outputPath);
		}

		/// <summary>
		/// 현재 Addressables 상태와 <paramref name="settings"/> 에 기록된 마지막 생성 스냅샷의 차이를 계산합니다.
		/// <see cref="Generate"/> 와 같은 <see cref="GetAssetList"/> + <see cref="ProcessData"/> 파이프라인을 실행하지만
		/// 파일을 쓰지 않고 <c>AssetDatabase.Refresh</c> 도 호출하지 않습니다. 사용자 요청 시 Services Explorer 에서
		/// 호출해도 안전합니다.
		/// </summary>
		public static DiffResult Diff(AddressableIdsEditorSettings settings)
		{
			var assetList = GetAssetList();

			ProcessData(assetList, settings, out var labelMap, out _);

			var currentAddresses = ExtractAddresses(assetList);
			var currentLabels = new List<string>(labelMap.Keys);
			currentAddresses.Sort(StringComparer.Ordinal);
			currentLabels.Sort(StringComparer.Ordinal);

			var snapshotAddresses = settings.LastGenerationAddresses;
			var snapshotLabels = settings.LastGenerationLabels;

			var added = SortedSetDiff(currentAddresses, snapshotAddresses);
			var removed = SortedSetDiff(snapshotAddresses, currentAddresses);
			var addedLabels = SortedSetDiff(currentLabels, snapshotLabels);
			var removedLabels = SortedSetDiff(snapshotLabels, currentLabels);

			var collisions = DetectSanitizedNameCollisions(assetList);
			var nullTypes = DetectNullAssetTypes(assetList);

			var hasSnapshot = settings.HasSnapshot;
			var filenameChanged = hasSnapshot &&
			                      !string.Equals(settings.LastGenerationFilenameUsed, settings.ScriptFilename, StringComparison.Ordinal);
			var labelFilterChanged = hasSnapshot &&
			                         !string.Equals(settings.LastGenerationLabelFilterUsed, settings.AddressableLabel, StringComparison.Ordinal);

			return new DiffResult(
				hasSnapshot,
				filenameChanged,
				labelFilterChanged,
				assetList.Count,
				labelMap.Count,
				added,
				removed,
				addedLabels,
				removedLabels,
				collisions,
				nullTypes);
		}

		/// <summary>
		/// 파일 스탯만 쓰는 최신 여부 검사: 생성된 스크립트의 마지막 수정 시각을 <c>AddressableAssetSettings.asset</c>
		/// 과 읽기 전용이 아닌 각 그룹 에셋과 비교합니다. 최악의 경우에도 <see cref="File.GetLastWriteTime"/> 호출 약 20 회이고
		/// Addressables 엔트리를 열거하지 않으므로 탭을 갱신할 때마다 호출해도 안전합니다.
		/// </summary>
		public static FreshnessResult ComputeFreshness(AddressableIdsEditorSettings settings)
		{
			var scriptPath = ResolveScriptPath(settings);
			var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
			var absScriptPath = Path.Combine(projectRoot, scriptPath);
			var scriptExists = File.Exists(absScriptPath);
			var scriptWriteTime = scriptExists ? File.GetLastWriteTime(absScriptPath) : default;

			var latestSource = default(DateTime);
			var assetsSettings = AddressableAssetSettingsDefaultObject.Settings;

			if (assetsSettings != null)
			{
				var settingsAssetPath = AssetDatabase.GetAssetPath(assetsSettings);
				if (!string.IsNullOrEmpty(settingsAssetPath))
				{
					var t = File.GetLastWriteTime(Path.Combine(projectRoot, settingsAssetPath));
					if (t > latestSource) latestSource = t;
				}

				foreach (var group in assetsSettings.groups)
				{
					if (group == null || group.ReadOnly)
					{
						continue;
					}

					var groupPath = AssetDatabase.GetAssetPath(group);
					if (string.IsNullOrEmpty(groupPath))
					{
						continue;
					}

					var t = File.GetLastWriteTime(Path.Combine(projectRoot, groupPath));
					if (t > latestSource) latestSource = t;
				}
			}

			var isStale = scriptExists && latestSource > scriptWriteTime;
			return new FreshnessResult(isStale, scriptPath, scriptExists, latestSource, scriptWriteTime);
		}

		/// <summary>
		/// 생성기가 쓰는(또는 쓸) 디스크 경로를 구합니다. <c>SaveScript</c> 의 규칙과 같습니다:
		/// <c>Assets/</c> 아래 어디든 설정된 파일 이름의 기존 스크립트가 있으면 그것을 쓰고, 없으면
		/// <c>Assets/&lt;ScriptFilename&gt;.cs</c> 를 씁니다.
		/// </summary>
		public static string ResolveScriptPath(AddressableIdsEditorSettings settings)
		{
			var scriptPath = $"Assets/{settings.ScriptFilename}.cs";
			var found = AssetDatabase.FindAssets($"t:Script {settings.ScriptFilename}");

			foreach (var guid in found)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);

				if (path.EndsWith($"/{settings.ScriptFilename}.cs"))
				{
					scriptPath = path;
					break;
				}
			}

			return scriptPath;
		}

		private static List<AddressableAssetEntry> GetAssetList()
		{
			var assetList = new List<AddressableAssetEntry>();
			var assetsSettings = AddressableAssetSettingsDefaultObject.Settings;

			foreach (var settingsGroup in assetsSettings.groups)
			{
				if (settingsGroup.ReadOnly)
				{
					continue;
				}

				settingsGroup.GatherAllAssets(assetList, true, true, false);
			}

			return assetList;
		}

		private static void GenerateScript(List<AddressableAssetEntry> assetList, AddressableIdsEditorSettings settings,
		                                   Dictionary<string, IList<AddressableAssetEntry>> labelMap, List<string> paths,
		                                   out string outputPath)
		{
			var stringBuilder = new StringBuilder();

			stringBuilder.AppendLine("/* AUTO GENERATED CODE */");
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine("using System.Collections.Generic;");
			stringBuilder.AppendLine("using System.Collections.ObjectModel;");
			stringBuilder.AppendLine("using Geuneda.Services.AssetsImporter;");
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine("// ReSharper disable InconsistentNaming");
			stringBuilder.AppendLine("// ReSharper disable once CheckNamespace");
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine($"namespace {settings.Namespace}");
			stringBuilder.AppendLine("{");

			stringBuilder.AppendLine($"\tpublic enum {settings.ScriptFilename}");
			stringBuilder.AppendLine("\t{");
			GenerateAddressEnums(stringBuilder, assetList);
			stringBuilder.AppendLine("\t}");

			stringBuilder.AppendLine("");
			stringBuilder.AppendLine("\tpublic enum AddressableLabel");
			stringBuilder.AppendLine("\t{");
			GenerateLabelEnums(stringBuilder, new List<string>(labelMap.Keys));
			stringBuilder.AppendLine("\t}");

			stringBuilder.AppendLine("");
			stringBuilder.AppendLine("\tpublic static class AddressablePathLookup");
			stringBuilder.AppendLine("\t{");
			GeneratePaths(stringBuilder, paths);
			stringBuilder.AppendLine("\t}");

			stringBuilder.AppendLine("");
			stringBuilder.AppendLine("\tpublic static class AddressableConfigLookup");
			stringBuilder.AppendLine("\t{");
			GenerateLoopUpMethods(stringBuilder, settings);
			GenerateLabelMap(stringBuilder, labelMap);
			GenerateConfigs(stringBuilder, assetList);
			stringBuilder.AppendLine("\t}");

			stringBuilder.AppendLine("}");

			outputPath = SaveScript(stringBuilder.ToString(), settings);
		}

		private static string SaveScript(string scriptString, AddressableIdsEditorSettings settings)
		{
			var scriptAssets = AssetDatabase.FindAssets($"t:Script {settings.ScriptFilename}");
			var scriptPath = $"Assets/{settings.ScriptFilename}.cs";

			foreach (var scriptAsset in scriptAssets)
			{
				var path = AssetDatabase.GUIDToAssetPath(scriptAsset);

				if (path.EndsWith($"/{settings.ScriptFilename}.cs"))
				{
					scriptPath = path;
					break;
				}
			}

			File.WriteAllText(scriptPath, scriptString);
			return scriptPath;
		}

		private static void GenerateLoopUpMethods(StringBuilder stringBuilder, AddressableIdsEditorSettings settings)
		{
			stringBuilder.AppendLine($"\t\tpublic static IList<{nameof(AddressableConfig)}> Configs => _addressableConfigs;");
			stringBuilder.AppendLine($"\t\tpublic static IList<string> Labels => _addressableLabels;");
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine($"\t\tpublic static {nameof(AddressableConfig)} GetConfig(this {settings.ScriptFilename} addressable)");
			stringBuilder.AppendLine("\t\t{");
			stringBuilder.AppendLine("\t\t\treturn _addressableConfigs[(int) addressable];");
			stringBuilder.AppendLine("\t\t}");
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine($"\t\tpublic static IList<{nameof(AddressableConfig)}> GetConfigs(this AddressableLabel label)");
			stringBuilder.AppendLine("\t\t{");
			stringBuilder.AppendLine("\t\t\treturn _addressableLabelMap[_addressableLabels[(int) label]];");
			stringBuilder.AppendLine("\t\t}");
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine($"\t\tpublic static IList<{nameof(AddressableConfig)}> GetConfigs(string label)");
			stringBuilder.AppendLine("\t\t{");
			stringBuilder.AppendLine("\t\t\treturn _addressableLabelMap[label];");
			stringBuilder.AppendLine("\t\t}");
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine($"\t\tpublic static string ToLabelString(this AddressableLabel label)");
			stringBuilder.AppendLine("\t\t{");
			stringBuilder.AppendLine("\t\t\treturn _addressableLabels[(int) label];");
			stringBuilder.AppendLine("\t\t}");
		}

		private static void GenerateLabelMap(StringBuilder stringBuilder, IDictionary<string, IList<AddressableAssetEntry>> assetLabelMap)
		{
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine("\t\tprivate static readonly IList<string> _addressableLabels = new List<string>");
			stringBuilder.AppendLine("\t\t{");

			if (assetLabelMap.Count > 0)
			{
				stringBuilder.AppendLine($"\t\t\t{GenerateLabels(new List<string>(assetLabelMap.Keys))}");
			}

			stringBuilder.AppendLine("\t\t}.AsReadOnly();");
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine($"\t\tprivate static readonly IReadOnlyDictionary<string, IList<{nameof(AddressableConfig)}>> _addressableLabelMap = new ReadOnlyDictionary<string, IList<{nameof(AddressableConfig)}>>(new Dictionary<string, IList<{nameof(AddressableConfig)}>>");
			stringBuilder.AppendLine("\t\t{");

			foreach (var labelMap in assetLabelMap)
			{
				stringBuilder.AppendLine($"\t\t\t{{\"{labelMap.Key}\", new List<{nameof(AddressableConfig)}>");
				stringBuilder.AppendLine("\t\t\t\t{");

				for (var i = 0; i < labelMap.Value.Count; i++)
				{
					stringBuilder.AppendLine($"\t\t\t\t\t{GenerateAddressableConfig(labelMap.Value[i], i)},");
				}

				stringBuilder.AppendLine("\t\t\t\t}.AsReadOnly()}");
			}

			stringBuilder.AppendLine("\t\t});");
		}

		private static void GeneratePaths(StringBuilder stringBuilder, IList<string> paths)
		{
			for (var i = 0; i < paths.Count; i++)
			{
				stringBuilder.AppendLine($"\t\tpublic static readonly string {GetCleanName(paths[i], false)} = \"{paths[i]}\";");
			}
		}

		private static void GenerateConfigs(StringBuilder stringBuilder, IReadOnlyList<AddressableAssetEntry> assetList)
		{
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine($"\t\tprivate static readonly IList<{nameof(AddressableConfig)}> _addressableConfigs = new List<{nameof(AddressableConfig)}>");
			stringBuilder.AppendLine("\t\t{");

			for (var i = 0; i < assetList.Count; i++)
			{
				stringBuilder.Append($"\t\t\t{GenerateAddressableConfig(assetList[i], i)}");
				stringBuilder.Append(i + 1 == assetList.Count ? "\n" : ",\n");
			}

			stringBuilder.AppendLine("\t\t}.AsReadOnly();");
		}

		private static string GenerateLabels(IList<string> labels)
		{
			var stringBuilder = new StringBuilder();

			if (labels.Count == 0)
			{
				stringBuilder.Append("\"\"");
			}

			for (var i = 0; i < labels.Count; i++)
			{
				stringBuilder.Append($"\"{labels[i]}\"");
				stringBuilder.Append(i + 1 == labels.Count ? "" : ",");
			}

			return stringBuilder.ToString();
		}

		private static string GenerateAddressableConfig(AddressableAssetEntry addressableAssetEntry, int index)
		{
			var assetType = AssetDatabase.GetMainAssetTypeAtPath(addressableAssetEntry.AssetPath);

			assetType = assetType == typeof(UnityEditor.SceneAsset)
				? typeof(UnityEngine.SceneManagement.Scene)
				: assetType;

			Assert.IsNotNull(assetType, $"Failed to get asset type for {addressableAssetEntry.AssetPath}");

			return $"new {nameof(AddressableConfig)}({index.ToString()}, \"{addressableAssetEntry.address}\", \"{addressableAssetEntry.AssetPath}\", " +
			       $"typeof({assetType}), new [] {{{GenerateLabels(new List<string>(addressableAssetEntry.labels))}}})";
		}

		private static void ProcessData(IList<AddressableAssetEntry> assetList, AddressableIdsEditorSettings settings,
		                                out Dictionary<string, IList<AddressableAssetEntry>> labelMap, out List<string> paths)
		{
			labelMap = new Dictionary<string, IList<AddressableAssetEntry>>();
			paths = new List<string>();

			for (var i = assetList.Count - 1; i > -1; --i)
			{
				// 라벨이 비어 있으면 전부 생성합니다.
				if (!string.IsNullOrEmpty(settings.AddressableLabel))
				{
					foreach (var label in assetList[i].labels)
					{
						if (label != settings.AddressableLabel)
						{
							continue;
						}

						if (!labelMap.TryGetValue(label, out var list))
						{
							list = new List<AddressableAssetEntry>();
							labelMap.Add(label, list);
						}

						list.Add(assetList[i]);
					}

					if (!assetList[i].labels.Contains(settings.AddressableLabel))
					{
						assetList.RemoveAt(i);
						continue;
					}
				}

				var address = assetList[i].address;
				var pathLastCharIndex = address.Replace('\\', '/').LastIndexOf('/');
				var path = pathLastCharIndex < 0 ? address : address.Substring(0, pathLastCharIndex);

				if (!paths.Contains(path))
				{
					paths.Add(path);
				}
			}
		}

		private static void GenerateAddressEnums(StringBuilder stringBuilder, IReadOnlyList<AddressableAssetEntry> assetList)
		{
			var addedNames = new List<string>();

			for (var i = 0; i < assetList.Count; i++)
			{
				var name = ResolveSanitizedEnumName(assetList[i].address, addedNames, out _);
				addedNames.Add(name);

				stringBuilder.Append("\t\t");
				stringBuilder.Append(GetCleanName(assetList[i].address, true));
				stringBuilder.Append(i + 1 == assetList.Count ? "\n" : ",\n");
			}
		}

		/// <summary>
		/// 주어진 Addressable <paramref name="address"/> 의 enum 멤버 이름을 구합니다. 정리한 이름이
		/// <paramref name="seenNames"/> 에 이미 있으면 <see cref="GenerateAddressEnums"/> 와 같은 <c>name_filetype</c>
		/// 대체 이름을 씁니다. 대체 이름을 썼으면 <paramref name="collided"/> 를 <c>true</c> 로 설정합니다.
		/// </summary>
		private static string ResolveSanitizedEnumName(string address, List<string> seenNames, out bool collided)
		{
			var name = GetCleanName(address, true);
			var lastDot = address.LastIndexOf('.');
			var filetype = lastDot >= 0 ? address.Substring(lastDot + 1) : string.Empty;

			collided = seenNames.Contains(name);
			return collided ? $"{name}_{filetype}" : name;
		}

		private static List<string> ExtractAddresses(IReadOnlyList<AddressableAssetEntry> assetList)
		{
			var addresses = new List<string>(assetList.Count);

			for (var i = 0; i < assetList.Count; i++)
			{
				addresses.Add(assetList[i].address);
			}

			return addresses;
		}

		private static List<string> DetectSanitizedNameCollisions(IReadOnlyList<AddressableAssetEntry> assetList)
		{
			var addedNames = new List<string>();
			var collisions = new List<string>();

			for (var i = 0; i < assetList.Count; i++)
			{
				var name = ResolveSanitizedEnumName(assetList[i].address, addedNames, out var collided);
				addedNames.Add(name);

				if (collided)
				{
					collisions.Add(assetList[i].address);
				}
			}

			collisions.Sort(StringComparer.Ordinal);
			return collisions;
		}

		private static List<string> DetectNullAssetTypes(IReadOnlyList<AddressableAssetEntry> assetList)
		{
			var nulls = new List<string>();

			for (var i = 0; i < assetList.Count; i++)
			{
				var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetList[i].AssetPath);

				if (assetType == null)
				{
					nulls.Add(assetList[i].address);
				}
			}

			nulls.Sort(StringComparer.Ordinal);
			return nulls;
		}

		/// <summary>
		/// <paramref name="left"/> 에는 있고 <paramref name="right"/> 에는 없는 요소를 반환합니다.
		/// 두 입력은 반드시 서수(ordinal) 순으로 미리 정렬되어 있어야 하며, 출력도 서수 순으로 정렬됩니다.
		/// </summary>
		private static List<string> SortedSetDiff(IReadOnlyList<string> left, IReadOnlyList<string> right)
		{
			var result = new List<string>();
			var i = 0;
			var j = 0;

			while (i < left.Count && j < right.Count)
			{
				var cmp = string.CompareOrdinal(left[i], right[j]);

				if (cmp < 0)
				{
					result.Add(left[i]);
					i++;
				}
				else if (cmp > 0)
				{
					j++;
				}
				else
				{
					i++;
					j++;
				}
			}

			while (i < left.Count)
			{
				result.Add(left[i]);
				i++;
			}

			return result;
		}

		private static void GenerateLabelEnums(StringBuilder stringBuilder, IList<string> labels)
		{
			for (var i = 0; i < labels.Count; i++)
			{
				stringBuilder.Append("\t\tLabel_");
				stringBuilder.Append(GetCleanName(labels[i], true));
				stringBuilder.Append(i + 1 == labels.Count ? "\n" : ",\n");
			}
		}

		private static string GetCleanName(string name, bool withUnderscore)
		{
			var index = name.LastIndexOf('.');
			var charReplace = withUnderscore ? "_" : "";

			name = index < 0 ? name : name.Substring(0, index);
			name = name.Replace("/", charReplace);
			name = name.Replace("\\", charReplace);
			name = name.Replace(" ", charReplace);
			name = name.Replace("-", charReplace);

			return name;
		}
	}
}
