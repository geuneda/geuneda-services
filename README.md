# Geuneda Services

Unity 게임용 기반 서비스 모음입니다. 서비스 로케이터, 메시지 브로커, 틱, 코루틴, 오브젝트 풀, 데이터 저장, 결정론적 RNG, 시간, 커맨드, 빌드 버전 정보와 Addressables 기반 에셋 로딩을 필요한 것만 골라 쓸 수 있습니다.

## 설치

Unity Package Manager 의 `+` > `Add package from git URL` 에서 아래 URL 을 추가하거나, `Packages/manifest.json` 에 직접 적습니다.

```
https://github.com/geuneda/geuneda-services.git
```

`com.geuneda.gamedata` 와 `com.cysharp.unitask` 는 Unity 레지스트리에 없어 자동으로 설치되지 않습니다. 함께 추가하세요.

```json
{
  "dependencies": {
    "com.geuneda.gamedata": "https://github.com/geuneda/geuneda-gamedata.git",
    "com.geuneda.services": "https://github.com/geuneda/geuneda-services.git",
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"
  }
}
```

특정 버전으로 고정하려면 URL 끝에 `#<태그>` 를 붙입니다. 사용할 수 있는 태그는 GitHub 저장소에서 확인하세요.

## 빠른 시작

부트스트랩에서 서비스를 인터페이스로 바인딩하고, 사용하는 쪽에서 해석합니다.

```csharp
using Geuneda.Services;
using Geuneda.Services.Pooling;
using UnityEngine;

public struct PlayerDamagedMessage : IMessage
{
    public int PlayerId;
    public float Damage;
}

public class GameBootstrap : MonoBehaviour
{
    private void Awake()
    {
        MainInstaller.Bind<IMessageBrokerService>(new MessageBrokerService());
        MainInstaller.Bind<ITickService>(new TickService());
        MainInstaller.Bind<IPoolService>(new PoolService());
        MainInstaller.Bind<IDataService>(new DataService());
    }

    private void OnDestroy()
    {
        // GameObject 를 만드는 서비스는 Dispose 후 제거한다
        MainInstaller.CleanDispose<ITickService>();
        MainInstaller.CleanDispose<IPoolService>();
        MainInstaller.Clean();
    }
}

public class HealthView : MonoBehaviour
{
    private IMessageBrokerService _broker;

    private void Start()
    {
        _broker = MainInstaller.Resolve<IMessageBrokerService>();
        _broker.Subscribe<PlayerDamagedMessage>(OnPlayerDamaged);
    }

    private void OnDestroy()
    {
        _broker?.UnsubscribeAll(this);
    }

    private void OnPlayerDamaged(PlayerDamagedMessage message)
    {
        Debug.Log($"Player {message.PlayerId} took {message.Damage} damage");
    }
}
```

플레이 중 `Tools > Geuneda > Services Explorer` 를 열면 바인딩된 서비스 상태를 볼 수 있습니다.

## 핵심 기능

| 기능 | 주요 타입 | 문서 |
|------|-----------|------|
| 서비스 로케이터 | `MainInstaller`(정적), `Installer`/`IInstaller` | [main-installer.md](docs/main-installer.md) |
| 메시지 브로커 | `IMessageBrokerService`, `IMessage` | [message-broker.md](docs/message-broker.md) |
| 틱 (Update/FixedUpdate/LateUpdate 분배) | `ITickService` | [tick-service.md](docs/tick-service.md) |
| 코루틴 (MonoBehaviour 없이 실행) | `ICoroutineService`, `IAsyncCoroutine` | [coroutine-service.md](docs/coroutine-service.md) |
| 오브젝트 풀 | `IPoolService`, `ObjectPool<T>`, `GameObjectPool<T>` | [pool-service.md](docs/pool-service.md) |
| 데이터 저장 (PlayerPrefs + JSON) | `IDataService`, `IDataProvider` | [data-service.md](docs/data-service.md) |
| 결정론적 RNG | `IRngService`, `RngData` | [rng-service.md](docs/rng-service.md) |
| 게임 시계 | `ITimeService`, `ITimeManipulator` | [time-service.md](docs/time-service.md) |
| 커맨드 | `ICommandService<TGameLogic>`, `IGameCommand<TGameLogic>` | [command-service.md](docs/command-service.md) |
| 빌드 버전/git 정보 | `VersionServices` | [version-services.md](docs/version-services.md) |
| Addressables 에셋 로딩/임포트 | `AssetResolverService`, `AddressablesAssetLoader`, `AssetConfigsScriptableObject<TId, TAsset>` | [asset-loading.md](docs/asset-loading.md) |

