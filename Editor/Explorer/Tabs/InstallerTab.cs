using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Geuneda.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// Displays all bindings held by <see cref="MainInstaller"/>.
	/// Supports per-binding Clean / CleanDispose and bulk Clean.
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

			// In edit mode (initial OR after a play session ended) the populated bindings
			// list is hidden, regardless of whatever static state MainInstaller still holds.
			// Together with OnExitingPlayMode() this guarantees that the tab does not retain
			// a live snapshot after Stop, even if the consumer's bootstrap forgot to call
			// MainInstaller.Clean() in OnDestroy. The banner conveys play-mode context.
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

		// Forcibly clear the bindings list synchronously when the user stops play mode.
		// Belt-and-braces against bootstraps that fail to call MainInstaller.Clean() in
		// OnDestroy — without this the static MainInstaller would surface stale bindings
		// in the tab until the next play session.
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
