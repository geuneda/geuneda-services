using Geuneda.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Geuneda.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// <see cref="IRngService"/> 의 현재 상태를 표시합니다: Seed, Stream, Count, 다음에 나올 32비트 값(항상 미리 보기),
	/// 개수를 정할 수 있는 Peek-N 미리 보기 목록, 그리고 결정적 카운터를 되감거나 앞으로 감는 조작.
	/// </summary>
	public class RngTab : ServiceTab
	{
		public override string DisplayName => "RNG";
		protected override int RefreshIntervalMs => 500;

		private const string PeekTooltip =
			"Preview the next N raw 32-bit outputs WITHOUT advancing the RNG count. " +
			"The live RNG state is unaffected — running game logic that calls NextUInt will still produce these same values in this order. " +
			"Range/NextFloat map these outputs (Range may occasionally consume more than one).";

		private const string RestoreTooltip =
			"Set the RNG count (number of 32-bit outputs consumed) to a specific index, replaying the deterministic stream from that point. " +
			"Use 0 to rewind to the very start. Future-pointing values fast-forward the RNG to a point that hasn't been drawn yet.";

		private const int PeekMin = 1;
		private const int PeekMax = 50;
		private const int RestoreSliderMax = 1000;

		private Label _seedLabel;
		private Label _streamLabel;
		private Label _counterLabel;
		private Label _nextValueLabel;
		private VisualElement _peekList;
		private SliderInt _peekSlider;
		private IntegerField _peekCountField;
		private SliderInt _restoreSlider;
		private UnsignedLongField _restoreCountField;

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

			var streamRow = new VisualElement();
			streamRow.AddToClassList("row");
			streamRow.Add(new Label("Stream") { style = { width = 120 } });
			_streamLabel = new Label("—");
			_streamLabel.AddToClassList("row-value");
			streamRow.Add(_streamLabel);
			scroll.Add(streamRow);

			var counterRow = new VisualElement();
			counterRow.AddToClassList("row");
			counterRow.Add(new Label("Count") { style = { width = 120 } });
			_counterLabel = new Label("—");
			_counterLabel.AddToClassList("row-value");
			counterRow.Add(_counterLabel);
			scroll.Add(counterRow);

			var nextRow = new VisualElement();
			nextRow.AddToClassList("row");
			nextRow.Add(new Label("Next value") { style = { width = 120 } });
			_nextValueLabel = new Label("—");
			_nextValueLabel.AddToClassList("row-value");
			_nextValueLabel.tooltip = "The exact 32-bit value the next NextUInt call will return. Updated on every refresh and on every Peek-N click.";
			nextRow.Add(_nextValueLabel);
			scroll.Add(nextRow);

			scroll.Add(MakeSectionLabel("Peek next N values (non-consuming)"));

			// Slider + IntegerField 쌍(TimeTab.AddTime 배치와 같음). 두 위젯은 양방향으로 동기화되어
			// 사용자가 드래그로 빠르게 조절하거나 정확한 개수를 직접 입력할 수 있습니다. 위젯 하나만 쓰면
			// 숫자 입력란이 "Count" 레이블 뒤에 좁게 묻혀 버립니다.
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
				"Sets the RNG count to a specific output index. The next NextUInt/NextFloat/Range call resumes the deterministic stream from that index. Useful for save/replay scenarios.");
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

			// 슬라이더 0..1000 은 일반적인 저장/리플레이 구간을 다룹니다. 더 큰 카운터가 필요하면
			// 옆의 UnsignedLongField 에 직접 입력하면 됩니다.
			_restoreSlider = new SliderInt(0, RestoreSliderMax) { value = 0, tooltip = RestoreTooltip };
			_restoreSlider.style.flexGrow = 1;
			_restoreSlider.style.minWidth = 100;
			restoreRow.Add(_restoreSlider);

			_restoreCountField = new UnsignedLongField { value = 0, tooltip = RestoreTooltip };
			_restoreCountField.style.width = 80;
			restoreRow.Add(_restoreCountField);

			_restoreCountField.RegisterValueChangedCallback(e =>
			{
				if (e.newValue <= RestoreSliderMax)
				{
					_restoreSlider.SetValueWithoutNotify((int)e.newValue);
				}
			});
			_restoreSlider.RegisterValueChangedCallback(e => _restoreCountField.SetValueWithoutNotify((ulong)e.newValue));

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
			// Edit 모드(처음이든 플레이 세션이 끝난 뒤든)에서는 정적 필드에 남은 RngService 가 있더라도
			// RNG 상태를 숨깁니다. OnExitingPlayMode() 와 함께, 사용하는 쪽 부트스트랩이 OnDestroy 에서
			// MainInstaller.Clean() 호출을 잊었더라도 Stop 이후 탭이 살아 있는 스냅샷을 유지하지 않도록 보장합니다.
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
			_streamLabel.text = rng.Data.Stream.ToString();
			_counterLabel.text = rng.Data.Count.ToString();
			_nextValueLabel.text = CreateGeneratorAtCurrentCount(rng.Data).NextUInt().ToString();
		}

		// 사용자가 Play 모드를 멈추면 모든 RNG 상태 위젯을 강제로 비웁니다. 사용하는 쪽 부트스트랩이
		// OnDestroy 에서 MainInstaller.Clean() 호출을 잊었더라도 탭이 멈춘 "마지막 플레이" 스냅샷을
		// 유지하지 않도록 하는 이중 안전장치입니다.
		protected override void OnExitingPlayMode()
		{
			ShowUnboundState();
		}

		private void ShowUnboundState()
		{
			_seedLabel.text = "not bound";
			_streamLabel.text = "—";
			_counterLabel.text = "—";
			_nextValueLabel.text = "—";
			_peekList.Clear();
		}

		private void OnPeek()
		{
			_peekList.Clear();

			var rng = TryResolve<IRngService>();

			if (rng == null)
			{
				_peekList.Add(MakeEmptyLabel("IRngService not bound"));
				return;
			}

			var count = Mathf.Clamp(_peekCountField.value, PeekMin, PeekMax);
			// 서비스와 같은 시드·스트림·위치의 복사본이라 서비스 상태는 그대로입니다
			var generator = CreateGeneratorAtCurrentCount(rng.Data);

			for (var i = 0; i < count; i++)
			{
				var index = generator.Position;
				var row = MakeRow($"[#{index}]", generator.NextUInt().ToString());
				_peekList.Add(row);
			}

			_nextValueLabel.text = CreateGeneratorAtCurrentCount(rng.Data).NextUInt().ToString();
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

		private static Pcg32 CreateGeneratorAtCurrentCount(IRngData data)
		{
			var generator = new Pcg32(data.Seed, data.Stream);

			generator.Advance(data.Count);

			return generator;
		}
	}
}
