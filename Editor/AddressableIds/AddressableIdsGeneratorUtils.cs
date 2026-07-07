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
	/// Result returned by <see cref="AddressableIdsGeneratorUtils.Generate"/> for display in the
	/// Services Explorer Addressable Ids tab.
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
	/// Result returned by <see cref="AddressableIdsGeneratorUtils.Diff"/> describing how the current
	/// Addressables state differs from the last-generation snapshot recorded on
	/// <see cref="AddressableIdsEditorSettings"/>. Lists are sorted ordinally.
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
	/// Cheap, file-stat-only freshness probe returned by <see cref="AddressableIdsGeneratorUtils.ComputeFreshness"/>.
	/// Compares the generated script's last-write-time against the latest write-time of
	/// <c>AddressableAssetSettings</c> and each non-readonly group asset.
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
	/// Pure editor utility that generates the Addressable Ids C# script.
	/// Used by <see cref="AddressableIdsTab"/> and the <c>AddressableIdsMenu</c> stubs.
	/// Does not reference any <c>[CustomEditor]</c>, <c>[MenuItem]</c>, or Explorer types.
	/// </summary>
	internal static class AddressableIdsGeneratorUtils
	{
		/// <summary>
		/// Generates the Addressable Ids script using <paramref name="settings"/> and refreshes the AssetDatabase.
		/// Records the generated address/label set on <paramref name="settings"/> via
		/// <see cref="AddressableIdsEditorSettings.RecordGeneration"/> for later diffing.
		/// Returns a <see cref="GenerationResult"/> describing the generated output.
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
		/// Computes a diff between the current Addressables state and the last-generation snapshot
		/// recorded on <paramref name="settings"/>. Runs the same <see cref="GetAssetList"/> +
		/// <see cref="ProcessData"/> pipeline that <see cref="Generate"/> uses, but does not write any
		/// files and does not call <c>AssetDatabase.Refresh</c>. Safe to call from the Services Explorer
		/// on user demand.
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
		/// File-stat-only freshness check: compares the generated script's last-write-time against
		/// <c>AddressableAssetSettings.asset</c> plus each non-readonly group asset. Worst case ~20
		/// <see cref="File.GetLastWriteTime"/> calls and no Addressables entry enumeration, so it is
		/// safe to call on every tab refresh.
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
		/// Resolves the on-disk path the generator writes to (or would write to). Mirrors the
		/// <c>SaveScript</c> resolution rule: prefer an existing script with the configured filename
		/// anywhere under <c>Assets/</c>, fall back to <c>Assets/&lt;ScriptFilename&gt;.cs</c>.
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
				// Empty label means generate everything.
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
		/// Resolves the enum-member name for a given Addressable <paramref name="address"/>, applying the
		/// same <c>name_filetype</c> fallback that <see cref="GenerateAddressEnums"/> uses when the cleaned
		/// name collides with one already in <paramref name="seenNames"/>. Sets <paramref name="collided"/>
		/// to <c>true</c> when the fallback path was taken.
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
		/// Returns the elements of <paramref name="left"/> that are not in <paramref name="right"/>.
		/// Both inputs MUST be pre-sorted ordinally; output is also sorted ordinally.
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
