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
	/// Displays all <see cref="IMessage"/> subscriptions held by <see cref="IMessageBrokerService"/>.
	/// Supports Unsubscribe per message type, UnsubscribeAll, and a dropdown-driven test publish
	/// against any concrete <see cref="IMessage"/> implementer discovered via reflection.
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

		// Forcibly clear the subscription list synchronously when the user stops play mode.
		// Belt-and-braces against bootstraps that fail to dispose the broker / call
		// MainInstaller.Clean() in OnDestroy — the broker's Subscriptions dictionary lives
		// on the service instance and survives until the static MainInstaller field is
		// reset (next domain reload) without this guard. Note: ServiceTab also invalidates
		// the refresh digest on play-mode transitions, so the deferred refresh that lands
		// after scene teardown will rebuild from scratch instead of short-circuiting.
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

			// Skip rebuild if nothing changed — see ServiceTab.TryShortCircuitRefresh.
			// This is what keeps rapid foldout clicks from getting eaten by the periodic
			// timer destroying mouse-captured VisualElements mid-click.
			if (TryShortCircuitRefresh(digest))
			{
				return;
			}

			_list.Clear();

			// Hide subscriber state in edit mode (initial OR after a play session ended)
			// regardless of any leftover MessageBrokerService kept alive by a static field.
			// Together with OnExitingPlayMode() this guarantees the subscription list does
			// not retain a live snapshot after Stop, even if the consumer's bootstrap
			// forgot to dispose the broker / call MainInstaller.Clean() in OnDestroy.
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

				// Sticky foldouts so the periodic Refresh doesn't re-expand on every tick.
				var foldout = MakeStickyFoldout(
					key: messageType.FullName ?? messageType.Name,
					text: $"{messageType.Name}  ({subscribers.Count})");
				foldout.AddToClassList("section-foldout");

				// Place the Unsubscribe button on the foldout's header row (next to the
				// type name + count) so destructive action is co-located with the object
				// it acts on, instead of buried at the bottom of the subscriber list.
				var headerToggle = foldout.Q<Toggle>();

				if (headerToggle != null)
				{
					headerToggle.style.flexDirection = FlexDirection.Row;
					headerToggle.style.alignItems = Align.Center;

					var headerSpacer = new VisualElement();
					headerSpacer.style.flexGrow = 1;
					headerToggle.Add(headerSpacer);

					var headerUnsubBtn = MakeRowButton("Unsubscribe All", () => OnUnsubscribeType(broker, messageType), danger: true);
					// Stop the click from bubbling to the toggle (would otherwise expand/collapse
					// the foldout every time the user fires the destructive action).
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
		/// Builds a deterministic digest of every piece of state the rebuild path renders:
		/// edit-mode-empty, not-bound, and per-subscription <c>(messageType, [target.method, ...])</c>
		/// tuples. When two consecutive refreshes produce the same digest the rebuild can be
		/// skipped — keeping rapid foldout clicks from getting destroyed mid-click by the
		/// 250 ms timer.
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

		// Re-scans assemblies only when the loaded count changes (cheap to do per refresh).
		// Avoids the per-tick allocation cost of a full reflection scan.
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

		// Prefers Type.Name for readability; falls back to FullName for any ambiguous short
		// names so the user can still distinguish e.g. NamespaceA.Foo from NamespaceB.Foo.
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
