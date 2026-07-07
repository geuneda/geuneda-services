// ReSharper disable CheckNamespace

namespace Geuneda.Services.Pooling
{
	/// <summary>
	/// This interface allows pooled objects to be notified when it is spawned
	/// </summary>
	public interface IPoolEntitySpawn
	{
		/// <summary>
		/// Invoked when the Entity is spawned
		/// </summary>
		void OnSpawn();
	}

	/// <inheritdoc cref="IPoolEntitySpawn"/>
	/// <remarks>
	/// This interface allows to spawn the pooled object with the given <typeparamref name="T"/> <paramref name="data"/>
	/// </remarks>
	public interface IPoolEntitySpawn<T>
	{
		/// <inheritdoc cref="IPoolEntitySpawn.OnSpawn"/>
		/// <remarks>
		/// Allows to spawn the pooled object with the given <typeparamref name="T"/> <paramref name="data"/>
		/// </remarks>
		void OnSpawn(T data);
	}

	/// <summary>
	/// This interface allows pooled objects to be notified when it is despawned
	/// </summary>
	public interface IPoolEntityDespawn
	{
		/// <summary>
		/// Invoked when the entity is despawned
		/// </summary>
		void OnDespawn();
	}

	/// <summary>
	/// This interface allows to self despawn by maintaining the reference of the despawing call
	/// </summary>
	/// <remarks>
	/// Implemenation of this class:
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
		/// Called by the <see cref="IObjectPool{T}"/> to initialize by the given <paramref name="pool"/>
		/// </summary>
		void Init(IObjectPool<T> pool);

		/// <summary>
		/// Despawns this pooled object
		/// </summary>
		bool Despawn();
	}
}
