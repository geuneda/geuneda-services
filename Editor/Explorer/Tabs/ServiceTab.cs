using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Geuneda.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// Abstract base for all Services Explorer tab panels.
	/// Handles play-mode-aware refresh scheduling and the "not in Play" snapshot banner.
	/// </summary>
	public abstract class ServiceTab : VisualElement
	{
		private const string BannerClass = "tab-banner";
		private const string RootClass = "tab-root";
		private const string EditModeBannerText = "Not in Play mode — showing last snapshot";
		private const string StoppedBannerText = "Play session ended — services unbound";

		private Label _banner;
		private IVisualElementScheduledItem _refreshTask;
		private bool _hasSeenPlay;

		// Tracks foldouts the user has explicitly collapsed in this tab instance, by stable key.
		// Tabs that nuke-and-rebuild their hierarchy in Refresh() need this — without it, fresh
		// Foldout instances default to value=true and the periodic refresh re-expands them
		// every RefreshIntervalMs, making collapse appear broken to the user.
		private readonly HashSet<string> _collapsedFoldoutKeys = new HashSet<string>();

		// Last data digest seen by Refresh(). Tabs that rebuild their hierarchy can short-circuit
		// when data is unchanged so the periodic timer doesn't fight user clicks (the rebuild
		// destroys mouse-captured VisualElements mid-click, losing rapid clicks).
		private string _lastRefreshDigest;

		/// <summary>Tab header text shown in the TabView strip.</summary>
		public abstract string DisplayName { get; }

		/// <summary>Refresh interval in milliseconds during Play mode. Override for slower updates.</summary>
		protected virtual int RefreshIntervalMs => 250;

		protected ServiceTab()
		{
			AddToClassList(RootClass);
			style.flexGrow = 1;

			_banner = new Label(EditModeBannerText);
			_banner.AddToClassList(BannerClass);
			Add(_banner);

			BuildUi();
			UpdateBannerVisibility();

			RegisterCallback<AttachToPanelEvent>(OnAttach);
			RegisterCallback<DetachFromPanelEvent>(OnDetach);
		}

		/// <summary>Build all child VisualElements. Called once in the constructor after the banner.</summary>
		protected abstract void BuildUi();

		/// <summary>
		/// Pull latest data from services and repopulate UI.
		/// Called every <see cref="RefreshIntervalMs"/> ms during Play mode,
		/// and once manually on attach (Edit mode snapshot).
		/// </summary>
		protected abstract void Refresh();

		/// <summary>
		/// Called synchronously on <see cref="PlayModeStateChange.ExitingPlayMode"/>, BEFORE
		/// scene teardown / <c>MonoBehaviour.OnDestroy</c> runs. Subclasses with populated
		/// state widgets (lists, labels, dropdowns) should override this to forcibly clear
		/// those widgets to an "unbound / session ended" state.
		///
		/// This is a belt-and-braces guarantee that the tab visually clears the moment play
		/// stops, independent of whether the consumer's bootstrap properly disposed services
		/// and called <see cref="MainInstaller.Clean"/> in <c>OnDestroy</c>. Tabs that already
		/// render correctly off an unbound <c>MainInstaller</c> can leave this as a no-op.
		/// </summary>
		protected virtual void OnExitingPlayMode() { }

		private void OnAttach(AttachToPanelEvent _)
		{
			EditorApplication.playModeStateChanged += OnPlayModeChanged;

			if (EditorApplication.isPlayingOrWillChangePlaymode)
			{
				_hasSeenPlay = true;
			}

			// Force the first refresh after open to fully rebuild, so toggling tabs always
			// renders the latest state regardless of any cached digest from a prior session.
			InvalidateRefreshDigest();

			UpdateBannerVisibility();
			Refresh();

			if (EditorApplication.isPlaying)
			{
				StartRefreshTimer();
			}
		}

		private void OnDetach(DetachFromPanelEvent _)
		{
			EditorApplication.playModeStateChanged -= OnPlayModeChanged;
			StopRefreshTimer();
		}

		private void OnPlayModeChanged(PlayModeStateChange state)
		{
			switch (state)
			{
				case PlayModeStateChange.EnteredPlayMode:
					_hasSeenPlay = true;
					UpdateBannerVisibility();
					InvalidateRefreshDigest();
					Refresh();
					StartRefreshTimer();
					break;
				case PlayModeStateChange.ExitingPlayMode:
					StopRefreshTimer();
					// Forcibly clear the populated UI right now, BEFORE OnDestroy runs.
					// This decouples the tab's empty-state from MainInstaller cleanup so
					// the user sees a clean snapshot the moment they press Stop, even if
					// their bootstrap forgot to call MainInstaller.Clean().
					OnExitingPlayMode();
					UpdateBannerVisibility();
					InvalidateRefreshDigest();
					// Then defer a follow-up refresh so it lands AFTER scene teardown
					// (i.e. after MonoBehaviour.OnDestroy → MainInstaller.Clean()), which
					// re-renders any state from the now-empty MainInstaller.
					EditorApplication.delayCall += DelayedExitRefresh;
					break;
				case PlayModeStateChange.EnteredEditMode:
					UpdateBannerVisibility();
					InvalidateRefreshDigest();
					Refresh();
					break;
			}
		}

		private void DelayedExitRefresh()
		{
			if (panel == null)
			{
				return;
			}

			UpdateBannerVisibility();
			InvalidateRefreshDigest();
			Refresh();
		}

		private void StartRefreshTimer()
		{
			StopRefreshTimer();
			_refreshTask = schedule.Execute(() =>
			{
				if (panel != null)
				{
					Refresh();
				}
			}).Every(RefreshIntervalMs);
		}

		private void StopRefreshTimer()
		{
			_refreshTask?.Pause();
			_refreshTask = null;
		}

		private void UpdateBannerVisibility()
		{
			if (EditorApplication.isPlaying)
			{
				_banner.style.display = DisplayStyle.None;
				return;
			}

			_banner.text = _hasSeenPlay ? StoppedBannerText : EditModeBannerText;
			_banner.style.display = DisplayStyle.Flex;
		}

		// ---- Helpers for sub-classes ----

		/// <summary>
		/// Creates a horizontal row with a type label on the left and optional value label,
		/// then appends action buttons on the right.
		/// </summary>
		protected static VisualElement MakeRow(string label, string value = null)
		{
			var row = new VisualElement();
			row.AddToClassList("row");

			var lbl = new Label(label);
			lbl.AddToClassList("row-label");
			row.Add(lbl);

			if (value != null)
			{
				var val = new Label(value);
				val.AddToClassList("row-value");
				row.Add(val);
			}

			return row;
		}

		/// <summary>Creates a small action button styled for use inside a row.</summary>
		protected static Button MakeRowButton(string text, System.Action onClick, bool danger = false)
		{
			var btn = new Button(onClick) { text = text };
			btn.AddToClassList("row-btn");

			if (danger)
			{
				btn.AddToClassList("row-btn-danger");
			}

			return btn;
		}

		/// <summary>Displays a section heading label.</summary>
		protected static Label MakeSectionLabel(string text)
		{
			var lbl = new Label(text);
			lbl.AddToClassList("tab-section-label");
			return lbl;
		}

		/// <summary>Displays an italic "empty" label.</summary>
		protected static Label MakeEmptyLabel(string text = "— none —")
		{
			var lbl = new Label(text);
			lbl.AddToClassList("tab-empty-label");
			return lbl;
		}

		/// <summary>Creates a bottom action bar VisualElement.</summary>
		protected static VisualElement MakeActionBar()
		{
			var bar = new VisualElement();
			bar.AddToClassList("action-bar");
			return bar;
		}

		/// <summary>
		/// Creates a visually prominent primary action button styled with the <c>action-primary</c> USS class.
		/// Use for the single most important call-to-action in each tab's action bar.
		/// </summary>
		protected static Button MakePrimaryButton(string text, System.Action onClick)
		{
			var btn = new Button(onClick) { text = text };
			btn.AddToClassList("action-primary");
			return btn;
		}

		/// <summary>
		/// Creates a destructive primary action button styled with the <c>action-primary-danger</c>
		/// USS class. Use for primary call-to-actions that remove or invalidate state
		/// (e.g. <c>Unsubscribe All</c>, <c>Stop All Coroutines</c>, <c>Clean All</c> bindings).
		/// </summary>
		protected static Button MakePrimaryDangerButton(string text, System.Action onClick)
		{
			var btn = new Button(onClick) { text = text };
			btn.AddToClassList("action-primary-danger");
			return btn;
		}

		/// <summary>
		/// Tries to resolve <typeparamref name="T"/> from <see cref="MainInstaller"/>.
		/// Returns null and does not throw if the service is not bound.
		/// </summary>
		protected static T TryResolve<T>() where T : class
		{
			MainInstaller.TryResolve<T>(out var service);
			return service;
		}

		/// <summary>
		/// Returns <c>true</c> when the supplied <paramref name="digest"/> matches the digest
		/// recorded on the previous <see cref="Refresh"/> call, i.e. the displayed data has
		/// not changed and the tab can return early without rebuilding the hierarchy.
		/// </summary>
		/// <remarks>
		/// <para>The 250 ms periodic refresh otherwise nukes-and-rebuilds the visual tree even
		/// when nothing changed, which destroys mouse-captured <see cref="VisualElement"/>s
		/// mid-click — rapid clicks on <see cref="Foldout"/>s appear lost because Unity loses
		/// the mouse-up against a now-deleted target. Skipping the rebuild whenever the digest
		/// matches solves this in the common case (asset load/subscription churn is rare
		/// compared to every-250 ms).</para>
		///
		/// <para>The digest must be a deterministic string built from every piece of state the
		/// tab renders — counts, identities, per-row status flags, etc. Computing it should be
		/// cheap (single pass over the data; no allocations in hot inner loops beyond a shared
		/// <c>StringBuilder</c>). The digest is reset to <c>null</c> on attach so the very first
		/// refresh after the tab opens always rebuilds.</para>
		/// </remarks>
		protected bool TryShortCircuitRefresh(string digest)
		{
			if (digest != null && string.Equals(_lastRefreshDigest, digest, System.StringComparison.Ordinal))
			{
				return true;
			}
			_lastRefreshDigest = digest;
			return false;
		}

		/// <summary>
		/// Forces the next <see cref="Refresh"/> to rebuild even if the digest matches.
		/// Call this from any code path that mutates child VisualElements outside of
		/// <see cref="Refresh"/> (e.g. an action-bar button that adds/removes rows directly).
		/// </summary>
		protected void InvalidateRefreshDigest()
		{
			_lastRefreshDigest = null;
		}

		/// <summary>
		/// Creates a <see cref="Foldout"/> whose expanded/collapsed state survives the tab's
		/// periodic <see cref="Refresh"/>. Use this in any tab whose <c>Refresh</c> rebuilds
		/// the hierarchy from scratch (e.g. <see cref="AssetResolverTab"/>, <see cref="MessageBrokerTab"/>),
		/// otherwise the user-visible collapse is reverted every <see cref="RefreshIntervalMs"/> ms.
		/// </summary>
		/// <remarks>
		/// State is keyed by <paramref name="key"/> (must be unique per tab instance — a stable
		/// string built from the data identity, e.g. <c>"Sprite"</c> or <c>"Sprite/SpriteId"</c>),
		/// stored in the per-tab <see cref="_collapsedFoldoutKeys"/> set. The set is NOT persisted
		/// across domain reloads; collapse memory lives only as long as the tab instance.
		///
		/// <para>The callback filters by <c>evt.target == foldout</c> on purpose. UIToolkit's
		/// <see cref="ChangeEvent{T}"/> bubbles up the visual tree, so a click on a NESTED
		/// foldout's internal <see cref="Toggle"/> would otherwise reach an ANCESTOR foldout's
		/// callback (target = the inner toggle, not the ancestor) and we'd incorrectly mark
		/// the ancestor as collapsed. Only the foldout's own value-set path produces events
		/// whose <c>target</c> equals the foldout itself; that's what we want to record.</para>
		/// </remarks>
		protected Foldout MakeStickyFoldout(string key, string text, bool defaultExpanded = true)
		{
			var foldout = new Foldout { text = text };

			var initialValue = defaultExpanded
				? !_collapsedFoldoutKeys.Contains(key)
				: _collapsedFoldoutKeys.Contains(key);
			foldout.SetValueWithoutNotify(initialValue);

			foldout.RegisterValueChangedCallback(evt =>
			{
				// Ignore ChangeEvent<bool> bubbled up from descendant Toggles / nested Foldouts.
				// Without this filter, collapsing an inner foldout marks the OUTER foldout
				// collapsed too on the next refresh tick.
				if (evt.target != foldout)
				{
					return;
				}

				if (evt.newValue)
				{
					_collapsedFoldoutKeys.Remove(key);
				}
				else
				{
					_collapsedFoldoutKeys.Add(key);
				}
			});

			return foldout;
		}
	}
}
