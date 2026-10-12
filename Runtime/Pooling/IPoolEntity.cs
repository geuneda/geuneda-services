// ReSharper disable CheckNamespace

namespace Geuneda.Services.Pooling
{
	/// <summary>
	/// 풀 객체가 스폰될 때 알림을 받을 수 있게 하는 인터페이스
	/// </summary>
	public interface IPoolEntitySpawn
	{
		/// <summary>
		/// 엔티티가 스폰될 때 호출됩니다
		/// </summary>
		void OnSpawn();
	}

	/// <inheritdoc cref="IPoolEntitySpawn"/>
	/// <remarks>
	/// 이 인터페이스로 풀 객체를 <typeparamref name="T"/> 의 <paramref name="data"/> 와 함께 스폰할 수 있습니다
	/// </remarks>
	public interface IPoolEntitySpawn<T>
	{
		/// <inheritdoc cref="IPoolEntitySpawn.OnSpawn"/>
		/// <remarks>
		/// 풀 객체를 <typeparamref name="T"/> 의 <paramref name="data"/> 와 함께 스폰할 수 있게 합니다
		/// </remarks>
		void OnSpawn(T data);
	}

	/// <summary>
	/// 풀 객체가 디스폰될 때 알림을 받을 수 있게 하는 인터페이스
	/// </summary>
	public interface IPoolEntityDespawn
	{
		/// <summary>
		/// 엔티티가 디스폰될 때 호출됩니다
		/// </summary>
		void OnDespawn();
	}

	/// <summary>
	/// 디스폰을 호출할 풀 참조를 보관해 스스로 디스폰할 수 있게 하는 인터페이스
	/// </summary>
	/// <remarks>
	/// 이 클래스의 구현 예:
	/// <code>
	/// public class MyObjectPool : IPoolEntityObject<typeparamref name="T"/>
	/// {
	///		private IObjectPool<typeparamref name="T"/> _pool;
	///
	/// 	public void Init(IObjectPool<typeparamref name="T"/> pool)
	/// 	{
	/// 		_pool = pool;
	/// 	}
	///
	/// 	public bool Despawn()
	/// 	{
	/// 		return _pool.Despawn(this);
	/// 	}
	/// }
	/// </code>
	/// </remarks>
	public interface IPoolEntityObject<T> where T : class
	{
		/// <summary>
		/// <see cref="IObjectPool{T}"/> 가 주어진 <paramref name="pool"/> 로 초기화할 때 호출합니다
		/// </summary>
		void Init(IObjectPool<T> pool);

		/// <summary>
		/// 이 풀 객체를 디스폰합니다
		/// </summary>
		bool Despawn();
	}
}
