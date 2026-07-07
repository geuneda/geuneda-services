# Migration Guide — v1.x → v2.0.0

## Overview

v2.0.0 absorbs `com.geuneda.assetsimporter` into `com.geuneda.services`. The package now has two new hard dependencies (`com.unity.addressables`, `com.cysharp.unitask`) and a reorganized `Runtime/` and `Editor/` folder structure.

**Namespace changes in 2.0.0** affect pool interfaces, command interfaces, asset importer types, and editor versioning code. The concrete service classes (`PoolService`, `CommandService<>`, etc.) and foundation services (DI, messaging, ticking, persistence, time, RNG, versioning) stay in `Geuneda.Services`.

Consumers should update their `using` directives per sections 2, 3, and below.

---

## 1. Package Reference

Remove `com.geuneda.assetsimporter` and update `com.geuneda.services` in your `manifest.json` or submodule:

```diff
- "com.geuneda.assetsimporter": "https://github.com/CoderGamester/Unity-AssetsImporter.git",
  "com.geuneda.services": "https://github.com/CoderGamester/Services.git",
```

`com.unity.addressables` and `com.cysharp.unitask` are now resolved automatically as transitive dependencies of `com.geuneda.services`.

---

## 2. Pool Types (Breaking)

Pool interfaces and pool implementation types moved from `Geuneda.Services` to `Geuneda.Services.Pooling`.

```diff
- using Geuneda.Services;
+ using Geuneda.Services;
+ using Geuneda.Services.Pooling;
```

Affected types moved to `Geuneda.Services.Pooling`: `IPoolService`, `IObjectPool`, `IObjectPool<T>`, `IPoolEntitySpawn`, `IPoolEntitySpawn<T>`, `IPoolEntityDespawn`, `IPoolEntityObject<T>`, `ObjectPoolBase<T>`, `ObjectPool<T>`, `GameObjectPool`, `GameObjectPool<T>`.

`PoolService` (concrete) stays in `Geuneda.Services`.

## 3. Command Types (Breaking)

Command interfaces moved from `Geuneda.Services` to `Geuneda.Services.Commands`.

```diff
- using Geuneda.Services;
+ using Geuneda.Services;
+ using Geuneda.Services.Commands;
```

Affected types moved to `Geuneda.Services.Commands`: `IGameCommandBase`, `IGameCommand<>`, `IGameServerCommand<>`, `ICommandService<>`.

`CommandService<>` (concrete) stays in `Geuneda.Services`.

## 4. Asset Importer Runtime Types

All types previously in `Geuneda.AssetsImporter` (except `AssetResolverService`) are now in `Geuneda.Services.AssetsImporter`.

```diff
- using Geuneda.AssetsImporter;
+ using Geuneda.Services.AssetsImporter;
```

Affected types: `IAssetLoader`, `ISceneLoader`, `AddressablesAssetLoader`, `AddressableConfig`, `AddressableConfigComparer`, `AssetConfigsScriptableObject<TId,TAsset>`, `AssetConfigsScriptableObjectBase<TId,TAsset>`, `AssetLoaderUtils`, `AssetReferenceScene`.

---

## 5. AssetResolverService

`AssetResolverService` (and `IAssetResolverService` / `IAssetAdderService`) moved from `Geuneda.AssetsImporter` to `Geuneda.Services` (the root services namespace).

```diff
- using Geuneda.AssetsImporter;
- // AssetResolverService was in Geuneda.AssetsImporter
+ using Geuneda.Services;
+ // AssetResolverService is now in Geuneda.Services
```

If you only used `using Geuneda.AssetsImporter;` for `AssetResolverService` and nothing else, replace it with `using Geuneda.Services;` (which you almost certainly already have).

---

## 6. Editor Versioning Code

`VersionEditorUtils` and `GitEditorProcess` moved from namespace `Geuneda.Services.Editor` to `Geuneda.Services.Versioning.Editor`.

```diff
- using Geuneda.Services.Editor;
+ using Geuneda.Services.Versioning.Editor;
```

---

## 7. Asset Importer Editor Types

Editor types previously in `GeunedaEditor.AssetsImporter` are now in `Geuneda.Services.AssetsImporter.Editor`.

