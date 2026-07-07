using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Geuneda.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// Live display of all time values from <see cref="ITimeService"/>.
	/// AddTime and SetInitialTime controls are shown when the service is bound as <see cref="ITimeManipulator"/>.
	/// </summary>
	public class TimeTab : ServiceTab
	{
		public override string DisplayName => "Time";
		protected override int RefreshIntervalMs => 500;

		private Label _utcLabel;
		private Label _unityLabel;
		private Label _scaleLabel;
		private Label _unixLabel;
		private Label _extraLabel;
		private Label _initialLabel;
		private Slider _addTimeSlider;
		private FloatField _addTimeField;
		private TextField _setInitialField;
		private VisualElement _manipulatorSection;

		protected override void BuildUi()
		{
			var scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.AddToClassList("tab-scroll");

			scroll.Add(MakeSectionLabel("Live Values"));

			_utcLabel = MakeValueRow(scroll, "DateTimeUtcNow");
			_unityLabel = MakeValueRow(scroll, "UnityTimeNow");
			_scaleLabel = MakeValueRow(scroll, "UnityScaleTimeNow");
			_unixLabel = MakeValueRow(scroll, "UnixTimeNow");
			_extraLabel = MakeValueRow(scroll, "ExtraTime (offset)");
			_initialLabel = MakeValueRow(scroll, "InitialTime");

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

			var setInitialRow = new VisualElement();
			setInitialRow.style.flexDirection = FlexDirection.Row;
			setInitialRow.style.alignItems = Align.Center;
			setInitialRow.style.marginBottom = 4;

			var setLabel = new Label("SetInitialTime (UTC): ");
			setLabel.style.minWidth = 120;
			setInitialRow.Add(setLabel);

			_setInitialField = new TextField { value = DateTime.UtcNow.ToString("o") };
			_setInitialField.style.flexGrow = 1;
			setInitialRow.Add(_setInitialField);

			var applyInitBtn = new Button(OnSetInitialTime) { text = "Apply" };
			setInitialRow.Add(applyInitBtn);
			_manipulatorSection.Add(setInitialRow);

		scroll.Add(_manipulatorSection);
		Add(scroll);

		var bar = MakeActionBar();
		bar.Add(MakePrimaryButton("Reset Time", OnResetTime));
		Add(bar);
	}

		protected override void Refresh()
		{
			var service = TryResolve<ITimeService>();

			if (service == null)
			{
				service = TryResolve<ITimeManipulator>();
			}

			if (service == null)
			{
				_utcLabel.text = "ITimeService not bound";
				_unityLabel.text = "";
				_scaleLabel.text = "";
				_unixLabel.text = "";
				_extraLabel.text = "";
				_initialLabel.text = "";
				_manipulatorSection.style.display = DisplayStyle.None;
				return;
			}

			_utcLabel.text = service.DateTimeUtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
			_unityLabel.text = $"{service.UnityTimeNow:F3} s";
			_scaleLabel.text = $"{service.UnityScaleTimeNow:F3} s";
			_unixLabel.text = service.UnixTimeNow.ToString();

			var isManipulator = TryResolve<ITimeManipulator>() != null;
			_manipulatorSection.style.display = isManipulator ? DisplayStyle.Flex : DisplayStyle.None;

			if (service is TimeService ts)
			{
				_extraLabel.text = $"{ts.ExtraTime:F3} s";
				_initialLabel.text = ts.InitialTime.ToString("yyyy-MM-dd HH:mm:ss");
			}
			else
			{
				_extraLabel.text = "—";
				_initialLabel.text = "—";
			}
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

		// Reset extra time to 0 by subtracting the current extra offset.
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

		private void OnSetInitialTime()
		{
			var manipulator = TryResolve<ITimeManipulator>();

			if (manipulator == null)
			{
				return;
			}

			if (DateTime.TryParse(_setInitialField.value, null,
				System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
			{
				manipulator.SetInitialTime(dt);
				Refresh();
			}
			else
			{
				Debug.LogWarning($"[ServicesExplorer] Invalid DateTime string: {_setInitialField.value}");
			}
		}
	}
}
