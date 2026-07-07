# Pool Service

[← Back to index](README.md)

Efficient object pooling with lifecycle hooks.

**Key Points:**
- `PoolService` keeps **one pool per type**; `AddPool<T>()` throws (`ArgumentException`) if the type is already registered
- `GameObjectPool<T>` requires `T : Behaviour`; use it when you want a typed component reference on spawn
- `GameObjectPool` (non-generic) works with raw `GameObject` references
- `ObjectPool<T>` is a generic pool; lifecycle hooks are called via direct cast
- `CallOnSpawned`/`CallOnDespawned` are **virtual** in `ObjectPoolBase<T>` — override to customize dispatch
- `GameObjectPool.Dispose(bool disposeSampleEntity)`: `true` destroys the sample entity too; `false` (overload) destroys pooled instances only

```csharp
var poolService = new PoolService();

// Create and register a pool
var bulletPool = new GameObjectPool<Bullet>(initSize: 50, bulletPrefab);
poolService.AddPool(bulletPool);

// Spawn / Despawn
var bullet = poolService.Spawn<Bullet>();
poolService.Despawn(bullet);

// Spawn with data (entity must implement IPoolEntitySpawn<BulletData>)
var bullet = poolService.Spawn<Bullet, BulletData>(new BulletData { Damage = 100 });

// Direct pool access
IObjectPool<Bullet> pool = poolService.GetPool<Bullet>();
pool.DespawnAll();

// Despawn all via service
poolService.DespawnAll<Bullet>();

// Remove pool (does not destroy entities)
poolService.RemovePool<Bullet>();

// Dispose pool and optionally destroy sample entity
poolService.Dispose<Bullet>(disposeSampleEntity: true);
```

## Lifecycle Hook Interfaces

Implement on your entity class to receive pool events:

| Interface | When Called |
|-----------|-------------|
| `IPoolEntitySpawn` | On every spawn (no data) |
| `IPoolEntitySpawn<TData>` | On spawn with typed data |
| `IPoolEntityDespawn` | On despawn |
| `IPoolEntityObject<T>` | On first creation — receives pool reference for self-despawn |

**`ObjectPool<T>`** calls hooks via `(entity as IPoolEntitySpawn)?.OnSpawn()`.
**`GameObjectPool` / `GameObjectPool<T>`** call hooks via `entity.GetComponent<IPoolEntitySpawn>()?.OnSpawn()`.

## Error Reference

| Call | Exception | Condition |
|------|-----------|-----------|
| `AddPool<T>(pool)` duplicate | `ArgumentException` | Pool for `T` already registered |
