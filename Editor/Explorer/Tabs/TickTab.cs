using UnityEngine.UIElements;

namespace Geuneda.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// <see cref="ITickService"/> 의 모든 틱 구독자(Update / FixedUpdate / LateUpdate)를 표시합니다.
	/// 목록별 일괄 구독 해제 동작을 제공합니다.
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
			// Edit 모드에서는 정적 필드에 남은 TickService 인스턴스가 있더라도 틱 구독자 목록을
			// 숨깁니다. 자세한 이유는 ServiceTab.OnExitingPlayMode() 문서를 참고하세요.
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

		// 사용자가 Play 모드를 멈추면 세 틱 구독자 목록을 모두 동기적으로 강제로 비웁니다.
		// OnDestroy 에서 ITickService 를 해제하거나 MainInstaller.Clean() 을 호출하지 않는
		// 부트스트랩에 대한 이중 안전장치입니다.
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
