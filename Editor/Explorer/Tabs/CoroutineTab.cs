using UnityEngine.UIElements;

namespace Geuneda.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// <see cref="ICoroutineService"/> 가 추적하는 활성 <see cref="IAsyncCoroutine"/> 핸들을 표시합니다.
	/// 에디터 전용 추적은 <see cref="CoroutineService"/> 의 <c>#if UNITY_EDITOR</c> 로 켜집니다.
	/// </summary>
	public class CoroutineTab : ServiceTab
	{
		public override string DisplayName => "Coroutine";

		private ScrollView _scroll;
		private VisualElement _list;
		private Label _totalLabel;

		protected override void BuildUi()
		{
			var header = new VisualElement();
			header.AddToClassList("tab-header-row");
			_totalLabel = new Label("Active: 0");
			_totalLabel.AddToClassList("tab-section-label");
			header.Add(_totalLabel);
			Add(header);

			_scroll = new ScrollView(ScrollViewMode.Vertical);
			_scroll.AddToClassList("tab-scroll");
			_list = new VisualElement();
			_scroll.Add(_list);
			Add(_scroll);

		var bar = MakeActionBar();
		bar.Add(MakePrimaryDangerButton("Stop All Coroutines", OnStopAll));
		Add(bar);
		}

		protected override void Refresh()
		{
			_list.Clear();

#if UNITY_EDITOR
			// Edit 모드에서는 정적 필드에 남은 CoroutineService 인스턴스가 있더라도 활성 코루틴 항목을
			// 숨깁니다. 자세한 이유는 ServiceTab.OnExitingPlayMode() 문서를 참고하세요.
			if (UnityEditor.EditorApplication.isPlaying == false)
			{
				_totalLabel.text = "Active: 0";
				_list.Add(MakeEmptyLabel());
				return;
			}

			var cs = TryResolve<ICoroutineService>() as CoroutineService;

			if (cs == null)
			{
				_totalLabel.text = "ICoroutineService not bound";
				_list.Add(MakeEmptyLabel("ICoroutineService not bound"));
				return;
			}

			var active = cs.ActiveAsyncCoroutines;
			_totalLabel.text = $"Active: {active.Count}";

			if (active.Count == 0)
			{
				_list.Add(MakeEmptyLabel());
				return;
			}

			foreach (var co in active)
			{
				var status = co.IsRunning ? "running" : (co.IsCompleted ? "completed" : "stopped");
				var row = MakeRow($"t={co.StartTime:F2}s", $"[{status}]");
				var stopBtn = MakeRowButton("Stop", () =>
				{
					co.StopCoroutine(true);
					Refresh();
				}, danger: true);
				row.Add(stopBtn);
				_list.Add(row);
			}
#else
			_totalLabel.text = "Editor tracking not available in non-editor builds";
#endif
		}

		// 사용자가 Play 모드를 멈추면 활성 코루틴 목록을 동기적으로 강제로 비웁니다.
		// OnDestroy 에서 코루틴 서비스를 해제하거나 MainInstaller.Clean() 을 호출하지 않는
		// 부트스트랩에 대한 이중 안전장치입니다.
		protected override void OnExitingPlayMode()
		{
			_totalLabel.text = "Active: 0";
			_list.Clear();
			_list.Add(MakeEmptyLabel());
		}

		private void OnStopAll()
		{
			var cs = TryResolve<ICoroutineService>();
			cs?.StopAllCoroutines();
			Refresh();
		}
	}
}
