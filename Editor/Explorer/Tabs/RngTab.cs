using UnityEngine;
using UnityEngine.UIElements;

namespace Geuneda.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// Displays the current state of <see cref="IRngService"/>: Seed, Counter, the next
	/// upcoming value (always-on peek), a configurable Peek-N preview list, and a
	/// rewind/fast-forward control over the deterministic counter.
	/// </summary>
	public class RngTab : ServiceTab
	{
		public override string DisplayName => "RNG";
		protected override int RefreshIntervalMs => 500;

		private const string PeekTooltip =
			"Preview the next N values WITHOUT advancing the RNG counter. " +
			"The live RNG state is unaffected — running game logic that calls Next/Range will still produce these same values in this order.";

		private const string RestoreTooltip =
			"Set the RNG counter to a specific draw index, replaying the deterministic stream from that point. " +
			"Use 0 to rewind to the very start. Future-pointing values fast-forward the RNG to a point that hasn't been drawn yet.";

		private const int PeekMin = 1;
		private const int PeekMax = 50;
		private const int RestoreSliderMax = 1000;

		private Label _seedLabel;
		private Label _counterLabel;
		private Label _nextValueLabel;
		private VisualElement _peekList;
		private SliderInt _peekSlider;
		private IntegerField _peekCountField;
		private SliderInt _restoreSlider;
		private IntegerField _restoreCountField;

		protected override void BuildUi()
		{
			var scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.AddToClassList("tab-scroll");

			scroll.Add(MakeSectionLabel("State"));

			var seedRow = new VisualElement();
			seedRow.AddToClassList("row");
			seedRow.Add(new Label("Seed") { style = { width = 120 } });
			_seedLabel = new Label("—");
			_seedLabel.AddToClassList("row-value");
			seedRow.Add(_seedLabel);
			scroll.Add(seedRow);

			var counterRow = new VisualElement();
			counterRow.AddToClassList("row");
			counterRow.Add(new Label("Counter") { style = { width = 120 } });
			_counterLabel = new Label("—");
			_counterLabel.AddToClassList("row-value");
			counterRow.Add(_counterLabel);
			scroll.Add(counterRow);

			var nextRow = new VisualElement();
			nextRow.AddToClassList("row");
			nextRow.Add(new Label("Next value") { style = { width = 120 } });
			_nextValueLabel = new Label("—");
			_nextValueLabel.AddToClassList("row-value");
			_nextValueLabel.tooltip = "The exact int value the next Next/Range call will return. Updated on every refresh and on every Peek-N click.";
			nextRow.Add(_nextValueLabel);
			scroll.Add(nextRow);

			scroll.Add(MakeSectionLabel("Peek next N values (non-consuming)"));

			// Slider + IntegerField pair (matches TimeTab.AddTime layout). The two widgets
			// stay bidirectionally synced so the user can drag for quick iteration OR type
			// an exact count without the cramped single-widget form factor that buried the
			// numeric input behind the field's "Count" label.
			var peekControls = new VisualElement();
			peekControls.style.flexDirection = FlexDirection.Row;
			peekControls.style.alignItems = Align.Center;
			peekControls.style.marginBottom = 4;

			var peekLabel = new Label("Count: ");
			peekLabel.style.minWidth = 60;
			peekControls.Add(peekLabel);

			_peekSlider = new SliderInt(PeekMin, PeekMax) { value = 5, tooltip = "How many upcoming values to preview." };
			_peekSlider.style.flexGrow = 1;
			_peekSlider.style.minWidth = 100;
			peekControls.Add(_peekSlider);

			_peekCountField = new IntegerField { value = 5, tooltip = "How many upcoming values to preview." };
			_peekCountField.style.width = 60;
			peekControls.Add(_peekCountField);

			_peekCountField.RegisterValueChangedCallback(e =>
			{
				var clamped = Mathf.Clamp(e.newValue, PeekMin, PeekMax);
				_peekSlider.SetValueWithoutNotify(clamped);
				if (clamped != e.newValue)
				{
					_peekCountField.SetValueWithoutNotify(clamped);
				}
			});
			_peekSlider.RegisterValueChangedCallback(e => _peekCountField.SetValueWithoutNotify(e.newValue));

			var peekBtn = new Button(OnPeek) { text = "Peek N", tooltip = PeekTooltip };
			peekBtn.AddToClassList("row-btn");
			peekControls.Add(peekBtn);

			scroll.Add(peekControls);

			_peekList = new VisualElement();
			scroll.Add(_peekList);

			scroll.Add(MakeSectionLabel("Rewind / fast-forward counter"));

			var restoreInfo = new Label(
				"Sets the RNG counter to a specific draw index. The next Next/Range call resumes the deterministic stream from that index. Useful for save/replay scenarios.");
			restoreInfo.AddToClassList("tab-empty-label");
			restoreInfo.style.whiteSpace = WhiteSpace.Normal;
			restoreInfo.style.marginBottom = 2;
			scroll.Add(restoreInfo);

			var restoreRow = new VisualElement();
			restoreRow.style.flexDirection = FlexDirection.Row;
			restoreRow.style.alignItems = Align.Center;
			restoreRow.style.marginBottom = 4;

			var restoreLabel = new Label("Counter: ");
			restoreLabel.style.minWidth = 60;
			restoreRow.Add(restoreLabel);

			// Slider 0..1000 covers the common save/replay-window scenario; users needing
			// arbitrarily large counters can type directly into the IntegerField next to it.
			_restoreSlider = new SliderInt(0, RestoreSliderMax) { value = 0, tooltip = RestoreTooltip };
			_restoreSlider.style.flexGrow = 1;
			_restoreSlider.style.minWidth = 100;
			restoreRow.Add(_restoreSlider);

			_restoreCountField = new IntegerField { value = 0, tooltip = RestoreTooltip };
			_restoreCountField.style.width = 80;
			restoreRow.Add(_restoreCountField);

			_restoreCountField.RegisterValueChangedCallback(e =>
			{
				if (e.newValue >= 0 && e.newValue <= RestoreSliderMax)
				{
					_restoreSlider.SetValueWithoutNotify(e.newValue);
				}
			});
			_restoreSlider.RegisterValueChangedCallback(e => _restoreCountField.SetValueWithoutNotify(e.newValue));

			var restoreBtn = new Button(OnRestore) { text = "Set counter", tooltip = RestoreTooltip };
			restoreBtn.AddToClassList("row-btn");
			restoreRow.Add(restoreBtn);

			var rewindBtn = new Button(OnRewindToZero) { text = "Rewind to 0", tooltip = "Shortcut: replay the entire deterministic stream from index 0." };
			rewindBtn.AddToClassList("row-btn");
			restoreRow.Add(rewindBtn);

			scroll.Add(restoreRow);
			Add(scroll);
		}

		protected override void Refresh()
		{
			// Hide RNG state in edit mode (initial OR after a play session ended) regardless
			// of any leftover RngService kept alive by a static field. Together with
			// OnExitingPlayMode() this guarantees the tab does not retain a live snapshot
			// after Stop, even if the consumer's bootstrap forgot to call
			// MainInstaller.Clean() in OnDestroy.
			if (!UnityEditor.EditorApplication.isPlaying)
			{
				ShowUnboundState();
				return;
			}

			var rng = TryResolve<IRngService>();

			if (rng == null)
			{
				ShowUnboundState();
				return;
			}

			_seedLabel.text = rng.Data.Seed.ToString();
			_counterLabel.text = rng.Counter.ToString();
			_nextValueLabel.text = rng.Peek.ToString();
		}

		// Forcibly clear all RNG-state widgets when the user stops play mode. Belt-and-braces
		// guarantee that the tab does not retain a frozen "last play" snapshot even if the
		// consumer's bootstrap forgot to call MainInstaller.Clean() in OnDestroy.
		protected override void OnExitingPlayMode()
		{
			ShowUnboundState();
		}

		private void ShowUnboundState()
		{
			_seedLabel.text = "not bound";
			_counterLabel.text = "—";
			_nextValueLabel.text = "—";
			_peekList.Clear();
		}

		private void OnPeek()
		{
			_peekList.Clear();

			var rng = TryResolve<IRngService>() as RngService;

			if (rng == null)
			{
				_peekList.Add(MakeEmptyLabel("IRngService not bound"));
				return;
			}

			var count = Mathf.Clamp(_peekCountField.value, PeekMin, PeekMax);
			var stateCopy = RngService.CopyRngState(((RngData)rng.Data).State);
			var startCount = rng.Counter;

			for (var i = 0; i < count; i++)
			{
				var val = RngService.Range(0, int.MaxValue, stateCopy, false);
				var row = MakeRow($"[#{startCount + i}]", val.ToString());
				_peekList.Add(row);
			}

			_nextValueLabel.text = rng.Peek.ToString();
		}

		private void OnRestore()
		{
			var rng = TryResolve<IRngService>();

			if (rng == null)
			{
				return;
			}

			rng.Restore(_restoreCountField.value);
			Refresh();
		}

		private void OnRewindToZero()
		{
			var rng = TryResolve<IRngService>();

			if (rng == null)
			{
				return;
			}

			_restoreCountField.SetValueWithoutNotify(0);
			_restoreSlider.SetValueWithoutNotify(0);
			rng.Restore(0);
			Refresh();
		}
	}
}
