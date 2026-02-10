# Geuneda.Services - AI Agent Guide

## 1. Package Overview
- **Package**: `com.geuneda.services`
- **Unity**: 6000.0+
- **Dependencies** (see `package.json`)
  - `com.geuneda.gamedata` (**1.0.0**) (contains `floatP`, used by `RngService`)

This package provides a set of small, modular “foundation services” for Unity projects (service locator/DI-lite, messaging, ticking, coroutines, pooling, persistence, RNG, time, and build version helpers).

For user-facing docs, treat `README.md` as the primary entry point. This file is for contributors/agents working on the package itself.

## 2. Runtime Architecture (high level)
- **Service locator / bindings**: `Runtime/Installer.cs`, `Runtime/MainInstaller.cs`
  - `Installer` stores a `Dictionary<Type, object>` of interface type → instance.
  - `MainInstaller` is a **static** wrapper over a single `Installer` instance (global scope).
  - Binding is **instance-based** (`Bind<T>(T instance)`), not “type-to-type” or lifetime-managed DI.
  - Only **interfaces** can be bound (binding a non-interface throws).
- **Messaging**: `Runtime/MessageBrokerService.cs`
  - Message contract: `IMessage`
  - Pub/sub via `IMessageBrokerService` (`Publish`, `PublishSafe`, `Subscribe`, `Unsubscribe`, `UnsubscribeAll`)
  - Stores subscribers keyed by `action.Target` (so **static method subscriptions are not supported**).
- **Tick / update fan-out**: `Runtime/TickService.cs`
  - Creates a `DontDestroyOnLoad` GameObject with `TickServiceMonoBehaviour` to drive Update/LateUpdate/FixedUpdate callbacks.
  - Supports “buffered” ticking (`deltaTime`) and optional overflow carry (`timeOverflowToNextTick`) to reduce drift.
  - Uses scaled (`Time.time`) or unscaled (`Time.realtimeSinceStartup`) time depending on `realTime`.
- **Coroutine host**: `Runtime/CoroutineService.cs`
  - Creates a `DontDestroyOnLoad` GameObject with `CoroutineServiceMonoBehaviour` to run coroutines from pure C# code.
  - `IAsyncCoroutine` / `IAsyncCoroutine<T>` wraps a Unity `Coroutine` and offers completion callbacks + state flags.
- **Pooling**:
  - Pool registry: `Runtime/PoolService.cs` (`PoolService : IPoolService`)
  - Pool implementations: `Runtime/ObjectPool.cs`
    - Generic `ObjectPool<T>`
    - Unity-specific: `GameObjectPool`, `GameObjectPool<TBehaviour>`
  - Lifecycle hooks: `IPoolEntitySpawn`, `IPoolEntitySpawn<TData>`, `IPoolEntityDespawn`, `IPoolEntityObject<T>`
- **Persistence**: `Runtime/DataService.cs`
  - In-memory store keyed by `Type`
  - Disk persistence via `PlayerPrefs` + `Newtonsoft.Json` serialization
- **Time + manipulation**: `Runtime/TimeService.cs`
  - `ITimeService` + `ITimeManipulator` for querying time (Unity / Unix / DateTime UTC) and applying offsets.
- **Deterministic RNG**: `Runtime/RngService.cs`
  - Deterministic RNG state stored in `RngData` and exposed via `IRngData`.
  - Float API uses `floatP` (from `com.geuneda.gamedata`) for deterministic float math.
- **Build/version info**: `Runtime/VersionServices.cs`
  - Runtime access to version strings and git/build metadata loaded from a Resources TextAsset.

## 3. Key Directories / Files
- **Runtime**: `Runtime/`
  - Entry points: `MainInstaller.cs`, `Installer.cs`
  - Services: `MessageBrokerService.cs`, `TickService.cs`, `CoroutineService.cs`, `PoolService.cs`, `DataService.cs`, `TimeService.cs`, `RngService.cs`, `VersionServices.cs`
  - Pooling: `ObjectPool.cs`
- **Editor**: `Editor/`
  - Version data generation: `VersionEditorUtils.cs`, `GitEditorProcess.cs`
  - Must remain editor-only (relies on `UnityEditor` + starting git processes)
- **Tests**: `Tests/`
  - EditMode/PlayMode tests validating service behavior

## 4. Important Behaviors / Gotchas
- **`MainInstaller` API vs README snippets**
  - `MainInstaller` is a static class exposing `Bind/Resolve/TryResolve/Clean`.
  - If you see docs/examples referring to `MainInstaller.Instance` or fluent bindings, verify against runtime code—those snippets may be stale.
