using System;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Geuneda.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// <see cref="IDataService"/> 가 가진 모든 데이터 항목을 표시합니다.
	/// Save, Load, 저장소에 저장된 값 삭제를 지원합니다.
	/// </summary>
	public class DataTab : ServiceTab
	{
		public override string DisplayName => "Data";

		private ScrollView _scroll;
		private VisualElement _list;
		private Label _countLabel;

		protected override void BuildUi()
		{
			var header = new VisualElement();
			header.AddToClassList("tab-header-row");
			_countLabel = new Label("Entries: 0");
			_countLabel.AddToClassList("tab-section-label");
			header.Add(_countLabel);
			Add(header);

		_scroll = new ScrollView(ScrollViewMode.Vertical);
		_scroll.AddToClassList("tab-scroll");
		_list = new VisualElement();
		_scroll.Add(_list);
		Add(_scroll);

		var bar = MakeActionBar();
		bar.Add(MakePrimaryButton("Save All Data", OnSaveAll));
		Add(bar);
	}

		protected override void Refresh()
		{
			_list.Clear();

			// Edit 모드(처음이든 플레이 세션이 끝난 뒤든)에서는 정적 필드에 남은 DataService 가 있더라도
			// 항목을 숨깁니다. OnExitingPlayMode() 와 함께, 사용하는 쪽 부트스트랩이 OnDestroy 에서
			// MainInstaller.Clean() 호출을 잊었더라도 Stop 이후 데이터 목록이 살아 있는 스냅샷을 유지하지
			// 않도록 보장합니다.
			if (!EditorApplication.isPlaying)
			{
				_countLabel.text = "Entries: 0";
				_list.Add(MakeEmptyLabel());
				return;
			}

			var dataService = TryResolve<IDataService>() as DataService;

			if (dataService == null)
			{
				_countLabel.text = "IDataService not bound";
				_list.Add(MakeEmptyLabel("IDataService not bound"));
				return;
			}

			var entries = dataService.DataEntries;
			_countLabel.text = $"Entries: {entries.Count}";

			if (entries.Count == 0)
			{
				_list.Add(MakeEmptyLabel());
				return;
			}

			foreach (var kvp in entries)
			{
				var dataType = kvp.Key;
				var data = kvp.Value;
				var json = TrySerializeJson(data);
				var storageKey = dataService.GetStorageKey(dataType);
				var isSaved = dataService.Storage.HasKey(storageKey);

				var container = new VisualElement();
				container.style.marginBottom = 6;

				// 이름이 같은 타입을 구별할 수 있도록 저장 키(접두사 + FullName)를 표시한다
				var row = MakeRow(storageKey, isSaved ? "[saved]" : "[in-memory only]");

				var saveBtn = MakeRowButton("Save", () => OnSave(dataService, dataType));
				row.Add(saveBtn);

				var loadBtn = MakeRowButton("Load", () => { OnLoad(dataService, dataType); Refresh(); });
				row.Add(loadBtn);

				var deleteBtn = MakeRowButton("Del Saved", () => OnDeleteSaved(dataService, storageKey), danger: true);
				row.Add(deleteBtn);

				container.Add(row);

				if (!string.IsNullOrEmpty(json))
				{
					var jsonLabel = new Label(json);
					jsonLabel.AddToClassList("json-preview");
					container.Add(jsonLabel);
				}

				_list.Add(container);
			}
		}

		// 사용자가 Play 모드를 멈추면 데이터 항목 위젯을 동기적으로 강제로 비웁니다.
		// OnDestroy 에서 MainInstaller.Clean() 을 호출하지 않는 부트스트랩에 대한 이중 안전장치입니다.
		// DataService 항목 딕셔너리는 서비스 인스턴스에 남아 있어, 그렇지 않으면 Edit 모드에서 지난
		// 스냅샷으로 표시됩니다.
		protected override void OnExitingPlayMode()
		{
			_countLabel.text = "Entries: 0";
			_list.Clear();
			_list.Add(MakeEmptyLabel());
		}

		private void OnSaveAll()
	{
		var dataService = TryResolve<IDataService>();
		dataService?.SaveAllData();
		Refresh();
	}

	private static string TrySerializeJson(object data)
		{
			try
			{
				return JsonConvert.SerializeObject(data, Formatting.Indented);
			}
			catch
			{
				return null;
			}
		}

		private void OnSave(DataService dataService, Type dataType)
		{
			try
			{
				var method = typeof(DataService)
					.GetMethod(nameof(IDataService.SaveData))
					?.MakeGenericMethod(dataType);
				method?.Invoke(dataService, null);
				Refresh();
			}
			catch (Exception e)
			{
				Debug.LogError($"[ServicesExplorer] SaveData threw: {e.Message}");
			}
		}

		private void OnLoad(DataService dataService, Type dataType)
		{
			try
			{
				var method = typeof(DataService)
					.GetMethod(nameof(IDataService.LoadData))
					?.MakeGenericMethod(dataType);
				method?.Invoke(dataService, null);
			}
			catch (Exception e)
			{
				Debug.LogError($"[ServicesExplorer] LoadData threw: {e.Message}");
			}
		}

		private void OnDeleteSaved(DataService dataService, string key)
		{
			if (!EditorUtility.DisplayDialog("Delete Saved Data",
				$"Delete the saved value \"{key}\" from the service's storage? This cannot be undone.", "Delete", "Cancel"))
			{
				return;
			}

			dataService.Storage.Delete(key);
			Refresh();
		}
	}
}
