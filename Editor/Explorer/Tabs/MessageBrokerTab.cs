using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Geuneda.Services.Editor.Explorer.Tabs
{
	/// <summary>
	/// <see cref="IMessageBrokerService"/> 가 가진 모든 <see cref="IMessage"/> 구독을 표시합니다.
	/// 메시지 타입별 Unsubscribe, UnsubscribeAll, 그리고 리플렉션으로 찾은 구체 <see cref="IMessage"/> 구현 타입을
	/// 드롭다운에서 골라 테스트 발행하는 기능을 제공합니다.
	/// </summary>
	public class MessageBrokerTab : ServiceTab
	{
		public override string DisplayName => "Message Broker";

		private ScrollView _scroll;
		private VisualElement _list;
		private DropdownField _publishTypeDropdown;
		private Button _publishButton;
		private Label _publishStatus;
		private readonly List<Type> _messageTypes = new List<Type>();
		private int _lastDiscoveredAssemblyCount = -1;

		protected override void BuildUi()
		{
			_scroll = new ScrollView(ScrollViewMode.Vertical);
			_scroll.AddToClassList("tab-scroll");
			_list = new VisualElement();
			_scroll.Add(_list);
			Add(_scroll);

			Add(MakeSectionLabel("Test Publish"));

			var publishRow = new VisualElement();
			publishRow.style.flexDirection = FlexDirection.Row;
			publishRow.style.alignItems = Align.Center;
			publishRow.style.marginBottom = 2;

			_publishTypeDropdown = new DropdownField("Message")
			{
				tooltip = "Concrete IMessage type to publish. The broker receives default(T) — useful for smoke-testing handler wiring without authoring real payloads."
			};
			_publishTypeDropdown.style.flexGrow = 1;
			_publishTypeDropdown.style.minWidth = 200;
			publishRow.Add(_publishTypeDropdown);

			_publishButton = new Button(OnPublishTest) { text = "Publish default(T)" };
			_publishButton.AddToClassList("row-btn");
			_publishButton.style.marginLeft = 6;
			publishRow.Add(_publishButton);

			Add(publishRow);

			_publishStatus = new Label();
			_publishStatus.style.fontSize = 10;
			_publishStatus.style.color = new StyleColor(new Color(0.5f, 0.9f, 0.5f));
			_publishStatus.style.marginLeft = 2;
			_publishStatus.style.marginBottom = 4;
			Add(_publishStatus);

			var bar = MakeActionBar();
			bar.Add(MakePrimaryDangerButton("Unsubscribe All", OnUnsubscribeAll));
			Add(bar);

			RebuildMessageTypeChoices();
		}

		protected override void Refresh()
		{
			RebuildMessageTypeChoicesIfStale();
			RefreshSubscriptionList();
		}

		// 사용자가 Play 모드를 멈추면 구독 목록을 동기적으로 강제로 비웁니다.
		// OnDestroy 에서 브로커를 해제하거나 MainInstaller.Clean() 을 호출하지 않는 부트스트랩에 대한
		// 이중 안전장치입니다. 이 보호가 없으면 브로커의 Subscriptions 딕셔너리는 서비스 인스턴스에 남아
		// 정적 MainInstaller 필드가 초기화될 때(다음 도메인 리로드)까지 유지됩니다. 참고: ServiceTab 도
		// Play 모드 전환 때 갱신 다이제스트를 무효화하므로, 씬 정리 뒤에 실행되는 지연 갱신은 건너뛰지 않고
		// 처음부터 다시 만듭니다.
		protected override void OnExitingPlayMode()
		{
			_list.Clear();
			_list.Add(MakeEmptyLabel());
		}

		private void RefreshSubscriptionList()
		{
			var isPlaying = UnityEditor.EditorApplication.isPlaying;
			var broker = isPlaying ? TryResolve<IMessageBrokerService>() as MessageBrokerService : null;
			var digest = ComputeDigest(isPlaying, broker);

			// 바뀐 것이 없으면 재구성을 건너뜁니다(ServiceTab.TryShortCircuitRefresh 참고).
			// 주기 타이머가 클릭 도중 마우스를 캡처한 VisualElement 를 파괴해 빠른 폴드아웃 클릭이
			// 사라지는 것을 막아 줍니다.
			if (TryShortCircuitRefresh(digest))
			{
				return;
			}

			_list.Clear();

			// Edit 모드(처음이든 플레이 세션이 끝난 뒤든)에서는 정적 필드에 남은 MessageBrokerService 가
			// 있더라도 구독자 상태를 숨깁니다. OnExitingPlayMode() 와 함께, 사용하는 쪽 부트스트랩이
			// OnDestroy 에서 브로커 해제나 MainInstaller.Clean() 호출을 잊었더라도 Stop 이후 구독 목록이
			// 살아 있는 스냅샷을 유지하지 않도록 보장합니다.
			if (!isPlaying)
			{
				_list.Add(MakeEmptyLabel());
				return;
			}

			if (broker == null)
			{
				_list.Add(MakeEmptyLabel("IMessageBrokerService not bound"));
				return;
			}

			var subs = broker.Subscriptions;

			if (subs.Count == 0)
			{
				_list.Add(MakeEmptyLabel());
				return;
			}

			foreach (var kvp in subs)
			{
				var messageType = kvp.Key;
				var subscribers = kvp.Value;

				// 주기 Refresh 가 틱마다 다시 펼치지 않도록 상태가 유지되는 폴드아웃을 씁니다.
				var foldout = MakeStickyFoldout(
					key: messageType.FullName ?? messageType.Name,
					text: $"{messageType.Name}  ({subscribers.Count})");
				foldout.AddToClassList("section-foldout");

				// Unsubscribe 버튼을 폴드아웃 헤더 행(타입 이름 + 개수 옆)에 둡니다. 파괴적 동작을
				// 구독자 목록 맨 아래에 묻어 두지 않고 대상 옆에 배치하기 위해서입니다.
				var headerToggle = foldout.Q<Toggle>();

				if (headerToggle != null)
				{
					headerToggle.style.flexDirection = FlexDirection.Row;
					headerToggle.style.alignItems = Align.Center;

					var headerSpacer = new VisualElement();
					headerSpacer.style.flexGrow = 1;
					headerToggle.Add(headerSpacer);

					var headerUnsubBtn = MakeRowButton("Unsubscribe All", () => OnUnsubscribeType(broker, messageType), danger: true);
					// 클릭이 토글로 버블링되지 않게 막습니다(그렇지 않으면 파괴적 동작을 실행할 때마다
					// 폴드아웃이 펼쳐지거나 접힙니다).
					headerUnsubBtn.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
					headerToggle.Add(headerUnsubBtn);
				}

				foreach (var sub in subscribers)
				{
					var targetName = sub.Key?.GetType().Name ?? "(null)";
					var methodName = (sub.Value as Delegate)?.Method?.Name ?? "?";
					var subRow = MakeRow($"  ↳ {targetName}", methodName);
					foldout.Add(subRow);
				}

				_list.Add(foldout);
			}
		}

		/// <summary>
		/// 재구성 경로가 그리는 모든 상태로 결정적 다이제스트를 만듭니다: Edit 모드 빈 상태, 미바인딩,
		/// 구독별 <c>(messageType, [target.method, ...])</c> 튜플. 연속된 두 갱신의 다이제스트가 같으면
		/// 재구성을 건너뛰어 250 ms 타이머가 빠른 폴드아웃 클릭을 클릭 도중 파괴하지 않게 합니다.
		/// </summary>
		private static string ComputeDigest(bool isPlaying, MessageBrokerService broker)
		{
			if (!isPlaying)
			{
				return "<edit>";
			}
			if (broker == null)
			{
				return "<unbound>";
			}

			var sb = new StringBuilder();

			foreach (var kvp in broker.Subscriptions)
			{
				sb.Append(kvp.Key.FullName ?? kvp.Key.Name).Append('[');
				foreach (var sub in kvp.Value)
				{
					sb.Append(sub.Key?.GetType().FullName ?? "(null)").Append('.');
					sb.Append((sub.Value as Delegate)?.Method?.Name ?? "?").Append(',');
				}
				sb.Append(']');
			}
			return sb.ToString();
		}

		// 로드된 어셈블리 수가 바뀔 때만 다시 스캔합니다(갱신마다 확인해도 비용이 작음).
		// 틱마다 전체 리플렉션 스캔을 하며 생기는 할당 비용을 피합니다.
		private void RebuildMessageTypeChoicesIfStale()
		{
			var current = AppDomain.CurrentDomain.GetAssemblies().Length;

			if (current == _lastDiscoveredAssemblyCount)
			{
				return;
			}

			RebuildMessageTypeChoices();
		}

		private void RebuildMessageTypeChoices()
		{
			_messageTypes.Clear();
			_lastDiscoveredAssemblyCount = AppDomain.CurrentDomain.GetAssemblies().Length;

			foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type[] types;

				try
				{
					types = asm.GetTypes();
				}
				catch (System.Reflection.ReflectionTypeLoadException ex)
				{
					types = ex.Types.Where(t => t != null).ToArray();
				}
				catch
				{
					continue;
				}

				foreach (var type in types)
				{
					if (type == null || type.IsAbstract || type.IsInterface)
					{
						continue;
					}

					if (!typeof(IMessage).IsAssignableFrom(type))
					{
						continue;
					}

					_messageTypes.Add(type);
				}
			}

			_messageTypes.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));

			if (_messageTypes.Count == 0)
			{
				_publishTypeDropdown.choices = new List<string> { "(no IMessage types found)" };
				_publishTypeDropdown.index = 0;
				_publishTypeDropdown.SetEnabled(false);
				_publishButton.SetEnabled(false);
				return;
			}

			var labels = BuildShortLabelChoices(_messageTypes);
			var previousValue = _publishTypeDropdown.value;
			_publishTypeDropdown.choices = labels;
			_publishTypeDropdown.SetEnabled(true);
			_publishButton.SetEnabled(true);

			var restoredIndex = labels.IndexOf(previousValue);
			_publishTypeDropdown.index = restoredIndex >= 0 ? restoredIndex : 0;
		}

		// 읽기 쉽도록 Type.Name 을 우선 쓰고, 짧은 이름이 겹치면 FullName 으로 대체해
		// 예를 들어 NamespaceA.Foo 와 NamespaceB.Foo 를 구분할 수 있게 합니다.
		private static List<string> BuildShortLabelChoices(IReadOnlyList<Type> types)
		{
			var nameCounts = new Dictionary<string, int>();

			for (var i = 0; i < types.Count; i++)
			{
				var name = types[i].Name;
				nameCounts[name] = nameCounts.TryGetValue(name, out var n) ? n + 1 : 1;
			}

			var labels = new List<string>(types.Count);

			for (var i = 0; i < types.Count; i++)
			{
				var t = types[i];
				labels.Add(nameCounts[t.Name] > 1 ? t.FullName : t.Name);
			}

			return labels;
		}

		private void OnUnsubscribeType(MessageBrokerService broker, Type messageType)
		{
			if (!EditorUtility.DisplayDialog("Unsubscribe",
				$"Remove ALL subscribers for {messageType.Name}?", "Remove", "Cancel"))
			{
				return;
			}

			var method = typeof(IMessageBrokerService)
				.GetMethod(nameof(IMessageBrokerService.Unsubscribe))
				?.MakeGenericMethod(messageType);
			method?.Invoke(broker, new object[] { null });
			Refresh();
		}

		private void OnUnsubscribeAll()
		{
			var broker = TryResolve<IMessageBrokerService>();

			if (broker == null)
			{
				return;
			}

			if (!EditorUtility.DisplayDialog("UnsubscribeAll",
				"Remove ALL subscriptions from the message broker?", "Remove All", "Cancel"))
			{
				return;
			}

			broker.UnsubscribeAll(null);
			Refresh();
		}

		private void OnPublishTest()
		{
			_publishStatus.text = "";

			var broker = TryResolve<IMessageBrokerService>();

			if (broker == null)
			{
				_publishStatus.text = "not bound";
				return;
			}

			var index = _publishTypeDropdown.index;

			if (index < 0 || index >= _messageTypes.Count)
			{
				_publishStatus.text = "select a message type";
				return;
			}

			var type = _messageTypes[index];

			try
			{
				var msg = Activator.CreateInstance(type);
				var publishMethod = typeof(IMessageBrokerService)
					.GetMethod(nameof(IMessageBrokerService.Publish))
					?.MakeGenericMethod(type);
				publishMethod?.Invoke(broker, new[] { msg });
				_publishStatus.text = $"published {type.Name}";
			}
			catch (MissingMethodException)
			{
				_publishStatus.text = $"{type.Name} has no parameterless ctor";
			}
			catch (Exception ex)
			{
				_publishStatus.text = "error";
				Debug.LogError($"[ServicesExplorer] Publish test threw: {ex.Message}");
			}
		}
	}
}
