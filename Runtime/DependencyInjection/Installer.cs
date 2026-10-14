using Geuneda.Core;
using Geuneda.Services;
using System;
using System.Buffers;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <summary>
	/// 바인딩 인스톨러의 컨테이너로 정의된 객체를 표시합니다.
	/// 모든 바인딩된 인터페이스의 로케이터 역할을 합니다.
	/// 인터페이스만 바인딩할 수 있습니다.
	/// </summary>
	/// <remarks>
	/// "제어의 역전" 원칙을 따릅니다 <see cref="https://en.wikipedia.org/wiki/Inversion_of_control"/>
	/// </remarks>
	public interface IInstaller
	{
		/// <summary>
		/// 인터페이스 <typeparamref name="T"/>를 주어진 <paramref name="instance"/>에 바인딩합니다
		/// </summary>
		/// <exception cref="ArgumentException">
		/// 주어진 <paramref name="instance"/>가 <typeparamref name="T"/> 인터페이스를 구현하지 않으면 발생합니다
		/// </exception>
		/// <returns>
		/// 체인 호출을 위한 이 인스톨러 참조
		/// </returns>
		IInstaller Bind<T>(T instance) where T : class;

		/// <summary>
		/// 여러 타입의 인터페이스를 주어진 <paramref name="instance"/>에 바인딩합니다
		/// </summary>
		/// <exception cref="ArgumentException">
		/// 주어진 <paramref name="instance"/>가 모든 타입 인터페이스를 구현하지 않으면 발생합니다
		/// </exception>
		/// <returns>
		/// 체인 호출을 위한 이 인스톨러 참조
		/// </returns>
		IInstaller Bind<T, T1, T2>(T instance)
			where T : class, T1, T2
			where T1 : class
			where T2 : class;

		/// <summary>
		/// 여러 타입의 인터페이스를 주어진 <paramref name="instance"/>에 바인딩합니다
		/// </summary>
		/// <exception cref="ArgumentException">
		/// 주어진 <paramref name="instance"/>가 모든 타입 인터페이스를 구현하지 않으면 발생합니다
		/// </exception>
		/// <returns>
		/// 체인 호출을 위한 이 인스톨러 참조
		/// </returns>
		IInstaller Bind<T, T1, T2, T3>(T instance) 
			where T : class, T1, T2, T3
			where T1 : class
			where T2 : class
			where T3 : class;

		/// <summary>
		/// <typeparamref name="T"/> 타입에 바인딩된 인스턴스를 요청합니다.
		/// 인스턴스가 바인딩되어 있으면 true를 반환합니다.
		/// </summary>
		bool TryResolve<T>(out T instance);

		/// <summary>
		/// <typeparamref name="T"/> 타입에 바인딩된 인스턴스를 요청합니다
		/// </summary>
		/// <exception cref="ArgumentException">
		/// 주어진 <typeparamref name="T"/> 타입이 아직 바인딩되지 않았으면 발생합니다
		/// </exception>
		T Resolve<T>();

		/// <summary>
		/// 인스톨러에서 주어진 <typeparamref name="T"/> 타입의 바인딩을 정리합니다.
		/// 게임 상태를 초기화할 때 유용합니다.
		/// 주어진 <typeparamref name="T"/> 타입을 성공적으로 정리하면 TRUE, 그렇지 않으면 FALSE를 반환합니다.
		/// </summary>
		bool Clean<T>() where T : class;

		/// <summary>
		/// 인스톨러의 모든 바인딩을 정리합니다.
		/// 게임 상태를 초기화할 때 유용합니다.
		/// </summary>
		void Clean();
	}
	
	/// <inheritdoc />
	public class Installer : IInstaller
	{
		private static readonly ModuleLogger Logger = new ModuleLogger("Geuneda.Services.Installer");

		private readonly Dictionary<Type, object> _bindings = new Dictionary<Type, object>();
		private readonly List<Action<Type>> _bindingChangedHandlers = new List<Action<Type>>();

		internal IReadOnlyDictionary<Type, object> Bindings => _bindings;

		/// <summary>
		/// 바인딩이 추가되거나 지워진 뒤 그 인터페이스 타입과 함께 발생합니다. 처리기 안에서 해석하면 바뀐 상태가 보입니다.
		/// 늦게 바인딩되거나 교체되는 서비스를 따라가야 하는 쪽(예: 화면 컴포넌트)이 구독합니다.
		/// </summary>
		/// <remarks>
		/// <list type="bullet">
		/// <item><c>Bind&lt;T, T1, T2&gt;</c> 처럼 여러 인터페이스로 바인딩하면 인터페이스마다 한 번씩 발생합니다.
		/// <see cref="Clean()"/> 은 모두 지운 뒤 지운 인터페이스마다 한 번씩 발생합니다.</item>
		/// <item>예외로 실패한 인터페이스 바인딩이나 지운 것이 없는 <see cref="Clean{T}"/> 에서는 발생하지 않습니다.
		/// 다중 인터페이스 <c>Bind</c> 는 인터페이스마다 차례로 바인딩하므로, 뒤쪽 인터페이스에서 예외가 나도
		/// 앞서 성공한 바인딩은 남고 그 알림도 이미 발생한 상태입니다.</item>
		/// <item>처리기 예외는 로그로 남기고 나머지 처리기를 계속 호출합니다. <c>Bind</c>/<c>Clean</c> 호출자에게 전파하지 않습니다.</item>
		/// <item>처리기 목록은 발생 시점의 스냅샷으로 순회합니다. 처리기 안에서 해제한 처리기도 이번 알림에서는 호출될 수 있습니다(일반 C# 이벤트와 같음).</item>
		/// <item>일반 이벤트와 달리 += / -= 마다 처리기 목록 전체를 새로 만들지 않습니다(목록 용량이 늘 때만 할당). 구독자가 많아도
		/// 등록·해제가 가비지를 만들지 않습니다. 해제는 등록한 델리게이트와 같은 것 중 마지막 하나를 지우고,
		/// 합성 델리게이트(a + b)는 처리기 하나로 다룹니다.</item>
		/// </list>
		/// </remarks>
		public event Action<Type> BindingChanged
		{
			add
			{
				if (value != null)
				{
					_bindingChangedHandlers.Add(value);
				}
			}
			remove
			{
				if (value == null)
				{
					return;
				}

				var index = _bindingChangedHandlers.LastIndexOf(value);

				if (index >= 0)
				{
					_bindingChangedHandlers.RemoveAt(index);
				}
			}
		}

		/// <inheritdoc />
		public IInstaller Bind<T>(T instance) where T : class
		{
			var type = typeof(T);

			if (!type.IsInterface)
			{
				throw new ArgumentException($"Cannot bind {instance} because {type} is not an interface");
			}

			_bindings.Add(type, instance);
			RaiseBindingChanged(type);

			return this;
		}

		/// <inheritdoc />
		public IInstaller Bind<T, T1, T2>(T instance)
			where T : class, T1, T2
			where T1 : class
			where T2 : class
		{
			Bind<T1>(instance);
			Bind<T2>(instance);

			return this;
		}

		/// <inheritdoc />
		public IInstaller Bind<T, T1, T2, T3>(T instance)
			where T : class, T1, T2, T3
			where T1 : class
			where T2 : class
			where T3 : class
		{
			Bind<T1>(instance);
			Bind<T2>(instance);
			Bind<T3>(instance);

			return this;
		}

		/// <inheritdoc />
		public bool TryResolve<T>(out T instance)
		{
			var ret = _bindings.TryGetValue(typeof(T), out object inst);

			instance = (T)inst;

			return ret;
		}

		/// <inheritdoc />
		public T Resolve<T>()
		{
			if (!_bindings.TryGetValue(typeof(T), out object instance))
			{
				throw new ArgumentException($"The type {typeof(T)} is not binded");
			}

			return (T) instance;
		}

		/// <inheritdoc />
		public bool Clean<T>() where T : class
		{
			if (!_bindings.Remove(typeof(T)))
			{
				return false;
			}

			RaiseBindingChanged(typeof(T));

			return true;
		}

		/// <inheritdoc />
		public void Clean()
		{
			if (_bindings.Count == 0)
			{
				return;
			}

			// 모두 지운 뒤에 알립니다. 처리기가 일부만 지워진 상태를 보지 않게 합니다.
			var types = new Type[_bindings.Count];
			_bindings.Keys.CopyTo(types, 0);
			_bindings.Clear();

			foreach (var type in types)
			{
				RaiseBindingChanged(type);
			}
		}

		/// <summary>
		/// 처리기와 바인딩을 알림 없이 모두 버립니다. 플레이 세션이 시작될 때 이전 세션 상태를 지우는 데 씁니다.
		/// 이전 세션의 처리기는 이미 파괴된 오브젝트를 가리킬 수 있으므로 호출하지 않습니다.
		/// </summary>
		internal void Reset()
		{
			_bindingChangedHandlers.Clear();
			_bindings.Clear();
		}

		private void RaiseBindingChanged(Type type)
		{
			var count = _bindingChangedHandlers.Count;

			if (count == 0)
			{
				return;
			}

			// 처리기가 등록·해제해도 이번 알림 대상이 바뀌지 않도록 스냅샷으로 순회합니다.
			var snapshot = ArrayPool<Action<Type>>.Shared.Rent(count);
			_bindingChangedHandlers.CopyTo(snapshot);

			try
			{
				for (var i = 0; i < count; i++)
				{
					try
					{
						snapshot[i](type);
					}
					catch (Exception exception)
					{
						// 구독자 하나의 오류가 다른 구독자의 갱신과 Bind/Clean 호출자까지 막지 않도록 기록만 합니다.
						Logger.Error(exception);
					}
				}
			}
			finally
			{
				ArrayPool<Action<Type>>.Shared.Return(snapshot, true);
			}
		}
	}
}