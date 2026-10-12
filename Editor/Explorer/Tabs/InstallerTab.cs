using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Geuneda.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// <see cref="MainInstaller"/> 가 가진 모든 바인딩을 표시합니다.
	/// 바인딩별 Clean / CleanDispose 와 일괄 Clean 을 지원합니다.
	/// </summary>
	public class InstallerTab : ServiceTab
	{
		public override string DisplayName => "Installer";

		private ScrollView _scroll;
		private VisualElement _list;
		private VisualElement _actionBar;

		protected override void BuildUi()
		{
			_scroll = new ScrollView(ScrollViewMode.Vertical);
			_scroll.AddToClassList("tab-scroll");

			_list = new VisualElement();
			_scroll.Add(_list);
			Add(_scroll);

		_actionBar = MakeActionBar();
		_actionBar.Add(MakePrimaryDangerButton("Clean All", OnCleanAll));
		Add(_actionBar);
		}

		protected override void Refresh()
		{
			_list.Clear();

			// Edit 모드(처음이든 플레이 세션이 끝난 뒤든)에서는 MainInstaller 에 정적 상태가 남아 있더라도
			// 바인딩 목록을 숨깁니다. OnExitingPlayMode() 와 함께, 사용하는 쪽 부트스트랩이 OnDestroy 에서
			// MainInstaller.Clean() 호출을 잊었더라도 Stop 이후 탭이 살아 있는 스냅샷을 유지하지 않도록
			// 보장합니다. Play 모드 상황은 배너로 알립니다.
			if (!UnityEditor.EditorApplication.isPlaying)
			{
				_list.Add(MakeEmptyLabel());
				return;
			}

			var installer = MainInstaller.InstallerInstance;

			if (installer == null)
			{
				_list.Add(MakeEmptyLabel("MainInstaller not available"));
				return;
			}

			var bindings = installer.Bindings;

			if (bindings.Count == 0)
			{
				_list.Add(MakeEmptyLabel());
				return;
			}

			foreach (var kvp in bindings)
			{
				var interfaceType = kvp.Key;
				var instance = kvp.Value;
				var row = MakeRow(interfaceType.Name, instance.GetType().Name);

				var cleanBtn = MakeRowButton("Clean", () => OnClean(interfaceType));
				row.Add(cleanBtn);

				if (instance is IDisposable)
				{
					var disposeBtn = MakeRowButton("CleanDispose", () => OnCleanDispose(interfaceType, (IDisposable)instance), danger: true);
					row.Add(disposeBtn);
				}

				_list.Add(row);
			}
		}

		// 사용자가 Play 모드를 멈추면 바인딩 목록을 동기적으로 강제로 비웁니다.
		// OnDestroy 에서 MainInstaller.Clean() 을 호출하지 않는 부트스트랩에 대한 이중 안전장치입니다.
		// 이것이 없으면 정적 MainInstaller 때문에 다음 플레이 세션까지 지난 바인딩이 탭에 표시됩니다.
		protected override void OnExitingPlayMode()
		{
			_list.Clear();
			_list.Add(MakeEmptyLabel());
		}

		private void OnClean(Type interfaceType)
		{
			if (!EditorUtility.DisplayDialog("Clean Binding",
				$"Remove binding for {interfaceType.Name} from MainInstaller?", "Remove", "Cancel"))
			{
				return;
			}

			var method = typeof(MainInstaller).GetMethod("Clean", new Type[0])?.MakeGenericMethod(interfaceType);
			method?.Invoke(null, null);
			Refresh();
		}

		private void OnCleanDispose(Type interfaceType, IDisposable instance)
		{
			if (!EditorUtility.DisplayDialog("CleanDispose Binding",
				$"Dispose and remove binding for {interfaceType.Name}?", "Dispose & Remove", "Cancel"))
			{
				return;
			}

			try
			{
				instance.Dispose();
			}
			catch (Exception e)
			{
				Debug.LogError($"[ServicesExplorer] Dispose threw: {e.Message}");
			}

			var method = typeof(MainInstaller).GetMethod("Clean", new Type[0])?.MakeGenericMethod(interfaceType);
			method?.Invoke(null, null);
			Refresh();
		}

		private void OnCleanAll()
		{
			if (!EditorUtility.DisplayDialog("Clean All Bindings",
				"Remove ALL bindings from MainInstaller? This may break running game logic.", "Clean All", "Cancel"))
			{
				return;
			}

			MainInstaller.Clean();
			Refresh();
		}
	}
}
