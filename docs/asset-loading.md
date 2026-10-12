# 에셋 로딩과 임포트 (Addressables)

[목차로 돌아가기](README.md)

Addressables 기반 에셋/씬 로딩과, 에디터에서 id 열거형과 설정 에셋을 만들어 주는 도구입니다. v2.0.0 에서 예전 `com.geuneda.assetsimporter` 패키지를 흡수했습니다. 이 패키지를 쓰면 `com.geuneda.assetsimporter` 를 따로 설치하지 마세요(타입 중복).

## 네임스페이스

| 타입 | 네임스페이스 |
|------|--------------|
| `AssetResolverService`, `IAssetResolverService`, `IAssetAdderService` | `Geuneda.Services` |
| `IAssetLoader`, `ISceneLoader`, `AddressablesAssetLoader`, `AssetConfigsScriptableObject` 계열, `AddressableConfig`, `AddressableConfigComparer`, `AssetLoaderUtils`, `AssetReferenceScene` | `Geuneda.Services.AssetsImporter` |
| `IAssetConfigsImporter`, `IAssetConfigsGeneratorImporter`, `AssetsConfigsImporter<,,>`, `AssetsConfigsImporterBase<,,>`, `AssetsConfigsGeneratorImporter<>` (에디터 전용) | `Geuneda.Services.AssetsImporter.Editor` |

`Pair<TKey, TValue>` 는 `com.geuneda.gamedata` 의 `Geuneda.DataExtensions` 네임스페이스에 있습니다. 비동기 메서드는 UniTask(`Cysharp.Threading.Tasks`)를 반환합니다.

## 저수준: AddressablesAssetLoader

`IAssetLoader` 와 `ISceneLoader` 를 구현한 얇은 래퍼입니다. 키(주소, 라벨, `AssetReference` 등 Addressables 키)로 직접 로드합니다. 로드에 실패하면 Addressables 의 `OperationException` 을 그대로 던집니다.

```csharp
using Cysharp.Threading.Tasks;
using Geuneda.Services.AssetsImporter;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LowLevelLoading
{
    private readonly AddressablesAssetLoader _loader = new AddressablesAssetLoader();

    public async UniTask RunAsync(Transform parent)
    {
        var texture = await _loader.LoadAssetAsync<Texture2D>("Textures/hero_avatar");
        _loader.UnloadAsset(texture); // Addressables.Release(texture)

        GameObject player = await _loader.InstantiateAsync("Prefabs/Player", parent, instantiateInWorldSpace: false);
        GameObject enemy  = await _loader.InstantiateAsync("Prefabs/Enemy", Vector3.zero, Quaternion.identity, parent);

        Scene scene = await _loader.LoadSceneAsync("Scenes/Battle", LoadSceneMode.Additive);
        await _loader.UnloadSceneAsync(scene);
    }
}
```

- `UnloadAsset<T>` 는 동기이며 `void` 입니다. `Addressables.Release(asset)` 로 참조 카운트만 줄이고 콜백을 호출합니다. 메모리 회수는 호출자가 씬 전환, 부팅, 메모리 경고 같은 시점에 `Resources.UnloadUnusedAssets()` 로 직접 하세요(`LoadSceneMode.Single` 씬 로드 때는 Unity 가 자동으로 정리합니다).
- 이 패키지에는 `InstantiateAsync` 로 만든 인스턴스 전용 해제 메서드가 없습니다. 필요하면 Addressables API 를 직접 사용하세요.
- `UnloadSceneAsync(Scene)` 은 `SceneManager.UnloadSceneAsync` 를 호출합니다(Addressables 씬 핸들을 해제하는 `Addressables.UnloadSceneAsync` 가 아님).

## 고수준: AssetResolverService

id 타입(보통 enum)과 에셋 타입으로 `AssetReference` 를 등록해 두고, id 로 요청하는 서비스입니다. `AddressablesAssetLoader` 를 상속하므로 저수준 메서드도 그대로 쓸 수 있습니다.

### 설정 에셋 정의

`AssetConfigsScriptableObject<TId, TAsset>` 를 상속한 ScriptableObject 에 id 와 `AssetReference` 쌍을 저장합니다. 이 클래스는 `AssetConfigsScriptableObjectBase<TId, AssetReference>` 를 상속하며(`<TId, TAsset>` 이 아님), `TAsset` 은 `AssetType` 프로퍼티로만 쓰입니다. 에셋을 직접 참조하지 않는 weak-link 구조입니다.

