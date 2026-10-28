# 풀 서비스

[목차로 돌아가기](README.md)

생명주기 훅을 가진 오브젝트 풀과, 타입별 풀을 모아 관리하는 `PoolService` 입니다.

## 네임스페이스

- `PoolService` (구체 클래스): `Geuneda.Services`
- 그 외 풀 타입 전부: `Geuneda.Services.Pooling` — `IPoolService`, `IObjectPool`, `IObjectPool<T>`, `ObjectPoolBase<T>`, `ObjectPool<T>`, `GameObjectPool`, `GameObjectPool<T>`, `IPoolEntitySpawn`, `IPoolEntitySpawn<T>`, `IPoolEntityDespawn`, `IPoolEntityObject<T>`

`IPoolService` 를 쓰는 코드는 `using Geuneda.Services.Pooling;` 이 필요합니다.

## 풀 종류

| 타입 | 대상 | 생명주기 훅 호출 방식 |
|------|------|------------------------|
| `ObjectPool<T>` | 순수 C# 객체 (`T : class`) | 엔티티를 직접 캐스팅 (`entity as IPoolEntitySpawn`) |
| `GameObjectPool` | `GameObject` | `TryGetComponent<IPoolEntitySpawn>()` 등 |
| `GameObjectPool<T>` | 컴포넌트 (`T : Behaviour`) | `TryGetComponent<IPoolEntitySpawn>()` 등 |

- `GameObjectPool` / `GameObjectPool<T>` 는 스폰 시 `SetActive(true)`, 디스폰 시 `SetActive(false)` 를 호출합니다. `DespawnToSampleParent`(기본 `true`)면 디스폰할 때 샘플 엔티티의 부모 아래로 되돌립니다.
- 기본 인스턴시에이터는 샘플을 샘플의 부모 아래에 `Object.Instantiate` 하고 비활성화합니다. 생성자에 `Func<T, T>` 인스턴시에이터를 넘겨 바꿀 수 있습니다.
- 풀이 비면 새로 인스턴스를 만듭니다(초기 크기는 상한이 아님).
- `ObjectPool<T>(uint initSize, Func<T> instantiator)` 생성자는 샘플 엔티티를 만들기 위해 `instantiator` 를 한 번 더 호출합니다.

## 생명주기 훅

| 인터페이스 | 호출 시점 |
|------------|-----------|
| `IPoolEntitySpawn` | 스폰할 때마다 (`Spawn()` 과 `Spawn(data)` 모두) |
| `IPoolEntitySpawn<TData>` | `Spawn(data)` 로 스폰할 때 (`IPoolEntitySpawn.OnSpawn()` 다음에 호출) |
| `IPoolEntityDespawn` | 디스폰할 때 |
| `IPoolEntityObject<T>` | 인스턴스가 처음 생성될 때 `Init(IObjectPool<T>)` 호출. 엔티티가 스스로 `Despawn()` 할 수 있게 풀 참조를 넘김 |

`ObjectPoolBase<T>` 의 `CallOnSpawned` / `CallOnDespawned` 는 `protected virtual` 이라 파생 풀에서 디스패치 방식을 바꿀 수 있습니다.

## 사용 예

```csharp
using Geuneda.Services;
using Geuneda.Services.Pooling;
using UnityEngine;

public struct BulletData
{
    public int Damage;
}

public class Bullet : MonoBehaviour, IPoolEntitySpawn<BulletData>, IPoolEntityDespawn
{
    public int Damage;

    public void OnSpawn(BulletData data) => Damage = data.Damage;
    public void OnDespawn() => Damage = 0;
}

public class BulletSpawner
{
    private readonly IPoolService _poolService;

    public BulletSpawner(IPoolService poolService, Bullet bulletPrefab)
    {
        _poolService = poolService;
        _poolService.AddPool(new GameObjectPool<Bullet>(50, bulletPrefab));
    }

    public void Fire()
    {
        var bullet = _poolService.Spawn<Bullet, BulletData>(new BulletData { Damage = 100 });

        // ...

        _poolService.Despawn(bullet);
    }

    public void Clear()
    {
        IObjectPool<Bullet> pool = _poolService.GetPool<Bullet>();
        Debug.Log($"Spawned: {pool.SpawnedReadOnly.Count}");

        _poolService.DespawnAll<Bullet>();
    }
}
```

## IPoolService API

| 메서드 | 설명 |
|--------|------|
| `AddPool<T>(IObjectPool<T> pool)` | 풀 등록. 타입당 하나만 가능 |
| `GetPool<T>()` / `TryGetPool<T>(out IObjectPool<T>)` | 풀 조회 |
| `RemovePool<T>()` | 서비스에서만 제거 (엔티티는 파괴하지 않음) |
| `Spawn<T>()` / `Spawn<T, TData>(TData data)` | 스폰. `Spawn<T, TData>` 는 `T : IPoolEntitySpawn<TData>` 제약 |
| `Despawn<T>(T entity)` | 디스폰. 성공하면 `true` |
| `DespawnAll<T>()` | 해당 타입 풀 전체 디스폰 |
| `Clear()` | 모든 풀을 서비스에서 떼어 `IDictionary<Type, IObjectPool>` 로 반환 (풀 자체는 해제하지 않음) |
| `Dispose<T>(bool disposeSampleEntity)` | 해당 풀의 `Dispose(bool)` 호출 후 서비스에서 제거 |
| `Dispose()` | 모든 풀의 `Dispose()` 호출 후 비우기 |

## 해제 동작 주의

- `IObjectPool.Dispose()` (인자 없음): 풀이 가진 엔티티(스폰된 것과 대기 중인 것)를 비웁니다. `ObjectPool<T>` 는 참조만 버리고, `GameObjectPool` 계열은 각 인스턴스의 GameObject 를 `Object.Destroy` 합니다(이미 외부에서 파괴된 항목은 건너뜀). 샘플 엔티티는 건드리지 않습니다.
- `GameObjectPool.Dispose(bool)` / `GameObjectPool<T>.Dispose(bool)`: `Dispose()` 와 같이 엔티티를 정리하고, 인자가 `true` 이면 샘플 엔티티(GameObject)도 `Object.Destroy` 하고 풀이 가진 샘플 참조를 null 로 만듭니다. `false` 이면 샘플은 그대로 둡니다(v2.1.4 이전에는 인자와 관계없이 샘플을 파괴했습니다).
- `Reset(initSize, sampleEntity)` 는 `Dispose()` 로 기존 엔티티를 정리한 뒤 새 샘플로 다시 채웁니다.
- 디스폰되어 풀에 대기 중인 GameObject 가 풀 밖에서 파괴되면, 다음 `Spawn()` 은 그 항목을 건너뛰고 다음 대기 항목을 꺼내거나 새로 만듭니다(v2.1.4 까지는 `MissingReferenceException` 이 났습니다). 인스턴시에이터가 null 이나 파괴된 객체를 돌려주면 재시도하지 않고 `InvalidOperationException` 을 던집니다.

## 예외

| 호출 | 예외 | 조건 |
|------|------|------|
| `AddPool<T>(pool)` | `ArgumentException` | `T` 타입 풀이 이미 등록됨 |
| `GetPool<T>()`, `Spawn<T>()`, `Despawn<T>()`, `DespawnAll<T>()`, `Dispose<T>(bool)` | `ArgumentException` | `T` 타입 풀이 등록되지 않음 |
