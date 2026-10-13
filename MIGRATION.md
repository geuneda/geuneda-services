# 마이그레이션 가이드

버전별 전체 변경은 [CHANGELOG.md](CHANGELOG.md) 를 참고하세요. 기존 프로젝트를 이전 메이저에 고정하려면 manifest 의 URL 끝에 태그를 붙입니다(예: `https://github.com/geuneda/geuneda-services.git#v2.1.4`).

## v2.x → v3.0 (호환성 파괴)

1. **core 의존성**: `Packages/manifest.json` 에 `"com.geuneda.core": "https://github.com/geuneda/geuneda-core.git"` 를 추가합니다.
2. **세이브 형식**: `DataService` 가 `{"version":n,"data":{...}}` 봉투로 저장하고, PlayerPrefs 키가 `typeof(T).Name` 에서 `typeof(T).FullName` 으로 바뀌었습니다. v2.x 세이브는 자동으로 옮기지 않으므로, 출시한 게임은 v2.x 에 고정하거나 직접 옮기는 코드를 둡니다. 새 세이브 스키마는 `[DataVersion(n)]` 과 `AddMigration<T>` 로 단계별로 옮깁니다.
3. **빌드 타입**: `VersionServices.VersionData.BuildType` 이 없어졌습니다. 내부 버전 끝의 환경 이름은 core 의 `AppEnv.Name`(`GEUNEDA_ENV_DEV`/`QA`/`PROD` define)입니다. `VersionEditorUtils.SetAndSaveInternalVersion(bool)` 은 `SetAndSaveInternalVersion()` / `SetAndSaveInternalVersion(BuildTarget)` 으로 바꿉니다.
4. 그 밖의 동작 변경(틱 콜백 중 해제, 버전 비교, 풀 Spawn)은 CHANGELOG 의 3.0.0 항목을 봅니다.

---

# v1.x → v2.x

v2.0.0 에서 `com.geuneda.assetsimporter` 가 이 패키지에 통합되었고, 일부 타입의 네임스페이스와 폴더 구조가 바뀌었습니다. v1.x 에서 올리는 경우 아래 순서로 확인하세요. 버전별 전체 변경은 [CHANGELOG.md](CHANGELOG.md) 를 참고하세요.

## 1. 패키지 참조

