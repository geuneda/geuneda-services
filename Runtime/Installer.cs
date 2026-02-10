using Geuneda.Services;
using System;
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
		private readonly Dictionary<Type, object> _bindings = new Dictionary<Type, object>();

		/// <inheritdoc />
		public IInstaller Bind<T>(T instance) where T : class
		{
			var type = typeof(T);

			if (!type.IsInterface)
			{
				throw new ArgumentException($"Cannot bind {instance} because {type} is not an interface");
			}

			_bindings.Add(type, instance);

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
			return _bindings.Remove(typeof(T));
		}

		/// <inheritdoc />
		public void Clean()
		{
			_bindings.Clear();
		}
	}
}