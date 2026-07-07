using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Geuneda.Services.Editor.Explorer.Tabs;

namespace Geuneda.Services.Editor.Explorer
{
	/// <summary>
	/// Main Services Explorer dockable window.
	/// Open via <c>Tools &gt; Geuneda &gt; Services Explorer</c>.
	/// </summary>
	public class ServicesExplorerWindow : EditorWindow
	{
		private const string SelectedTabPrefKey = "Geuneda.ServicesExplorer.SelectedTab";
		private const float MinWidth = 640f;
		private const float MinHeight = 400f;

		private TabView _tabView;
		private readonly List<ServiceTab> _tabs = new List<ServiceTab>();

		[MenuItem("Tools/Geuneda/Services Explorer")]
		public static void Open()
		{
			var window = GetWindow<ServicesExplorerWindow>();

			window.titleContent = new GUIContent("Services Explorer");
			window.minSize = new Vector2(MinWidth, MinHeight);
			window.Show();
		}

		/// <summary>
		/// Opens the Services Explorer window and navigates to the tab matching <typeparamref name="T"/>.
		/// </summary>
		public static ServicesExplorerWindow OpenOnTab<T>() where T : ServiceTab
		{
			var window = GetWindow<ServicesExplorerWindow>();

			window.titleContent = new GUIContent("Services Explorer");
			window.minSize = new Vector2(MinWidth, MinHeight);
			window.Show();
			window.SelectTab<T>();

			return window;
		}

		/// <summary>
		/// Navigates to the tab matching <typeparamref name="T"/>. No-ops if no such tab is registered.
		/// </summary>
		public void SelectTab<T>() where T : ServiceTab
		{
			for (var i = 0; i < _tabs.Count; i++)
			{
				if (_tabs[i] is T)
				{
					_tabView.activeTab = _tabView[i] as Tab;
					return;
				}
			}
		}

		private void CreateGUI()
		{
			var guids = AssetDatabase.FindAssets("ServicesExplorerWindow t:VisualTreeAsset");

			if (guids.Length > 0)
			{
				var uxmlPath = AssetDatabase.GUIDToAssetPath(guids[0]);
				var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);

				if (visualTree != null)
				{
					visualTree.CloneTree(rootVisualElement);
				}
			}

			_tabView = rootVisualElement.Q<TabView>("service-tab-view");

			if (_tabView == null)
			{
				_tabView = new TabView { name = "service-tab-view" };
				_tabView.style.flexGrow = 1;
				rootVisualElement.Add(_tabView);
			}

			RegisterTabs();
			RestoreSelectedTab();

			_tabView.activeTabChanged += OnActiveTabChanged;
		}

		private void RegisterTabs()
		{
			_tabs.Clear();

			AddTab(new OverviewTab(this));
			AddTab(new VersioningTab());
			AddTab(new InstallerTab());
			AddTab(new MessageBrokerTab());
			AddTab(new TickTab());
			AddTab(new CoroutineTab());
			AddTab(new PoolTab());
			AddTab(new DataTab());
			AddTab(new TimeTab());
			AddTab(new RngTab());
			AddTab(new AssetResolverTab());
			AddTab(new AssetsImporterTab());
			AddTab(new AddressableIdsTab());
		}

		private void AddTab(ServiceTab serviceTab)
		{
			var tab = new Tab(serviceTab.DisplayName);

			tab.Add(serviceTab);
			_tabView.Add(tab);
			_tabs.Add(serviceTab);
		}

		private void RestoreSelectedTab()
		{
			var savedIndex = EditorPrefs.GetInt(SelectedTabPrefKey, 0);

			if (savedIndex >= 0 && savedIndex < _tabView.childCount)
			{
				_tabView.activeTab = _tabView[savedIndex] as Tab;
			}
		}

		private void OnActiveTabChanged(Tab previous, Tab current)
		{
			var index = _tabView.IndexOf(current);

			if (index >= 0)
			{
				EditorPrefs.SetInt(SelectedTabPrefKey, index);
			}
		}

		private void OnDisable()
		{
			if (_tabView != null)
			{
				_tabView.activeTabChanged -= OnActiveTabChanged;
			}
		}
	}
}
