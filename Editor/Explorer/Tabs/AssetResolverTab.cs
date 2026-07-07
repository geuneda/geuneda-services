using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;

namespace Geuneda.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// Displays the AssetMap tree held by <see cref="IAssetResolverService"/>.
	/// Destructive unload actions are gated behind a toggle.
	/// </summary>
	public class AssetResolverTab : ServiceTab
	{
		public override string DisplayName => "Asset Resolver";

		private ScrollView _scroll;
		private VisualElement _tree;
		private Toggle _destructiveToggle;
		private Label _countLabel;

		protected override void BuildUi()
		{
			var header = new VisualElement();
			header.AddToClassList("tab-header-row");
			_countLabel = new Label("Asset types: 0");
			_countLabel.AddToClassList("tab-section-label");
			header.Add(_countLabel);

		_destructiveToggle = new Toggle("Enable destructive actions");
		_destructiveToggle.AddToClassList("destructive-toggle");
		_destructiveToggle.value = false;
		// The destructive toggle gates per-row Unload buttons in the rebuild path, so
		// flipping it must invalidate the digest — otherwise the next Refresh sees
		// matching data and short-circuits, leaving the buttons un-rendered.
		_destructiveToggle.RegisterValueChangedCallback(_ => InvalidateRefreshDigest());
		header.Add(_destructiveToggle);
		Add(header);

		_scroll = new ScrollView(ScrollViewMode.Vertical);
		_scroll.AddToClassList("tab-scroll");
		_tree = new VisualElement();
		_scroll.Add(_tree);
		Add(_scroll);

		var bar = MakeActionBar();
		bar.Add(MakePrimaryButton("Unload All", OnUnloadAll));
		Add(bar);
	}

		protected override void Refresh()
		{
			var resolver = TryResolve<IAssetResolverService>() as AssetResolverService;
			var digest = ComputeDigest(resolver);

			// Skip rebuild if nothing changed — see ServiceTab.TryShortCircuitRefresh.
			// This is what keeps rapid foldout clicks from getting eaten by the periodic
			// timer destroying mouse-captured VisualElements mid-click.
			if (TryShortCircuitRefresh(digest))
			{
				return;
			}

			_tree.Clear();

			if (resolver == null)
			{
				_countLabel.text = "IAssetResolverService not bound";
				_tree.Add(MakeEmptyLabel("IAssetResolverService not bound"));
				return;
			}

			var assetMap = resolver.AssetMap;
			_countLabel.text = $"Asset types: {assetMap.Count}";

			if (assetMap.Count == 0)
			{
				_tree.Add(MakeEmptyLabel());
				return;
			}

			foreach (var assetKvp in assetMap)
			{
				var assetType = assetKvp.Key;
				var idMap = assetKvp.Value;

				// Sticky foldouts so the periodic Refresh doesn't re-expand on every tick.
				var assetFoldout = MakeStickyFoldout(
					key: assetType.FullName ?? assetType.Name,
					text: assetType.Name);
				assetFoldout.AddToClassList("section-foldout");

				foreach (var idKvp in idMap)
				{
					var idType = idKvp.Key;
					var dictionary = idKvp.Value as IDictionary;

					var idFoldout = MakeStickyFoldout(
						key: $"{assetType.FullName ?? assetType.Name}/{idType.FullName ?? idType.Name}",
						text: $"Id: {idType.Name}  ({dictionary?.Count ?? 0})");
					idFoldout.AddToClassList("section-foldout");

					if (dictionary != null)
					{
						foreach (DictionaryEntry entry in dictionary)
						{
							var id = entry.Key;
							var assetRef = entry.Value as AssetReference;
							var loaded = assetRef != null && assetRef.IsValid();
							var status = loaded ? "loaded" : "not loaded";
							var row = MakeRow(id.ToString(), status);

							if (loaded && _destructiveToggle.value)
							{
								var capturedId = id;
								var capturedIdType = idType;
								var capturedAssetType = assetType;
								var unloadBtn = MakeRowButton("Unload", () =>
								{
									OnUnload(resolver, capturedAssetType, capturedIdType, capturedId);
								}, danger: true);
								row.Add(unloadBtn);
							}

							idFoldout.Add(row);
						}
					}

					assetFoldout.Add(idFoldout);
				}

				_tree.Add(assetFoldout);
			}
		}

		/// <summary>
		/// Builds a deterministic digest of every piece of state the rebuild path renders:
		/// the not-bound branch, the destructive-toggle flag (gates per-row Unload buttons),
		/// and per-row <c>(assetType, idType, id, loaded)</c> tuples. When two consecutive
		/// refreshes produce the same digest the rebuild can be skipped — keeping rapid
		/// foldout clicks from getting destroyed mid-click by the 250 ms timer.
		/// </summary>
		private string ComputeDigest(AssetResolverService resolver)
		{
			if (resolver == null)
			{
				return "<unbound>";
			}

			var sb = new StringBuilder();
			sb.Append(_destructiveToggle != null && _destructiveToggle.value ? "DST|" : "---|");

			foreach (var assetKvp in resolver.AssetMap)
			{
				sb.Append(assetKvp.Key.FullName ?? assetKvp.Key.Name).Append('{');
				foreach (var idKvp in assetKvp.Value)
				{
					sb.Append(idKvp.Key.FullName ?? idKvp.Key.Name).Append('[');
					if (idKvp.Value is IDictionary dict)
					{
						foreach (DictionaryEntry e in dict)
						{
							sb.Append(e.Key);
							var assetRef = e.Value as AssetReference;
							sb.Append(assetRef != null && assetRef.IsValid() ? "=1," : "=0,");
						}
					}
					sb.Append(']');
				}
				sb.Append('}');
			}
			return sb.ToString();
		}

		private void OnUnloadAll()
	{
		if (!_destructiveToggle.value)
		{
			Debug.LogWarning("[ServicesExplorer] Enable destructive actions first to use Unload All.");
			return;
		}

		var resolver = TryResolve<IAssetResolverService>() as AssetResolverService;

		if (resolver == null)
		{
			return;
		}

		if (!EditorUtility.DisplayDialog("Unload All Assets",
			"Unload all registered assets? This cannot be undone.", "Unload All", "Cancel"))
		{
			return;
		}

		foreach (var assetKvp in resolver.AssetMap)
		{
			var assetType = assetKvp.Key;

			foreach (var idKvp in assetKvp.Value)
			{
				var idType = idKvp.Key;
				var dictionary = idKvp.Value as System.Collections.IDictionary;

				if (dictionary == null)
				{
					continue;
				}

				foreach (DictionaryEntry entry in dictionary)
				{
					OnUnload(resolver, assetType, idType, entry.Key);
				}
			}
		}

		Refresh();
	}

	private void OnUnload(AssetResolverService resolver, Type assetType, Type idType, object id)
		{
			try
			{
				var method = typeof(AssetResolverService)
					.GetMethod("UnloadAssets", new[] { typeof(bool), idType.MakeArrayType() });
				if (method == null)
				{
					return;
				}

				var genericMethod = method.MakeGenericMethod(idType, assetType);
				var idArray = Array.CreateInstance(idType, 1);
				idArray.SetValue(id, 0);
				genericMethod.Invoke(resolver, new object[] { true, idArray });
				Refresh();
			}
			catch (Exception e)
			{
				Debug.LogError($"[ServicesExplorer] UnloadAssets threw: {e.Message}");
			}
		}
	}
}
