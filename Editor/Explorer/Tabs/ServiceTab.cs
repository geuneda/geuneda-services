using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Geuneda.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// 모든 Services Explorer 탭 패널의 추상 기반 클래스입니다.
	/// Play 모드에 맞춘 갱신 예약과 "Play 모드 아님" 스냅샷 배너를 처리합니다.
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

		// 이 탭 인스턴스에서 사용자가 직접 접은 폴드아웃을 고정 키로 기록합니다.
		// Refresh() 에서 계층을 지우고 다시 만드는 탭에 필요합니다. 이것이 없으면 새 Foldout 인스턴스가
		// 기본값 value=true 로 만들어져 주기 갱신이 RefreshIntervalMs 마다 다시 펼치므로
		// 사용자에게는 접기가 고장 난 것처럼 보입니다.
		private readonly HashSet<string> _collapsedFoldoutKeys = new HashSet<string>();

		// Refresh() 가 마지막으로 본 데이터 다이제스트. 계층을 다시 만드는 탭은 데이터가 같으면
		// 재구성을 건너뛰어 주기 타이머가 사용자 클릭과 충돌하지 않게 합니다(재구성은 클릭 도중
		// 마우스를 캡처한 VisualElement 를 파괴해 빠른 클릭을 잃게 만듭니다).
		private string _lastRefreshDigest;

		/// <summary>TabView 탭 줄에 표시되는 탭 제목.</summary>
		public abstract string DisplayName { get; }

		/// <summary>Play 모드 중 갱신 간격(밀리초). 더 느리게 갱신하려면 재정의하세요.</summary>
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

		/// <summary>모든 자식 VisualElement 를 만듭니다. 생성자에서 배너 다음에 한 번 호출됩니다.</summary>
		protected abstract void BuildUi();

		/// <summary>
		/// 서비스에서 최신 데이터를 가져와 UI 를 다시 채웁니다.
		/// Play 모드 중에는 <see cref="RefreshIntervalMs"/> ms 마다,
		/// 패널에 붙을 때는 한 번(Edit 모드 스냅샷) 호출됩니다.
		/// </summary>
		protected abstract void Refresh();

		/// <summary>
		/// <see cref="PlayModeStateChange.ExitingPlayMode"/> 에서 씬 정리 / <c>MonoBehaviour.OnDestroy</c> 가
		/// 실행되기 전에 동기적으로 호출됩니다. 상태를 표시하는 위젯(리스트, 레이블, 드롭다운)이 있는
		/// 파생 클래스는 이를 재정의해 위젯을 "바인딩 해제 / 세션 종료" 상태로 강제로 비워야 합니다.
		///
		/// 사용하는 쪽 부트스트랩이 서비스를 제대로 해제하고 <c>OnDestroy</c> 에서
		/// <see cref="MainInstaller.Clean"/> 을 호출했는지와 상관없이, 플레이를 멈추는 순간 탭이 비워지도록
		/// 보장하는 이중 안전장치입니다. 바인딩이 없는 <c>MainInstaller</c> 로도 이미 올바르게 그리는 탭은
		/// 비워 두어도 됩니다.
		/// </summary>
		protected virtual void OnExitingPlayMode() { }

		private void OnAttach(AttachToPanelEvent _)
		{
			EditorApplication.playModeStateChanged += OnPlayModeChanged;

			if (EditorApplication.isPlayingOrWillChangePlaymode)
			{
				_hasSeenPlay = true;
			}

			// 열린 뒤 첫 갱신은 반드시 전체를 다시 만들도록 해, 이전 세션의 다이제스트가 남아 있어도
			// 탭을 오갈 때 항상 최신 상태를 그립니다.
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
					// OnDestroy 가 실행되기 전에 채워진 UI 를 지금 바로 강제로 비웁니다.
					// 탭의 빈 상태를 MainInstaller 정리와 분리해, 부트스트랩이 MainInstaller.Clean()
					// 호출을 잊었더라도 사용자가 Stop 을 누르는 순간 깨끗한 화면을 보게 합니다.
					OnExitingPlayMode();
					UpdateBannerVisibility();
					InvalidateRefreshDigest();
					// 이어서 후속 갱신을 미뤄 씬 정리 이후(MonoBehaviour.OnDestroy → MainInstaller.Clean() 이후)에
					// 실행되게 하고, 비워진 MainInstaller 기준으로 상태를 다시 그립니다.
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

		// ---- 파생 클래스용 헬퍼 ----

		/// <summary>
		/// 왼쪽에 타입 레이블과 선택적인 값 레이블을 둔 가로 행을 만듭니다.
		/// 액션 버튼은 호출하는 쪽에서 오른쪽에 덧붙입니다.
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

		/// <summary>행 안에서 쓰는 작은 액션 버튼을 만듭니다.</summary>
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

		/// <summary>섹션 제목 레이블을 만듭니다.</summary>
		protected static Label MakeSectionLabel(string text)
		{
			var lbl = new Label(text);
			lbl.AddToClassList("tab-section-label");
			return lbl;
		}

		/// <summary>기울임꼴 "비어 있음" 레이블을 만듭니다.</summary>
		protected static Label MakeEmptyLabel(string text = "— none —")
		{
			var lbl = new Label(text);
			lbl.AddToClassList("tab-empty-label");
			return lbl;
		}

		/// <summary>하단 액션 바 VisualElement 를 만듭니다.</summary>
		protected static VisualElement MakeActionBar()
		{
			var bar = new VisualElement();
			bar.AddToClassList("action-bar");
			return bar;
		}

		/// <summary>
		/// <c>action-primary</c> USS 클래스를 적용한 눈에 띄는 주요 액션 버튼을 만듭니다.
		/// 각 탭 액션 바에서 가장 중요한 동작 하나에 쓰세요.
		/// </summary>
		protected static Button MakePrimaryButton(string text, System.Action onClick)
		{
			var btn = new Button(onClick) { text = text };
			btn.AddToClassList("action-primary");
			return btn;
		}

		/// <summary>
		/// <c>action-primary-danger</c> USS 클래스를 적용한 파괴적 주요 액션 버튼을 만듭니다.
		/// 상태를 지우거나 무효화하는 주요 동작(예: <c>Unsubscribe All</c>, <c>Stop All Coroutines</c>,
		/// 바인딩 <c>Clean All</c>)에 쓰세요.
		/// </summary>
		protected static Button MakePrimaryDangerButton(string text, System.Action onClick)
		{
			var btn = new Button(onClick) { text = text };
			btn.AddToClassList("action-primary-danger");
			return btn;
		}

		/// <summary>
		/// <see cref="MainInstaller"/> 에서 <typeparamref name="T"/> 를 찾아봅니다.
		/// 서비스가 바인딩되지 않았으면 예외 없이 null 을 반환합니다.
		/// </summary>
		protected static T TryResolve<T>() where T : class
		{
			MainInstaller.TryResolve<T>(out var service);
			return service;
		}

		/// <summary>
		/// 주어진 <paramref name="digest"/> 가 이전 <see cref="Refresh"/> 호출 때 기록한 다이제스트와 같으면
		/// <c>true</c> 를 반환합니다. 즉 표시할 데이터가 바뀌지 않았으므로 탭이 계층을 다시 만들지 않고
		/// 바로 반환해도 됩니다.
		/// </summary>
		/// <remarks>
		/// <para>이 검사가 없으면 250 ms 주기 갱신이 아무것도 바뀌지 않아도 비주얼 트리를 지우고 다시 만들어,
		/// 클릭 도중 마우스를 캡처한 <see cref="VisualElement"/> 를 파괴합니다. Unity 가 이미 삭제된 대상에서
		/// 마우스 업을 놓치므로 <see cref="Foldout"/> 을 빠르게 클릭하면 클릭이 사라진 것처럼 보입니다.
		/// 다이제스트가 같을 때 재구성을 건너뛰면 일반적인 경우 이 문제가 해결됩니다(에셋 로드나 구독 변경은
		/// 250 ms 주기에 비해 드뭅니다).</para>
		///
		/// <para>다이제스트는 탭이 그리는 모든 상태(개수, 식별자, 행별 상태 플래그 등)로 만든 결정적 문자열이어야
		/// 합니다. 계산 비용은 작아야 합니다(데이터를 한 번만 순회하고, 공유 <c>StringBuilder</c> 외에는 내부 루프에서
		/// 할당하지 않음). 패널에 붙을 때 다이제스트를 <c>null</c> 로 초기화하므로 탭이 열린 뒤 첫 갱신은 항상
		/// 다시 만듭니다.</para>
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
		/// 다이제스트가 같아도 다음 <see cref="Refresh"/> 가 다시 만들도록 강제합니다.
		/// <see cref="Refresh"/> 밖에서 자식 VisualElement 를 바꾸는 코드 경로(예: 행을 직접 추가/제거하는
		/// 액션 바 버튼)에서 호출하세요.
		/// </summary>
		protected void InvalidateRefreshDigest()
		{
			_lastRefreshDigest = null;
		}

		/// <summary>
		/// 펼침/접힘 상태가 탭의 주기적 <see cref="Refresh"/> 를 거쳐도 유지되는 <see cref="Foldout"/> 을 만듭니다.
		/// <c>Refresh</c> 에서 계층을 처음부터 다시 만드는 탭(예: <see cref="AssetResolverTab"/>,
		/// <see cref="MessageBrokerTab"/>)에서 쓰세요. 그렇지 않으면 사용자가 접은 상태가
		/// <see cref="RefreshIntervalMs"/> ms 마다 되돌아갑니다.
		/// </summary>
		/// <remarks>
		/// 상태는 <paramref name="key"/> 로 구분합니다(탭 인스턴스 안에서 고유해야 하며, 데이터 식별자로 만든
		/// 고정 문자열, 예: <c>"Sprite"</c> 또는 <c>"Sprite/SpriteId"</c>). 탭별 <see cref="_collapsedFoldoutKeys"/>
		/// 집합에 저장되며 도메인 리로드를 넘어 저장되지 않습니다. 접힘 기억은 탭 인스턴스가 살아 있는 동안만 유지됩니다.
		///
		/// <para>콜백은 일부러 <c>evt.target == foldout</c> 으로 거릅니다. UIToolkit 의 <see cref="ChangeEvent{T}"/> 는
		/// 비주얼 트리를 따라 버블링되므로, 중첩된 폴드아웃 내부 <see cref="Toggle"/> 을 클릭한 이벤트가 상위 폴드아웃의
		/// 콜백에도 도달합니다(target 은 상위가 아니라 내부 토글). 거르지 않으면 상위 폴드아웃을 잘못 접힘으로 기록합니다.
		/// <c>target</c> 이 폴드아웃 자신인 이벤트는 그 폴드아웃의 값 설정 경로에서만 나오며, 기록할 대상은 그것뿐입니다.</para>
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
				// 하위 Toggle / 중첩 Foldout 에서 버블링된 ChangeEvent<bool> 은 무시합니다.
				// 이 필터가 없으면 안쪽 폴드아웃을 접을 때 다음 갱신 틱에서 바깥 폴드아웃도
				// 접힘으로 기록됩니다.
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