```csharp
using Geuneda.Services.AssetsImporter;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SpriteId { Hero, Coin }
public enum SceneId { MainMenu, Battle }

[CreateAssetMenu(fileName = "SpriteConfigs", menuName = "Configs/SpriteConfigs")]
public class SpriteConfigs : AssetConfigsScriptableObject<SpriteId, Sprite> { }

[CreateAssetMenu(fileName = "SceneConfigs", menuName = "Configs/SceneConfigs")]
public class SceneConfigs : AssetConfigsScriptableObject<SceneId, Scene> { }
```

씬은 에셋 타입을 `UnityEngine.SceneManagement.Scene` 으로 등록해야 `LoadSceneAsync<TId>` 가 찾습니다.

### 등록과 요청

```csharp
using Cysharp.Threading.Tasks;
using Geuneda.Services;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AssetBootstrap
{
    public async UniTask InitAsync(SpriteConfigs spriteConfigs, SceneConfigs sceneConfigs)
    {
        // AddConfigs 는 IAssetAdderService 의 기본 인터페이스 메서드라 인터페이스 타입으로 호출해야 한다
        IAssetAdderService resolver = new AssetResolverService();

        resolver.AddConfigs(spriteConfigs);
        resolver.AddConfigs(sceneConfigs);

        // instantiate 기본값은 true. Sprite 에는 영향이 없지만 GameObject 는 인스턴스가 만들어진다
        Sprite hero = await resolver.RequestAsset<SpriteId, Sprite>(SpriteId.Hero, loadAsynchronously: true, instantiate: false);

        // 등록된 SpriteId/Sprite 전체 로드
        var allSprites = await resolver.LoadAllAssets<SpriteId, Sprite>();

        // 씬 로드 (setActive 기본값 true: 로드한 씬을 활성 씬으로 설정)
        await resolver.LoadSceneAsync<SceneId>(SceneId.Battle, LoadSceneMode.Additive, activateOnLoad: true, setActive: false);
        await resolver.UnloadSceneAsync<SceneId>(SceneId.Battle);

        // 해제: clearReferences 가 true 면 등록 정보도 지운다
        resolver.UnloadAssets<SpriteId, Sprite>(clearReferences: false);
        resolver.UnloadAssets<SpriteId, Sprite>(false, SpriteId.Hero, SpriteId.Coin);
    }
}
```

`MainInstaller` 에 등록할 때는 `MainInstaller.Bind<IAssetResolverService>(resolver)` 처럼 인터페이스로 바인딩합니다. Services Explorer 의 **Asset Resolver** 탭은 `IAssetResolverService` 로 바인딩된 인스턴스를 표시합니다.

### 주요 API

| 메서드 | 설명 |
|--------|------|
| `AddConfigs<TId, TAsset>(AssetConfigsScriptableObject<TId, TAsset>)` | 설정 에셋의 쌍을 등록 (`IAssetAdderService` 로만 호출 가능) |
| `AddAssets<TId>(Type assetType, List<Pair<TId, AssetReference>>)` / `AddAsset<TId>(Type, TId, AssetReference)` | 직접 등록. 같은 id 를 다시 등록하면 `ArgumentException` |
| `AddDebugConfigs(Sprite, GameObject, Material, AudioClip)` | 로드가 끝나지 않은 상태에서 대신 돌려줄 에러용 에셋 지정 |
| `RequestAsset<TId, TAsset>(id, loadAsynchronously = true, instantiate = true, onLoadCallback = null)` | id 로 에셋 요청. `loadAsynchronously: false` 면 `WaitForCompletion` 으로 동기 대기 |
| `RequestAsset<TId, TAsset, TData>(id, data, ...)` | 콜백에 `data` 를 함께 전달 |
| `LoadAllAssets<TId, TAsset>(loadAsynchronously = true, onLoadCallback = null)` | 등록된 전체 로드. `List<Pair<TId, TAsset>>` 반환 |
| `LoadSceneAsync<TId>(id, loadMode = Single, activateOnLoad = true, setActive = true, onLoadCallback = null)` | `SceneInstance` 반환 |
| `UnloadSceneAsync<TId>(id, onUnloadCallback = null)` | 등록되지 않은 id 면 경고만 남김 |
| `UnloadAssets<TId, TAsset>(clearReferences)` / `(clearReferences, assetConfigs)` / `(clearReferences, params TId[] ids)` | `AssetReference.ReleaseAsset()` 로 해제 |

`RequestAsset` 에서 `instantiate: true` 일 때 `GameObject` 는 `Object.Instantiate`, `Material` 은 `new Material(...)` 로 복사본을 돌려줍니다. 그 외 타입은 원본 에셋을 돌려줍니다.

## 에디터 도구

### Addressable Ids 생성기

Addressables 항목으로부터 id 열거형과 조회 코드를 생성합니다.

