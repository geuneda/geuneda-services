using UnityEngine.UIElements;

namespace Geuneda.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// Displays all tick subscribers (Update / FixedUpdate / LateUpdate) of <see cref="ITickService"/>.
	/// Provides bulk unsubscribe actions per list.
	/// </summary>
	public class TickTab : ServiceTab
	{
		public override string DisplayName => "Tick";

		private Foldout _updateFoldout;
		private Foldout _fixedFoldout;
		private Foldout _lateFoldout;
		private VisualElement _actionBar;

		protected override void BuildUi()
		{
			var scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.AddToClassList("tab-scroll");

			_updateFoldout = new Foldout { text = "Update (0)", value = true };
			_updateFoldout.AddToClassList("section-foldout");
			scroll.Add(_updateFoldout);

			_fixedFoldout = new Foldout { text = "FixedUpdate (0)", value = true };
			_fixedFoldout.AddToClassList("section-foldout");
			scroll.Add(_fixedFoldout);

			_lateFoldout = new Foldout { text = "LateUpdate (0)", value = false };
			_lateFoldout.AddToClassList("section-foldout");
			scroll.Add(_lateFoldout);

			Add(scroll);

		_actionBar = MakeActionBar();
		_actionBar.Add(MakePrimaryDangerButton("Unsubscribe All", OnClearAll));
		_actionBar.Add(new Button(OnClearUpdate) { text = "Clear Update" });
		_actionBar.Add(new Button(OnClearFixed) { text = "Clear FixedUpdate" });
		_actionBar.Add(new Button(OnClearLate) { text = "Clear LateUpdate" });
		Add(_actionBar);
		}

		protected override void Refresh()
		{
			// Hide tick subscriber lists in edit mode regardless of any leftover
			// TickService instance kept alive by a static field. See ServiceTab
			// OnExitingPlayMode() docs for the broader rationale.
			if (!UnityEditor.EditorApplication.isPlaying)
			{
				ShowEmptyState();
				return;
			}

			var tick = TryResolve<ITickService>() as TickService;

			if (tick == null)
			{
				_updateFoldout.text = "Update — not bound";
				_fixedFoldout.text = "FixedUpdate — not bound";
				_lateFoldout.text = "LateUpdate — not bound";
				ClearFoldout(_updateFoldout);
				ClearFoldout(_fixedFoldout);
				ClearFoldout(_lateFoldout);
				return;
			}

			PopulateFoldout(_updateFoldout, "Update", tick.OnUpdateList);
			PopulateFoldout(_fixedFoldout, "FixedUpdate", tick.OnFixedUpdateList);
			PopulateFoldout(_lateFoldout, "LateUpdate", tick.OnLateUpdateList);
		}

		// Forcibly clear all three tick subscriber lists synchronously when the user
		// stops play mode. Belt-and-braces against bootstraps that fail to dispose
		// ITickService / call MainInstaller.Clean() in OnDestroy.
		protected override void OnExitingPlayMode()
		{
			ShowEmptyState();
		}

		private void ShowEmptyState()
		{
			_updateFoldout.text = "Update (0)";
			_fixedFoldout.text = "FixedUpdate (0)";
			_lateFoldout.text = "LateUpdate (0)";
			ClearFoldout(_updateFoldout);
			ClearFoldout(_fixedFoldout);
			ClearFoldout(_lateFoldout);
		}

		private static void PopulateFoldout(Foldout foldout, string label,
			System.Collections.Generic.IReadOnlyList<TickService.TickData> list)
		{
			foldout.text = $"{label} ({list.Count})";
			ClearFoldout(foldout);

			if (list.Count == 0)
			{
				foldout.Add(MakeEmptyLabel());
				return;
			}

			foreach (var data in list)
			{
				var subscriberName = data.Subscriber?.GetType().Name ?? "?";
				var detail = $"dt={data.DeltaTime:F3}  realTime={data.RealTime}  overflow={data.TimeOverflowToNextTick}";
				var row = MakeRow($"[{data.Id}] {subscriberName}", detail);
				foldout.Add(row);
			}
		}

		private static void ClearFoldout(Foldout foldout)
		{
			while (foldout.childCount > 0)
			{
				foldout.RemoveAt(0);
			}
		}

		private void OnClearUpdate()
		{
			var tick = TryResolve<ITickService>();
			tick?.UnsubscribeAllOnUpdate();
			Refresh();
		}

		private void OnClearFixed()
		{
			var tick = TryResolve<ITickService>();
			tick?.UnsubscribeAllOnFixedUpdate();
			Refresh();
		}

		private void OnClearLate()
		{
			var tick = TryResolve<ITickService>();
			tick?.UnsubscribeAllOnLateUpdate();
			Refresh();
		}

		private void OnClearAll()
		{
			var tick = TryResolve<ITickService>();
			tick?.UnsubscribeAll();
			Refresh();
		}
	}
}
