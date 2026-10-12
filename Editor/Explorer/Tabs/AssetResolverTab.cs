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
	/// <see cref="IAssetResolverService"/> 가 가진 AssetMap 트리를 표시합니다.
	/// 파괴적인 언로드 동작은 토글을 켜야 사용할 수 있습니다.
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
		// 파괴적 동작 토글은 재구성 경로에서 행별 Unload 버튼 표시 여부를 정하므로, 바꿀 때 다이제스트를
		// 무효화해야 합니다. 그렇지 않으면 다음 Refresh 가 같은 데이터로 보고 건너뛰어 버튼이 그려지지 않습니다.
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

			// 바뀐 것이 없으면 재구성을 건너뜁니다(ServiceTab.TryShortCircuitRefresh 참고).
			// 주기 타이머가 클릭 도중 마우스를 캡처한 VisualElement 를 파괴해 빠른 폴드아웃 클릭이
			// 사라지는 것을 막아 줍니다.
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

				// 주기 Refresh 가 틱마다 다시 펼치지 않도록 상태가 유지되는 폴드아웃을 씁니다.
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
		/// 재구성 경로가 그리는 모든 상태로 결정적 다이제스트를 만듭니다: 미바인딩 분기, 파괴적 동작 토글
		/// 플래그(행별 Unload 버튼 표시 여부), 행별 <c>(assetType, idType, id, loaded)</c> 튜플. 연속된 두 갱신의
		/// 다이제스트가 같으면 재구성을 건너뛰어 250 ms 타이머가 빠른 폴드아웃 클릭을 클릭 도중 파괴하지 않게 합니다.
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