- `manifest.json` 에서 `com.geuneda.assetsimporter` 를 제거합니다. 같은 타입이 이 패키지에 있으므로 함께 설치하면 충돌합니다.
- 새 의존성 `com.unity.addressables`, `com.cysharp.unitask` 가 추가되었습니다. UniTask 는 Unity 레지스트리에 없으므로 직접 추가해야 합니다([README 설치](README.md#설치) 참고).

## 2. 풀 타입 (호환성 파괴)

풀 인터페이스와 구현이 `Geuneda.Services` 에서 `Geuneda.Services.Pooling` 으로 이동했습니다. 구체 클래스 `PoolService` 는 `Geuneda.Services` 에 남습니다.

```diff
  using Geuneda.Services;
+ using Geuneda.Services.Pooling;
```

이동한 타입: `IPoolService`, `IObjectPool`, `IObjectPool<T>`, `IPoolEntitySpawn`, `IPoolEntitySpawn<T>`, `IPoolEntityDespawn`, `IPoolEntityObject<T>`, `ObjectPoolBase<T>`, `ObjectPool<T>`, `GameObjectPool`, `GameObjectPool<T>`

## 3. 커맨드 타입 (호환성 파괴)

커맨드 계약이 `Geuneda.Services.Commands` 로 이동했습니다. 구체 클래스 `CommandService<>` 는 `Geuneda.Services` 에 남습니다.

```diff
  using Geuneda.Services;
+ using Geuneda.Services.Commands;
```

이동한 타입: `IGameCommandBase`, `IGameCommand<>`, `IGameServerCommand<>`, `ICommandService<>`

## 4. 에셋 임포터 런타임 타입

예전 `Geuneda.AssetsImporter` 의 타입은 `Geuneda.Services.AssetsImporter` 로 이동했습니다.

```diff
- using Geuneda.AssetsImporter;
+ using Geuneda.Services.AssetsImporter;
```

대상: `IAssetLoader`, `ISceneLoader`, `AddressablesAssetLoader`, `AddressableConfig`, `AddressableConfigComparer`, `AssetConfigsScriptableObject`, `AssetConfigsScriptableObjectBase<,>`, `AssetConfigsScriptableObject<,>`, `AssetLoaderUtils`, `AssetReferenceScene`

예외로 `AssetResolverService`, `IAssetResolverService`, `IAssetAdderService` 는 루트 네임스페이스 `Geuneda.Services` 로 이동했습니다.

## 5. 에디터 타입

| 예전 네임스페이스 | 새 네임스페이스 | 대상 |
|-------------------|-----------------|------|
| `Geuneda.Services.Editor` | `Geuneda.Services.Versioning.Editor` | `VersionEditorUtils`, `GitEditorProcess` |
| `GeunedaEditor.AssetsImporter` | `Geuneda.Services.AssetsImporter.Editor` | `IAssetConfigsImporter`, `IAssetConfigsGeneratorImporter`, `AssetsConfigsImporter<,,>`, `AssetsConfigsImporterBase<,,>`, `AssetsConfigsGeneratorImporter<>` |

다음 에디터 타입은 v2.0.0 에서 삭제되었습니다. 기능은 `Tools > Geuneda > ...` 메뉴와 Services Explorer 탭으로 옮겨졌습니다.

- `AssetsImporter`, `AssetsToolImporter` → `Tools > Geuneda > Assets Importer` 메뉴, **Assets Importer** 탭
- `AddressableIdsGenerator`, `AddressablesIdGeneratorSettings` → `Tools > Geuneda > Addressable Ids` 메뉴, **Addressable Ids** 탭

설정은 `Assets/*.asset` 대신 `ProjectSettings/AssetsImporterEditorSettings.asset`, `ProjectSettings/AddressableIdsEditorSettings.asset` 에 저장됩니다. 예전 `Assets/AssetsImporter.asset`, `Assets/AddressablesIdGeneratorSettings.asset` 은 더 이상 쓰이지 않으므로 지워도 됩니다. 생성기 설정(파일 이름, 네임스페이스, 라벨)은 **Addressable Ids** 탭에서 다시 입력하세요.

## 6. 생성된 코드 다시 만들기

예전 Addressable Ids 생성기로 만든 파일에는 `using Geuneda.AssetsImporter;` 가 들어 있습니다. 다음 중 하나로 고칩니다.

- `Tools > Geuneda > Addressable Ids > Generate Addressable Ids` 로 다시 생성
- 생성된 파일의 `using` 을 직접 수정

```diff
- using Geuneda.AssetsImporter;
+ using Geuneda.Services.AssetsImporter;
```

`AssetsConfigsGeneratorImporter<TAsset>` 로 생성한 코드도 같은 방식으로 `using` 을 고칩니다.

## 7. `IAssetLoader.UnloadAsset` 시그니처 변경

`UnloadAssetAsync<T>(T, Action)` 가 `UnloadAsset<T>(T, Action)` 으로 바뀌고 반환 타입이 `UniTask` 에서 `void` 가 되었습니다. 동작(`Addressables.Release` 후 콜백 호출)은 원래 동기였습니다.

```diff
- await loader.UnloadAssetAsync(texture);
+ loader.UnloadAsset(texture);
```

v2.0.0 부터 `UnloadAsset` 은 `Resources.UnloadUnusedAssets()` 나 `GC.Collect()` 를 호출하지 않습니다. 메모리 회수가 필요하면 적절한 시점에 직접 호출하세요.

## 8. 동작 변경

- `IAsyncCoroutine.StopCoroutine(triggerOnComplete)` 가 인자를 따릅니다. `false`(기본값)면 완료 콜백이 호출되지 않습니다. 예전에는 항상 호출되었습니다.
- v2.1.0: `VersionServices` 가 자동으로 로드됩니다. `LoadVersionDataAsync()` 를 부팅 시 직접 호출하던 코드는 지워도 됩니다.
- v2.1.1: `RngService` 의 분포 편향이 수정되어 같은 시드의 난수 시퀀스가 달라졌습니다. 시드 기반 리플레이 데이터는 호환되지 않습니다.

## 바뀌지 않은 것

다음 타입은 그대로 `Geuneda.Services` 에 있습니다.

- DI: `IInstaller`, `Installer`, `MainInstaller`
- 메시징: `IMessageBrokerService`, `MessageBrokerService`, `IMessage`
- 틱: `ITickService`, `TickService`
- 코루틴: `ICoroutineService`, `CoroutineService`, `IAsyncCoroutine`, `IAsyncCoroutine<T>`
- 데이터: `IDataService`, `IDataProvider`, `DataService`
- 시간: `ITimeService`, `ITimeManipulator`, `TimeService`
- RNG: `IRngService`, `RngService`, `RngData`, `IRngData`
- 버전: `VersionServices`, `VersionServices.VersionData`
- 구체 클래스: `PoolService`, `CommandService<>`
