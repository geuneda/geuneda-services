using System;
using Geuneda.Services.Pooling;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Geuneda.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// Displays all pools registered in <see cref="IPoolService"/>.
	/// Supports DespawnAll, Dispose, and RemovePool per entry.
	/// </summary>
	public class PoolTab : ServiceTab
	{
		public override string DisplayName => "Pool";

		private ScrollView _scroll;
		private VisualElement _list;
		private Label _countLabel;

		protected override void BuildUi()
		{
			var header = new VisualElement();
			header.AddToClassList("tab-header-row");
			_countLabel = new Label("Pools: 0");
			_countLabel.AddToClassList("tab-section-label");
			header.Add(_countLabel);
			Add(header);

		_scroll = new ScrollView(ScrollViewMode.Vertical);
		_scroll.AddToClassList("tab-scroll");
		_list = new VisualElement();
		_scroll.Add(_list);
		Add(_scroll);

		var bar = MakeActionBar();
		bar.Add(MakePrimaryButton("Clear All Pools", OnClearAllPools));
		Add(bar);
	}

		protected override void Refresh()
		{
			_list.Clear();

			// Hide pool entries in edit mode (initial OR after a play session ended)
			// regardless of any leftover PoolService kept alive by a static field.
			// Together with OnExitingPlayMode() this guarantees the pool list does not
			// retain a live snapshot after Stop, even if the consumer's bootstrap forgot
			// to dispose the pool service / call MainInstaller.Clean() in OnDestroy.
			if (!EditorApplication.isPlaying)
			{
				_countLabel.text = "Pools: 0";
				_list.Add(MakeEmptyLabel());
				return;
			}

			var poolService = TryResolve<IPoolService>() as PoolService;

			if (poolService == null)
			{
				_countLabel.text = "IPoolService not bound";
				_list.Add(MakeEmptyLabel("IPoolService not bound"));
				return;
			}

			var pools = poolService.Pools;
			_countLabel.text = $"Pools: {pools.Count}";

			if (pools.Count == 0)
			{
				_list.Add(MakeEmptyLabel());
				return;
			}

			foreach (var kvp in pools)
			{
				var entityType = kvp.Key;
				var pool = kvp.Value;
				var spawnedCount = GetSpawnedCount(pool, entityType);
				var sampleInfo = GetSampleInfo(pool, entityType);

				var row = MakeRow(entityType.Name, $"spawned={spawnedCount}  sample={sampleInfo}");

				var despawnBtn = MakeRowButton("DespawnAll", () =>
				{
					pool.DespawnAll();
					Refresh();
				});
				row.Add(despawnBtn);

				var pingBtn = MakeRowButton("Ping", () => PingSample(pool, entityType));
				row.Add(pingBtn);

				var removeBtn = MakeRowButton("RemovePool", () => OnRemovePool(poolService, entityType), danger: true);
				row.Add(removeBtn);

				var disposeBtn = MakeRowButton("Dispose", () => OnDispose(pool, poolService, entityType), danger: true);
				row.Add(disposeBtn);

				_list.Add(row);
			}
		}

		private static int GetSpawnedCount(IObjectPool pool, Type entityType)
		{
			try
			{
				var genericInterface = typeof(IObjectPool<>).MakeGenericType(entityType);
				var prop = genericInterface.GetProperty("SpawnedReadOnly");
				var value = prop?.GetValue(pool);

				if (value is System.Collections.ICollection col)
				{
					return col.Count;
				}
			}
			catch
			{
				// ignored
			}

			return -1;
		}

		private static string GetSampleInfo(IObjectPool pool, Type entityType)
		{
			try
			{
				var genericInterface = typeof(IObjectPool<>).MakeGenericType(entityType);
				var prop = genericInterface.GetProperty("SampleEntity");
				var sample = prop?.GetValue(pool);

				return sample != null ? sample.GetType().Name : "null";
			}
			catch
			{
				return "?";
			}
		}

		private static void PingSample(IObjectPool pool, Type entityType)
		{
			try
			{
				var genericInterface = typeof(IObjectPool<>).MakeGenericType(entityType);
				var prop = genericInterface.GetProperty("SampleEntity");
				var sample = prop?.GetValue(pool);

				if (sample is UnityEngine.Object unityObj)
				{
					EditorGUIUtility.PingObject(unityObj);
				}
			}
			catch
			{
				// ignored
			}
		}

		private void OnRemovePool(PoolService poolService, Type entityType)
		{
			if (!EditorUtility.DisplayDialog("Remove Pool",
				$"Remove pool for {entityType.Name}? (does not dispose)", "Remove", "Cancel"))
			{
				return;
			}

			var method = typeof(PoolService).GetMethod("RemovePool")?.MakeGenericMethod(entityType);
			method?.Invoke(poolService, null);
			Refresh();
		}

		// Forcibly clear the pool list synchronously when the user stops play mode.
		// Belt-and-braces against bootstraps that fail to dispose IPoolService / call
		// MainInstaller.Clean() in OnDestroy — without this the static MainInstaller
		// would surface stale pools (and their spawned-readonly counts) until the next
		// play session.
		protected override void OnExitingPlayMode()
		{
			_countLabel.text = "Pools: 0";
			_list.Clear();
			_list.Add(MakeEmptyLabel());
		}

		private void OnClearAllPools()
	{
		var poolService = TryResolve<IPoolService>() as PoolService;

		if (poolService == null)
		{
			return;
		}

		if (!EditorUtility.DisplayDialog("Clear All Pools",
			"Call DespawnAll on every registered pool?", "Clear All", "Cancel"))
		{
			return;
		}

		foreach (var pool in poolService.Pools.Values)
		{
			pool.DespawnAll();
		}

		Refresh();
	}

	private void OnDispose(IObjectPool pool, PoolService poolService, Type entityType)
		{
			if (!EditorUtility.DisplayDialog("Dispose Pool",
				$"Dispose pool for {entityType.Name}? Pooled instances will be destroyed.", "Dispose", "Cancel"))
			{
				return;
			}

			try
			{
				pool.Dispose();
			}
			catch (Exception e)
			{
				Debug.LogError($"[ServicesExplorer] Pool.Dispose threw: {e.Message}");
			}

			var removeMethod = typeof(PoolService).GetMethod("RemovePool")?.MakeGenericMethod(entityType);
			removeMethod?.Invoke(poolService, null);
			Refresh();
		}
	}
}