```diff
- using GeunedaEditor.AssetsImporter;
+ using Geuneda.Services.AssetsImporter.Editor;
```

Affected types: `AssetsImporter`, `AssetsToolImporter`, `IAssetConfigsImporter`, `AssetsConfigsImporter<>`, `AssetsConfigsImporterBase<>`, `AssetsConfigsGeneratorImporter<>`, `IAssetConfigsGeneratorImporter`, `AddressablesIdGeneratorSettingsEditor`, `AddressablesIdGeneratorSettings`.

---

## 8. Re-run Code Generators

If you previously used `AddressableIdsGenerator` (Tools → AddressableIds Generator), the generated file contains a `using` statement that must be updated.

**Option A — Re-run the generator**: Open Unity, go to Tools → AddressableIds Generator → Generate AddressableIds. The newly generated file will have the correct `using Geuneda.Services.AssetsImporter;`.

**Option B — Manual fix** in the generated file:

```diff
- using Geuneda.AssetsImporter;
+ using Geuneda.Services.AssetsImporter;
```

Similarly, if you used `AssetsConfigsGeneratorImporter<TAsset>` to code-generate an importer, re-run by setting the folder path on the importer ScriptableObject, or fix the generated file manually with the same `using` replacement.

---

## 9. `IAssetLoader.UnloadAsset` Signature Change

`IAssetLoader.UnloadAssetAsync<T>(T, Action)` was renamed to `IAssetLoader.UnloadAsset<T>(T, Action)` and its return type changed from `UniTask` to `void`. The underlying behaviour (`Addressables.Release(asset)` + invoke callback) is already synchronous, so the previous `UniTask`-returning signature was misleading; the rename makes the API honest.

```diff
- await loader.UnloadAssetAsync(texture);
+ loader.UnloadAsset(texture);
```

If you chained the release inside an `async` method, the fix is a single-line change: drop the `await` and the `Async` suffix. No other call-site adjustments are required.

**Behavioural reminder**: `UnloadAsset` only decrements the Addressables reference count. It does not free memory and does not destroy `GameObject` instances returned by `InstantiateAsync`. To reclaim memory, call `UnityEngine.Resources.UnloadUnusedAssets()` at scene transitions, boot, or memory-pressure events. To destroy instantiated prefabs, call `UnityEngine.Object.Destroy` separately.

---

## 10. What Did NOT Change

The following types remain in namespace `Geuneda.Services` — no action needed if you only use `using Geuneda.Services;`:

- DI: `IInstaller`, `Installer`, `MainInstaller`
- Messaging: `IMessageBrokerService`, `MessageBrokerService`, `IMessage`
- Ticking: `ITickService`, `TickService`
- Coroutines: `ICoroutineService`, `CoroutineService`, `IAsyncCoroutine`, `IAsyncCoroutine<T>`
- Data: `IDataService`, `IDataProvider`, `DataService`
- Time: `ITimeService`, `ITimeManipulator`, `TimeService`
- RNG: `IRngService`, `RngService`, `RngData`, `IRngData`
- Versioning: `VersionServices`, `VersionData`
- Concrete services and their interfaces that remain in `Geuneda.Services`: `PoolService`, `CommandService<>`, `AssetResolverService`, `IAssetResolverService`, `IAssetAdderService`

The following types **moved** to sub-namespaces (see sections 2, 3, 4 above):

- `Geuneda.Services.Pooling`: `IPoolService`, `IObjectPool`, `IObjectPool<T>`, `IPoolEntitySpawn`, `IPoolEntitySpawn<T>`, `IPoolEntityDespawn`, `IPoolEntityObject<T>`, `ObjectPoolBase<T>`, `ObjectPool<T>`, `GameObjectPool`, `GameObjectPool<T>`
- `Geuneda.Services.Commands`: `IGameCommandBase`, `IGameCommand<>`, `IGameServerCommand<>`, `ICommandService<>`
- `Geuneda.Services.AssetsImporter`: `IAssetLoader`, `ISceneLoader`, `AddressablesAssetLoader`, `AddressableConfig`, `AddressableConfigComparer`, `AssetConfigsScriptableObject`, `AssetConfigsScriptableObjectBase<,>`, `AssetConfigsScriptableObject<,>`, `AssetLoaderUtils`, `AssetReferenceScene`