- **Message broker mutation safety**
  - `Publish<T>` iterates subscribers directly; subscribing/unsubscribing during publish is blocked and throws.
  - Use `PublishSafe<T>` if you have chain subscriptions/unsubscriptions during message handling (it copies delegates first, at extra allocation cost).
  - `Subscribe` uses `action.Target` as the subscriber key, so **static methods cannot subscribe**.
- **Tick/coroutine services allocate a global GameObject**
  - `TickService` and `CoroutineService` each create a `DontDestroyOnLoad` GameObject. Call `Dispose()` when you want to tear them down (tests, game reset, domain reload edge cases).
  - These services do **not** enforce a singleton at runtime; constructing multiple instances will create multiple host GameObjects.
- **`IAsyncCoroutine.StopCoroutine(triggerOnComplete)`**
  - The current implementation triggers completion callbacks even when `triggerOnComplete` is `false` (parameter is not respected). Keep this in mind if you rely on cancellation semantics.
- **DataService persistence details**
  - Keys are `typeof(T).Name` in `PlayerPrefs` (name collisions are possible across assemblies/types with same name).
  - `LoadData<T>` requires `T` to have a parameterless constructor (via `Activator.CreateInstance<T>()`) if no data exists.
- **Pool lifecycle**
  - `PoolService` keeps **one pool per type**; it does not guard against duplicate `AddPool<T>()` calls (duplicate adds throw from `Dictionary.Add`).
  - `GameObjectPool.Dispose(bool)` destroys the `SampleEntity` GameObject; `GameObjectPool.Dispose()` destroys pooled instances but does not necessarily destroy the sample reference—be explicit about disposal expectations when changing pool behavior.
- `GameObjectPool` and `GameObjectPool<T>` override `CallOnSpawned`/`CallOnDespawned` (virtual methods) to use `GetComponent<IPoolEntitySpawn>()` / `GetComponent<IPoolEntityDespawn>()` for lifecycle hooks on **components**. This differs from `ObjectPool<T>` which casts the entity directly.
- **Version data pipeline**
  - Runtime expects a Resources TextAsset named `version-data` (`VersionServices.VersionDataFilename`).
  - `VersionEditorUtils` writes `Assets/Configs/Resources/version-data.txt` on editor load and can be invoked before builds. It uses git CLI; failures should be handled gracefully.
  - Accessors like `VersionServices.VersionInternal` will throw if version data hasn’t been loaded yet—call `VersionServices.LoadVersionDataAsync()` early (and decide how you want to handle load failures).

## 5. Coding Standards (Unity 6 / C# 9.0)
- **C#**: C# 9.0 syntax; explicit namespaces; no global usings.
- **Assemblies**
  - Runtime must not reference `UnityEditor`.
  - Editor tooling must live under `Editor/` (or be guarded with `#if UNITY_EDITOR` if absolutely necessary).
- **Performance**
  - Be mindful of allocations in hot paths (e.g., `PublishSafe` allocates; tick lists mutate; avoid per-frame allocations).

## 6. External Package Sources (for API lookups)
Prefer local UPM cache / local packages when needed:
- GameData: `Packages/com.geuneda.gamedata/` (e.g., `floatP`)
- Unity Newtonsoft JSON (Unity package): check `Library/PackageCache/` if you need source details

## 7. Dev Workflows (common changes)
- **Add a new service**
  - Add runtime interface + implementation under `Runtime/` (keep UnityEngine usage minimal if possible).
  - Add/adjust tests under `Tests/`.
  - If the service needs Unity callbacks, follow the `TickService`/`CoroutineService` pattern (single `DontDestroyOnLoad` host object + `Dispose()`).
- **Bind/resolve services**
  - Bind instances via `MainInstaller.Bind<IMyService>(myServiceInstance)`.
  - Resolve via `MainInstaller.Resolve<IMyService>()` or `TryResolve`.
  - Clear bindings on reset via `MainInstaller.Clean()` (or `Clean<T>()` / `CleanDispose<T>()`).
- **Update versioning**
  - Ensure `version-data.txt` exists/updates correctly in `Assets/Configs/Resources/`.
  - If changing `VersionServices.VersionData`, update both runtime parsing and `VersionEditorUtils` writing logic.

## 8. Update Policy
Update this file when:
- The binding/service-locator API changes (`Installer`, `MainInstaller`)
- Core service behavior changes (publish safety rules, tick timing, coroutine completion/cancellation semantics, pooling lifecycle)
- Versioning pipeline changes (resource filename, editor generator behavior, runtime parsing)
- Dependencies change (`package.json`, new external types like `floatP`)

