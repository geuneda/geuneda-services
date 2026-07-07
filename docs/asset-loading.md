# Asset Loading & Import

[← Back to index](README.md)

Addressables-based asset loading, typed by id and asset type, with optional editor-time import pipeline.

**Namespaces:**
- `AssetResolverService`, `IAssetResolverService`, `IAssetAdderService` → `Geuneda.Services`
- `IAssetLoader`, `ISceneLoader`, `AddressablesAssetLoader`, `AssetConfigsScriptableObject`, `AssetLoaderUtils`, `AssetReferenceScene`, `AddressableConfig` → `Geuneda.Services.AssetsImporter`
- Editor types (`AssetsImporter`, `AssetsToolImporter`, `AssetsConfigsImporter<>`, `AddressableIdsGenerator`) → `Geuneda.Services.AssetsImporter.Editor`

**Key Points:**
- Assets must be registered via `AddConfigs`, `AddAssets`, or `AddAsset` before calling `RequestAsset` or `LoadSceneAsync<TId>` — throws `MissingMemberException` otherwise
- `AddressablesAssetLoader` implements both `IAssetLoader` and `ISceneLoader`
- `AssetConfigsScriptableObject<TId,TAsset>` inherits `AssetConfigsScriptableObjectBase<TId, AssetReference>` (not `<TId, TAsset>`) — the weak-link Addressables pattern; `TAsset` is captured only as `AssetType`
- `UnloadAsset<T>` is synchronous and returns `void` — it only decrements the Addressables reference count for the given asset. Memory reclamation is the caller's responsibility: invoke `Resources.UnloadUnusedAssets()` at appropriate moments (scene transitions, boot, memory-pressure events). Unity also runs an unused-assets sweep automatically on `LoadSceneMode.Single` scene loads. For prefab instances returned by `InstantiateAsync`, `UnloadAsset` does not destroy the `GameObject`; call `Object.Destroy` separately if required.

```csharp
using Geuneda.Services;
using Geuneda.Services.AssetsImporter;

// Low-level: load any asset by Addressables key directly
var loader  = new AddressablesAssetLoader();
var texture = await loader.LoadAssetAsync<Texture2D>("Textures/hero_avatar");
loader.UnloadAsset(texture);

var instance = await loader.InstantiateAsync("Prefabs/Player");
await loader.ReleaseInstanceAsync(instance);

var scene = await loader.LoadSceneAsync("Scenes/MainMenu", LoadSceneMode.Single, activateOnLoad: true);
await loader.UnloadSceneAsync(scene);

// High-level: register configs and request by typed enum id
public class AssetBootstrap
{
    public async void Init(
        SpritesScriptableObject spriteConfigs,   // AssetConfigsScriptableObject<SpriteId, Sprite>
        ScenesScriptableObject  sceneConfigs)    // AssetConfigsScriptableObject<SceneId, Scene>
    {
        var resolver = new AssetResolverService();

        // Register weak-link configs from ScriptableObjects
        resolver.AddConfigs(spriteConfigs);
        resolver.AddConfigs(sceneConfigs);

        // Load a sprite by typed id (loadAsynchronously: true, instantiate: false)
        var sprite = await resolver.RequestAsset<SpriteId, Sprite>(
            SpriteId.HeroAvatar, loadAsynchronously: true, instantiate: false);

        // Load a scene
        await resolver.LoadSceneAsync<SceneId>(
            SceneId.MainMenu, LoadSceneMode.Single, activateOnLoad: true);

        // Bulk-load all registered sprites at once
        var allSprites = await resolver.LoadAllAssets<SpriteId, Sprite>();
    }
}

// Define a ScriptableObject config container (one per asset type + id enum)
[CreateAssetMenu(fileName = "SpritesConfig", menuName = "Configs/SpritesConfig")]
public class SpritesScriptableObject : AssetConfigsScriptableObject<SpriteId, Sprite> { }
```

## Asset Import Pipeline (Editor)

Editor tools live under `Tools → Assets Importer` and `Tools → AddressableIds Generator`.

```csharp
// Implement a custom importer for a new asset type
// TId must satisfy where TId : Enum
public class EnemySpritesImporter : AssetsConfigsImporter<EnemyId, Sprite, EnemySpriteConfig>
{
    // Asset names matched against enum value names by default.
    // Override IdPattern to customise name matching.
}
```

The `TId` type parameter on `AssetsConfigsImporter<TId,TAsset,TScriptableObject>` must be an enum — non-enum types will not compile.

## Error Reference

| Call | Exception | Condition |
|------|-----------|-----------|
| `RequestAsset<TId,TAsset>()` | `MissingMemberException` | Asset not registered via `AddAssets`/`AddConfigs` |
| `LoadSceneAsync<TId>()` | `MissingMemberException` | Scene not registered via `AddAssets`/`AddConfigs` |
