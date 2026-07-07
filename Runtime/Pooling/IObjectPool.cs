using System;
using System.Collections.Generic;

// ReSharper disable CheckNamespace

namespace Geuneda.Services.Pooling
{
	/// <summary>
	/// Simple object pool implementation that can handle any type of entity objects
	/// </summary>
	public interface IObjectPool : IDisposable
	{
		/// <summary>
		/// Despawns all active spawned entities and returns them back to the pool to be used again later
		/// This function does not reset the entity. For that, have the entity implement <see cref="IPoolEntityDespawn"/> or do it externally
		/// </summary>
		void DespawnAll();
		
		/// <inheritdoc cref="IDisposable.Dispose"/>
		/// <remarks>
		/// Will also dispose the sample entity depending on the value of <paramref name="disposeSampleEntity"/>
		/// </remarks>
		void Dispose(bool disposeSampleEntity);
	}

	/// <inheritdoc />
	public interface IObjectPool<T> : IObjectPool where T : class
	{
		/// <summary>
		/// The entity reference used to create the pooled entities
		/// </summary>
		T SampleEntity { get; }
		
		/// <summary>
		/// Requests the collection of already spawned elements as a read only list
		/// </summary>
		IReadOnlyList<T> SpawnedReadOnly { get; }

		/// <summary>
		/// Checks if there is an entity in the pool that matches the given <paramref name="conditionCheck"/>
		/// </summary>
		bool IsSpawned(Func<T, bool> conditionCheck);

		/// <summary>
		/// Clears any entities in the pool and resets it to the given <paramref name="initSize"/>
		/// </summary>
		void Reset(uint initSize, T sampleEntity);

		/// <summary>
		/// Spawns and returns an entity of the given type <typeparamref name="T"/>
		/// This function does not initialize the entity. For that, have the entity implement <see cref="IPoolEntitySpawn"/>
		/// or do it externally
		/// This function throws a <exception cref="StackOverflowException" /> if the pool is empty
		/// </summary>
		T Spawn();

		/// <inheritdoc cref="Spawn"/>
		/// <remarks>
		/// This interface allows to spawn the pooled object with the given <typeparamref name="T"/> <paramref name="data"/>
		/// </remarks>
		T Spawn<TData>(TData data);

		/// <summary>
		/// Despawns the entity that is valid with the given <paramref name="entityGetter"/> condition and returns it back to
		/// the pool to be used again later.
		/// If the given <paramref name="onlyFirst"/> is true then will only despawn one entity and not find more entities
		/// that match the given <paramref name="entityGetter"/> condition.
		/// This function does not reset the entity. For that, have the entity implement <see cref="IPoolEntityDespawn"/>
		/// or do it externally.
		/// Returns true if was able to despawn the entity back to the pool successfully, false otherwise
		/// </summary>
		bool Despawn(bool onlyFirst, Func<T, bool> entityGetter);

		/// <summary>
		/// Despawns the given <paramref name="entity"/> and returns it back to the pool to be used again later.
		/// This function does not reset the entity. For that, have the entity implement <see cref="IPoolEntityDespawn"/>
		/// or do it externally.
		/// Returns true if was able to despawn the entity back to the pool successfully, false otherwise.
		/// </summary>
		bool Despawn(T entity);

		/// <summary>
		/// Clears the contents out of this pool.
		/// Returns back its pool contents so they can be independently disposed
		/// </summary>
		List<T> Clear();
	}
}