- 실행: `Tools > Geuneda > Addressable Ids > Generate Addressable Ids`, Services Explorer 의 **Addressable Ids** 탭, 또는 `AssetConfigsScriptableObject` 인스펙터의 **Regenerate Addressable Ids** 버튼
- 설정(**Addressable Ids** 탭에서 편집, `ProjectSettings/AddressableIdsEditorSettings.asset` 에 저장)

| 설정 | 기본값 | 의미 |
|------|--------|------|
| `ScriptFilename` | `AddressableId` | 생성 파일 이름이자 주소 enum 이름 |
| `Namespace` | `Game.Ids` | 생성 코드의 네임스페이스 |
| `AddressableLabel` | `GenerateIds` | 이 라벨이 붙은 항목만 생성. 비우면 읽기 전용이 아닌 모든 그룹의 항목 |

- 출력 위치: `Assets/` 아래에 같은 이름의 스크립트가 있으면 그 파일을 덮어쓰고, 없으면 `Assets/{ScriptFilename}.cs` 에 만듭니다.
- 생성 내용: 주소 enum(`{ScriptFilename}`), 라벨 enum `AddressableLabel`(멤버 `Label_...`), 정적 클래스 `AddressablePathLookup`, 정적 클래스 `AddressableConfigLookup`(`Configs`, `Labels`, 확장 메서드 `GetConfig(this {ScriptFilename})`, `GetConfigs(this AddressableLabel)`, `GetConfigs(string label)`, `ToLabelString(this AddressableLabel)`)
- 주소 enum 멤버 이름은 주소에서 확장자를 떼고 `/`, `\`, 공백, `-` 를 `_` 로 바꾼 것입니다. 예: `Prefabs/Player.prefab` 은 `Prefabs_Player`.

```csharp
using Geuneda.Services.AssetsImporter;
using Game.Ids; // 생성된 코드의 네임스페이스
using UnityEngine;

AddressableConfig config = AddressableId.Prefabs_Player.GetConfig();
var prefab = await new AddressablesAssetLoader().LoadAssetAsync<GameObject>(config.Address);
```

### Assets Importer

폴더 안의 에셋을 enum id 에 맞춰 `AssetConfigsScriptableObject` 에 채워 넣는 에디터 임포터입니다. Editor 어셈블리에 `AssetsConfigsImporter<TId, TAsset, TScriptableObject>` 를 상속한 비추상 클래스를 만들면 자동으로 발견됩니다.

```csharp
using Geuneda.Services.AssetsImporter.Editor;
using UnityEngine;

// TId 는 enum 이어야 한다 (where TId : Enum)
public class SpriteConfigsImporter : AssetsConfigsImporter<SpriteId, Sprite, SpriteConfigs>
{
    // 기본 규칙: 경로에 "/{enum 이름}." 이 들어간 에셋을 매칭 (예: .../Hero.png)
    // 매칭 규칙을 바꾸려면 IdPattern(TId) 를 override 한다
}
```

- 실행: `Tools > Geuneda > Assets Importer > Import Assets Data`(발견된 모든 임포터), 또는 Services Explorer 의 **Assets Importer** 탭(임포터별 경로 지정과 임포트)
- 대상 폴더는 설정 에셋의 `AssetsFolderPath` 입니다. 설정 에셋이 없으면 `Assets/{TScriptableObject 이름}.asset` 으로 새로 만듭니다.
- `AssetsConfigsGeneratorImporter<TAsset>` 는 폴더의 파일 이름으로 enum(`Game.Ids`), 설정 ScriptableObject(`Game.Configs`), 임포터 스크립트를 `Assets/` 에 생성하는 일회성 생성기입니다.

### 인스펙터

- `AssetConfigsScriptableObject` 파생 에셋: 중복 키와 빈 GUID 를 보여 주는 진단 패널, 기본 필드, **Regenerate Addressable Ids** 버튼
- `AssetReferenceScene` 필드: 씬 에셋만 받는 `AssetReference` 로, 프로퍼티 드로어가 씬 경로와 Addressables Groups 창 열기 버튼을 표시합니다.

## 예외

| 호출 | 예외 | 조건 |
|------|------|------|
| `RequestAsset<TId, TAsset>()` | `MissingMemberException` | `TId`/`TAsset` 조합에 해당 id 가 등록되지 않음 |
| `LoadSceneAsync<TId>()` | `MissingMemberException` | `TId`/`Scene` 조합에 해당 id 가 등록되지 않음 |
| `LoadAllAssets<TId, TAsset>()` | `MissingMemberException` | `TId`/`TAsset` 조합이 등록되지 않음 |
| `AddressablesAssetLoader` 의 로드/인스턴스화 | Addressables `OperationException` | Addressables 작업 실패 |
