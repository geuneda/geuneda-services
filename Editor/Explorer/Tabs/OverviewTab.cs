using System;
using System.IO;
using Geuneda.Services.AssetsImporter.Editor;
using Geuneda.Services.AddressableIds.Editor;
using Geuneda.Services.Pooling;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Geuneda.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// Landing tab for the Services Explorer.
	/// Renders a card grid with one card per service tab; each card shows the bound/ready
	/// status and exposes an <c>Open</c> jump-link and the tab's primary CTA.
	/// </summary>
	public class OverviewTab : ServiceTab
	{
		public override string DisplayName => "Overview";
		protected override int RefreshIntervalMs => 1000;

		private readonly ServicesExplorerWindow _window;
		private VisualElement _grid;

		public OverviewTab(ServicesExplorerWindow window)
		{
			_window = window;
		}

		protected override void BuildUi()
		{
			var scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.AddToClassList("tab-scroll");

			_grid = new VisualElement();
			_grid.AddToClassList("overview-grid");
			scroll.Add(_grid);

			Add(scroll);
		}

		protected override void Refresh()
		{
			_grid.Clear();

			// ---- Tool tabs ----
			_grid.Add(BuildVersioningCard());
			_grid.Add(BuildAssetsImporterCard());
			_grid.Add(BuildAddressableIdsCard());

			// ---- Service tabs (runtime-bound) ----
			_grid.Add(BuildInstallerCard());
			_grid.Add(BuildMessageBrokerCard());
			_grid.Add(BuildTickCard());
			_grid.Add(BuildCoroutineCard());
			_grid.Add(BuildPoolCard());
			_grid.Add(BuildDataCard());
			_grid.Add(BuildTimeCard());
			_grid.Add(BuildRngCard());
			_grid.Add(BuildAssetResolverCard());
		}

		// ---- Tool cards ----

		private VisualElement BuildVersioningCard()
		{
			var versionDataExists = VersionDataFileExists();
			var card = MakeCard("Versioning", versionDataExists ? "version-data.txt present" : "version-data.txt missing", versionDataExists);
			var actions = GetActionsRow(card);
			actions.Add(MakeOpenButton<VersioningTab>());
			actions.Add(new Button(() => Versioning.Editor.VersionEditorUtils.SetAndSaveInternalVersion(false)) { text = "Refresh" });
			return card;
		}

		private VisualElement BuildAssetsImporterCard()
		{
			var count = AssetsImporterEditorUtils.DiscoverImporters().Count;
			var card = MakeCard("Assets Importer", $"{count} importer{(count == 1 ? "" : "s")} found", count > 0);
			var actions = GetActionsRow(card);
			actions.Add(MakeOpenButton<AssetsImporterTab>());
			actions.Add(new Button(AssetsImporterEditorUtils.ImportAll) { text = "Import All" });
			return card;
		}

		private VisualElement BuildAddressableIdsCard()
		{
			var settings = AddressableIdsEditorSettings.instance;
			var ok = AddressableIdsEditorSettings.IsValidIdentifier(settings.ScriptFilename, out _)
			         && AddressableIdsEditorSettings.IsValidNamespace(settings.Namespace, out _);
			var card = MakeCard("Addressable Ids", ok ? "settings ok" : "settings invalid", ok);
			var actions = GetActionsRow(card);
			actions.Add(MakeOpenButton<AddressableIdsTab>());
			actions.Add(new Button(() => AddressableIdsGeneratorUtils.Generate(settings)) { text = "Generate" });
			return card;
		}

		// ---- Service cards ----

		private VisualElement BuildInstallerCard()
		{
			var installer = MainInstaller.InstallerInstance;
			var bound = installer != null;
			var card = MakeCard("Installer", bound ? $"bound ({installer.Bindings.Count})" : "not bound", bound);
			var actions = GetActionsRow(card);
			actions.Add(MakeOpenButton<InstallerTab>());
			if (bound) actions.Add(new Button(() => { MainInstaller.Clean(); Refresh(); }) { text = "Clean All" });
			return card;
		}

		private VisualElement BuildMessageBrokerCard()
		{
			var svc = TryResolve<IMessageBrokerService>() as MessageBrokerService;
			var bound = svc != null;
			var card = MakeCard("Message Broker", bound ? $"bound ({svc.Subscriptions.Count} types)" : "not bound", bound);
			var actions = GetActionsRow(card);
			actions.Add(MakeOpenButton<MessageBrokerTab>());
			if (bound) actions.Add(new Button(() => { svc.UnsubscribeAll(null); Refresh(); }) { text = "Unsub All" });
			return card;
		}

		private VisualElement BuildTickCard()
		{
			var svc = TryResolve<ITickService>() as TickService;
			var bound = svc != null;
			var totalSubs = bound ? svc.OnUpdateList.Count + svc.OnFixedUpdateList.Count + svc.OnLateUpdateList.Count : 0;
			var card = MakeCard("Tick", bound ? $"bound ({totalSubs} subs)" : "not bound", bound);
			var actions = GetActionsRow(card);
			actions.Add(MakeOpenButton<TickTab>());
			if (bound) actions.Add(new Button(() => { svc.UnsubscribeAll(); Refresh(); }) { text = "Unsub All" });
			return card;
		}

		private VisualElement BuildCoroutineCard()
		{
			var svc = TryResolve<ICoroutineService>() as CoroutineService;
			var bound = svc != null;

#if UNITY_EDITOR
			var activeCount = bound ? svc.ActiveAsyncCoroutines.Count : 0;
			var statusText = bound ? $"bound ({activeCount} active)" : "not bound";
#else
			var statusText = bound ? "bound" : "not bound";
#endif

			var card = MakeCard("Coroutine", statusText, bound);
			var actions = GetActionsRow(card);
			actions.Add(MakeOpenButton<CoroutineTab>());
			if (bound) actions.Add(new Button(() => { svc.StopAllCoroutines(); Refresh(); }) { text = "Stop All" });
			return card;
		}

		private VisualElement BuildPoolCard()
		{
			var svc = TryResolve<IPoolService>() as PoolService;
			var bound = svc != null;
			var card = MakeCard("Pool", bound ? $"bound ({svc.Pools.Count} pools)" : "not bound", bound);
			var actions = GetActionsRow(card);
			actions.Add(MakeOpenButton<PoolTab>());

			if (bound)
			{
				var capturedSvc = svc;
				actions.Add(new Button(() =>
				{
					foreach (var pool in capturedSvc.Pools.Values)
					{
						pool.DespawnAll();
					}
					Refresh();
				}) { text = "Clear All" });
			}

			return card;
		}

		private VisualElement BuildDataCard()
		{
			var svc = TryResolve<IDataService>() as DataService;
			var bound = svc != null;
			var card = MakeCard("Data", bound ? $"bound ({svc.DataEntries.Count} entries)" : "not bound", bound);
			var actions = GetActionsRow(card);
			actions.Add(MakeOpenButton<DataTab>());
			if (bound) actions.Add(new Button(() => { svc.SaveAllData(); Refresh(); }) { text = "Save All" });
			return card;
		}

		private VisualElement BuildTimeCard()
		{
			var bound = TryResolve<ITimeService>() != null || TryResolve<ITimeManipulator>() != null;
			var card = MakeCard("Time", bound ? "bound" : "not bound", bound);
			var actions = GetActionsRow(card);
			actions.Add(MakeOpenButton<TimeTab>());
			return card;
		}

		private VisualElement BuildRngCard()
		{
			var svc = TryResolve<IRngService>();
			var bound = svc != null;
			var card = MakeCard("RNG", bound ? $"bound (counter={svc.Counter})" : "not bound", bound);
			var actions = GetActionsRow(card);
			actions.Add(MakeOpenButton<RngTab>());
			return card;
		}

		private VisualElement BuildAssetResolverCard()
		{
			var svc = TryResolve<IAssetResolverService>() as AssetResolverService;
			var bound = svc != null;
			var card = MakeCard("Asset Resolver", bound ? $"bound ({svc.AssetMap.Count} asset types)" : "not bound", bound);
			var actions = GetActionsRow(card);
			actions.Add(MakeOpenButton<AssetResolverTab>());
			return card;
		}

		// ---- Helpers ----

		private static bool VersionDataFileExists()
		{
			var folderPath = Versioning.Editor.VersioningEditorSettings.instance.ResourcesFolderPath;
			var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
			var absPath = Path.Combine(projectRoot, folderPath, VersionServices.VersionDataFilename + ".txt");
			return File.Exists(absPath);
		}

		private static VisualElement MakeCard(string title, string statusText, bool isOk)
		{
			var card = new VisualElement();
			card.AddToClassList("overview-card");

			var titleLabel = new Label(title);
			titleLabel.AddToClassList("overview-card-title");
			card.Add(titleLabel);

			var pill = new Label(statusText);
			pill.AddToClassList(isOk ? "status-ok" : "status-warn");
			card.Add(pill);

			var actions = new VisualElement();
			actions.AddToClassList("overview-card-actions");
			card.Add(actions);

			return card;
		}

		private static VisualElement GetActionsRow(VisualElement card)
		{
			return card.Q(className: "overview-card-actions");
		}

		private Button MakeOpenButton<TTab>() where TTab : ServiceTab
		{
			return new Button(() => _window?.SelectTab<TTab>()) { text = "Open" };
		}
	}
}
