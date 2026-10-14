using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Geuneda.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// <see cref="ITimeService"/> 의 모든 시간 값을 실시간으로 표시합니다.
	/// 서비스가 <see cref="ITimeManipulator"/> 로 바인딩되어 있으면 AddTime 과 SetServerTime 조작을 보여 줍니다.
	/// </summary>
	public class TimeTab : ServiceTab
	{
		public override string DisplayName => "Time";
		protected override int RefreshIntervalMs => 500;

		private Label _utcLabel;
		private Label _serverLabel;
		private Label _unityLabel;
		private Label _scaleLabel;
		private Label _unixLabel;
		private Label _extraLabel;
		private Label _anchorLabel;
		private Slider _addTimeSlider;
		private FloatField _addTimeField;
		private TextField _setServerField;
		private VisualElement _manipulatorSection;
		private Button _resetButton;

		protected override void BuildUi()
		{
			var scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.AddToClassList("tab-scroll");

			scroll.Add(MakeSectionLabel("Live Values"));

			_utcLabel = MakeValueRow(scroll, "DateTimeUtcNow");
			_serverLabel = MakeValueRow(scroll, "IsServerTime");
			_unityLabel = MakeValueRow(scroll, "UnityTimeNow");
			_scaleLabel = MakeValueRow(scroll, "UnityScaleTimeNow");
			_unixLabel = MakeValueRow(scroll, "UnixTimeNow");
			_extraLabel = MakeValueRow(scroll, "ExtraTime (offset)");
			_anchorLabel = MakeValueRow(scroll, "Anchor (UTC)");

			_manipulatorSection = new VisualElement();
			_manipulatorSection.style.marginTop = 8;

			_manipulatorSection.Add(MakeSectionLabel("Manipulate (ITimeManipulator)"));

			var addRow = new VisualElement();
			addRow.style.flexDirection = FlexDirection.Row;
			addRow.style.alignItems = Align.Center;
			addRow.style.marginBottom = 4;

			var addLabel = new Label("AddTime (s): ");
			addLabel.style.minWidth = 120;
			addRow.Add(addLabel);

			_addTimeSlider = new Slider(-3600, 3600) { value = 0 };
			_addTimeSlider.style.flexGrow = 1;
			_addTimeSlider.style.minWidth = 100;
			addRow.Add(_addTimeSlider);

			_addTimeField = new FloatField { value = 0 };
			_addTimeField.style.width = 70;
			_addTimeField.RegisterValueChangedCallback(e => _addTimeSlider.value = e.newValue);
			_addTimeSlider.RegisterValueChangedCallback(e => _addTimeField.SetValueWithoutNotify(e.newValue));
			addRow.Add(_addTimeField);

			var applyAddBtn = new Button(OnAddTime) { text = "Apply" };
			addRow.Add(applyAddBtn);
			_manipulatorSection.Add(addRow);

			var setServerRow = new VisualElement();
			setServerRow.style.flexDirection = FlexDirection.Row;
			setServerRow.style.alignItems = Align.Center;
			setServerRow.style.marginBottom = 4;

			var setLabel = new Label("SetServerTime (UTC): ");
			setLabel.style.minWidth = 120;
			setServerRow.Add(setLabel);

			_setServerField = new TextField { value = DateTime.UtcNow.ToString("o") };
			_setServerField.style.flexGrow = 1;
			setServerRow.Add(_setServerField);

			var applyServerBtn = new Button(OnSetServerTime) { text = "Apply" };
			setServerRow.Add(applyServerBtn);
			_manipulatorSection.Add(setServerRow);

		scroll.Add(_manipulatorSection);
		Add(scroll);

		var bar = MakeActionBar();
		_resetButton = MakePrimaryButton("Reset Time", OnResetTime);
		bar.Add(_resetButton);
		Add(bar);
	}

		protected override void Refresh()
		{
			// Edit 모드에서는 정적 바인딩에 이전 플레이의 시간 서비스가 남아 있어도(도메인 리로드 끔) 상태와 조작을 숨깁니다
			if (!UnityEditor.EditorApplication.isPlaying)
			{
				ShowUnboundState();
				return;
			}

			var service = TryResolve<ITimeService>();

			if (service == null)
			{
				service = TryResolve<ITimeManipulator>();
			}

			if (service == null)
			{
				ShowUnboundState();
				return;
			}

			_utcLabel.text = service.DateTimeUtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
			_serverLabel.text = service.IsServerTime ? "true (server)" : "false (device clock at startup)";
			_unityLabel.text = $"{service.UnityTimeNow:F3} s";
			_scaleLabel.text = $"{service.UnityScaleTimeNow:F3} s";
			_unixLabel.text = service.UnixTimeNow.ToString();

			var isManipulator = TryResolve<ITimeManipulator>() != null;
			_manipulatorSection.style.display = isManipulator ? DisplayStyle.Flex : DisplayStyle.None;
			_resetButton.SetEnabled(isManipulator);

			if (service is TimeService ts)
			{
				_extraLabel.text = $"{ts.ExtraTime:F3} s";
				_anchorLabel.text = ts.AnchorUtc.ToString("yyyy-MM-dd HH:mm:ss.fff UTC");
			}
			else
			{
				_extraLabel.text = "—";
				_anchorLabel.text = "—";
			}
		}

		// 플레이를 멈추면 이전 서비스의 서버 시각 상태와 조작 UI 를 바로 비웁니다
		protected override void OnExitingPlayMode()
		{
			ShowUnboundState();
		}

		private void ShowUnboundState()
		{
			_utcLabel.text = "ITimeService not bound";
			_serverLabel.text = "";
			_unityLabel.text = "";
			_scaleLabel.text = "";
			_unixLabel.text = "";
			_extraLabel.text = "";
			_anchorLabel.text = "";
			_manipulatorSection.style.display = DisplayStyle.None;
			_resetButton.SetEnabled(false);
		}

		private static Label MakeValueRow(VisualElement parent, string labelText)
		{
			var row = new VisualElement();
			row.AddToClassList("row");

			var label = new Label(labelText);
			label.AddToClassList("row-label");
			row.Add(label);

			var value = new Label("—");
			value.AddToClassList("row-value");
			row.Add(value);

			parent.Add(row);
			return value;
		}

		private void OnResetTime()
	{
		var manipulator = TryResolve<ITimeManipulator>();

		if (manipulator == null)
		{
			return;
		}

		// 현재 추가 오프셋을 빼서 추가 시간을 0 으로 되돌립니다.
		if (manipulator is TimeService ts)
		{
			manipulator.AddTime(-ts.ExtraTime);
		}

		_addTimeField.SetValueWithoutNotify(0f);
		_addTimeSlider.SetValueWithoutNotify(0f);
		Refresh();
	}

	private void OnAddTime()
		{
			var manipulator = TryResolve<ITimeManipulator>();
			manipulator?.AddTime(_addTimeField.value);
			Refresh();
		}

		private void OnSetServerTime()
		{
			var manipulator = TryResolve<ITimeManipulator>();

			if (manipulator == null)
			{
				return;
			}

			// 입력은 UTC 로 보고, 오프셋이 붙어 있으면 UTC 로 바꿉니다
			if (DateTime.TryParse(_setServerField.value, System.Globalization.CultureInfo.InvariantCulture,
				System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var dt))
			{
				manipulator.SetServerTime(dt);
				Refresh();
			}
			else
			{
				Debug.LogWarning($"[ServicesExplorer] Invalid DateTime string: {_setServerField.value}");
			}
		}
	}
}