꼭 알아야 할 규칙:

- 바인딩은 **인터페이스만** 가능합니다. `MainInstaller.Bind<T>` 는 단일 인터페이스만 받으며, 한 인스턴스를 여러 인터페이스로 묶으려면 `Installer.Bind<T, T1, T2>` 를 쓰거나 인터페이스마다 따로 바인딩합니다.
- 대부분 `Geuneda.Services` 에 있지만, 풀 계약(`IPoolService` 등)은 `Geuneda.Services.Pooling`, 커맨드 계약(`ICommandService<>`, `IGameCommand<>`)은 `Geuneda.Services.Commands`, 에셋 로더 계열은 `Geuneda.Services.AssetsImporter` 에 있습니다.
- `TickService` 와 `CoroutineService` 는 생성 시 `DontDestroyOnLoad` GameObject 를 만듭니다. 런타임에 생성하고 끝나면 `Dispose()` 하세요.
- 메시지 브로커는 정적 메서드를 구독할 수 없고, `Publish` 도중 구독/해제하면 예외가 납니다. 이 경우 `PublishSafe` 를 쓰세요.

## 에디터 도구

- **Services Explorer** (`Tools > Geuneda > Services Explorer`): `MainInstaller` 에 바인딩된 서비스를 탭별로 보여 주는 창입니다. 탭: Overview, Versioning, Installer, Message Broker, Tick, Coroutine, Pool, Data, Time, RNG, Asset Resolver, Assets Importer, Addressable Ids. 플레이 모드에서는 250ms 마다 갱신됩니다.
- **메뉴** (`Tools > Geuneda > ...`): `Versioning > Refresh Version Data`, `Assets Importer > Import Assets Data`, `Addressable Ids > Generate Addressable Ids`, 각 항목의 `Open in Explorer`
- **스캐폴더** (`Assets > Create > Geuneda Services > ...`): Message(`struct : IMessage`), Command(`struct : IGameCommand<TGameLogic>`, `TGameLogic` 은 직접 바꿔야 함), Service(`IName` + `Name : IName, IDisposable`), Pool Entity(`IPoolEntitySpawn`, `IPoolEntityDespawn` 구현 클래스). 네임스페이스는 Project Settings 의 Root Namespace(비어 있으면 `Game`)와 생성 폴더 경로로 정해집니다.
- **인스펙터**: `AssetConfigsScriptableObject` 진단 패널과 **Regenerate Addressable Ids** 버튼, `AssetReferenceScene` 프로퍼티 드로어

## 상세 문서

- [docs/](docs/README.md): 서비스별 사용법, 주의사항, 예외
- [MIGRATION.md](MIGRATION.md): v1.x 에서 v2.x 로 올릴 때 바뀐 네임스페이스와 API
- [CHANGELOG.md](CHANGELOG.md): 버전별 변경 이력
- [AGENTS.md](AGENTS.md): 패키지 구조와 기여 가이드

## 요구 사항과 의존성

- Unity 6000.3 이상
- 패키지 의존성 (`package.json` 기준 최소 버전)

| 패키지 | 버전 | 용도 |
|--------|------|------|
| `com.geuneda.gamedata` | 1.0.0 | `floatP`(RNG), `Pair<,>`(에셋 설정). Newtonsoft.Json(`com.unity.nuget.newtonsoft-json`)도 이 패키지를 통해 들어옵니다(`DataService` 가 사용) |
| `com.unity.addressables` | 1.21.20 | 에셋/씬 로딩 |
| `com.cysharp.unitask` | 2.5.10 | 에셋 로딩 비동기 API |

## 라이선스

MIT. [LICENSE.md](LICENSE.md) 를 참고하세요.

이 패키지는 CoderGamester(GameLovers)의 오픈소스 프로젝트에서 파생되었습니다. 원본 저작권: Miguel Tomas (GameLovers).
