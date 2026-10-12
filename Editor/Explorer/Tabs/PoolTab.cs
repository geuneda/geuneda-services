using System;
using Geuneda.Services.Pooling;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Geuneda.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// <see cref="IPoolService"/> 에 등록된 모든 풀을 표시합니다.
	/// 항목별 DespawnAll, Dispose, RemovePool 을 지원합니다.
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

			// Edit 모드(처음이든 플레이 세션이 끝난 뒤든)에서는 정적 필드에 남은 PoolService 가 있더라도
			// 풀 항목을 숨깁니다. OnExitingPlayMode() 와 함께, 사용하는 쪽 부트스트랩이 OnDestroy 에서
			// 풀 서비스 해제나 MainInstaller.Clean() 호출을 잊었더라도 Stop 이후 풀 목록이 살아 있는
			// 스냅샷을 유지하지 않도록 보장합니다.
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
				// 무시
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
				// 무시
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

		// 사용자가 Play 모드를 멈추면 풀 목록을 동기적으로 강제로 비웁니다.
		// OnDestroy 에서 IPoolService 를 해제하거나 MainInstaller.Clean() 을 호출하지 않는 부트스트랩에 대한
		// 이중 안전장치입니다. 이것이 없으면 정적 MainInstaller 때문에 다음 플레이 세션까지 지난 풀
		// (과 스폰 개수)이 표시됩니다.
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
