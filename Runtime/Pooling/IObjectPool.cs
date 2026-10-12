using System;
using System.Collections.Generic;

// ReSharper disable CheckNamespace

namespace Geuneda.Services.Pooling
{
	/// <summary>
	/// 어떤 타입의 엔티티 객체든 다룰 수 있는 단순한 오브젝트 풀 구현
	/// </summary>
	public interface IObjectPool : IDisposable
	{
		/// <summary>
		/// 스폰된 모든 활성 엔티티를 디스폰하고, 나중에 다시 쓸 수 있도록 풀로 되돌립니다.
		/// 이 함수는 엔티티를 초기화(reset)하지 않습니다. 초기화가 필요하면 엔티티가 <see cref="IPoolEntityDespawn"/> 을 구현하거나 외부에서 처리하세요
		/// </summary>
		void DespawnAll();

		/// <inheritdoc cref="IDisposable.Dispose"/>
		/// <remarks>
		/// <paramref name="disposeSampleEntity"/> 값에 따라 샘플 엔티티도 해제합니다
		/// </remarks>
		void Dispose(bool disposeSampleEntity);
	}

	/// <inheritdoc />
	public interface IObjectPool<T> : IObjectPool where T : class
	{
		/// <summary>
		/// 풀 엔티티를 만들 때 쓰는 원본 엔티티 참조
		/// </summary>
		T SampleEntity { get; }

		/// <summary>
		/// 이미 스폰된 요소 컬렉션을 읽기 전용 리스트로 가져옵니다
		/// </summary>
		IReadOnlyList<T> SpawnedReadOnly { get; }

		/// <summary>
		/// 주어진 <paramref name="conditionCheck"/> 를 만족하는 엔티티가 스폰되어 있는지 확인합니다
		/// </summary>
		bool IsSpawned(Func<T, bool> conditionCheck);

		/// <summary>
		/// 풀의 모든 엔티티를 정리하고 주어진 <paramref name="initSize"/> 로 다시 채웁니다
		/// </summary>
		void Reset(uint initSize, T sampleEntity);

		/// <summary>
		/// <typeparamref name="T"/> 타입의 엔티티를 스폰해 반환합니다.
		/// 이 함수는 엔티티를 초기화하지 않습니다. 초기화가 필요하면 엔티티가 <see cref="IPoolEntitySpawn"/> 을 구현하거나
		/// 외부에서 처리하세요.
		/// 풀에 대기 중인 엔티티가 없으면 인스턴시에이터로 새 엔티티를 만들어 반환합니다(풀이 늘어남)
		/// </summary>
		T Spawn();

		/// <inheritdoc cref="Spawn"/>
		/// <remarks>
		/// 이 인터페이스로 풀 객체를 <typeparamref name="T"/> 의 <paramref name="data"/> 와 함께 스폰할 수 있습니다
		/// </remarks>
		T Spawn<TData>(TData data);

		/// <summary>
		/// 주어진 <paramref name="entityGetter"/> 조건을 만족하는 엔티티를 디스폰하고, 나중에 다시 쓸 수 있도록
		/// 풀로 되돌립니다.
		/// <paramref name="onlyFirst"/> 가 true 이면 엔티티 하나만 디스폰하고 <paramref name="entityGetter"/> 조건을
		/// 만족하는 다른 엔티티는 찾지 않습니다.
		/// 이 함수는 엔티티를 초기화하지 않습니다. 초기화가 필요하면 엔티티가 <see cref="IPoolEntityDespawn"/> 을 구현하거나
		/// 외부에서 처리하세요.
		/// 엔티티를 풀로 되돌리는 데 성공하면 true, 아니면 false 를 반환합니다
		/// </summary>
		bool Despawn(bool onlyFirst, Func<T, bool> entityGetter);

		/// <summary>
		/// 주어진 <paramref name="entity"/> 를 디스폰하고 나중에 다시 쓸 수 있도록 풀로 되돌립니다.
		/// 이 함수는 엔티티를 초기화하지 않습니다. 초기화가 필요하면 엔티티가 <see cref="IPoolEntityDespawn"/> 을 구현하거나
		/// 외부에서 처리하세요.
		/// 엔티티를 풀로 되돌리는 데 성공하면 true, 아니면 false 를 반환합니다.
		/// </summary>
		bool Despawn(T entity);

		/// <summary>
		/// 이 풀의 내용을 비웁니다.
		/// 풀이 가지고 있던 내용을 반환하므로 따로 해제할 수 있습니다
		/// </summary>
		List<T> Clear();
	}
}
