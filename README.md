# Geuneda Services

[![Unity Version](https://img.shields.io/badge/Unity-6000.0%2B-blue.svg)](https://unity3d.com/get-unity/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Version](https://img.shields.io/github/v/tag/geuneda/geuneda-services?label=version)](CHANGELOG.md)

Unity 게임 아키텍처를 위한 핵심 서비스 패키지입니다. DI 컨테이너, 메시지 브로커, 풀링 시스템 등 다양한 서비스를 제공합니다.

> **바로가기**: [설치](#설치-방법) | [사용 시점](#사용-시점) | [빠른 시작](#빠른-시작) | [서비스 개요](#서비스-개요) | [문서](docs/README.md) | [변경 이력](CHANGELOG.md) | [마이그레이션 가이드](MIGRATION.md)

## 왜 이 패키지를 사용해야 하나요?

| 문제점 | 해결책 |
|--------|--------|
| 분산된 의존성 관리 | `MainInstaller`로 중앙 집중식 의존성 관리 |
| 강하게 결합된 시스템 | 메시지 브로커로 느슨한 결합 구현 |
| Update 관리 복잡성 | Tick 서비스로 업데이트 사이클 중앙화 |
| MonoBehaviour 없이 코루틴 | 코루틴 서비스로 순수 C# 코루틴 실행 |
| 인스턴스화로 인한 메모리 낭비 | 오브젝트 풀링으로 효율적 재사용 |
| 저장/불러오기 불일치 | 크로스 플랫폼 데이터 영속성 |
| 비결정론적 게임플레이 | 결정론적 RNG 서비스 |

**프로덕션 검증 완료:** 프레임당 할당을 최소화했으며, 실제 게임에서 사용되고 있습니다.

---

## 사용 시점

**이 패키지가 적합한 경우:** 완전한 DI 프레임워크에 얽매이지 않고, 필요한 서비스만 골라 쓸 수 있는 가볍고 독립적인 서비스 모음을 원할 때 사용하세요.

**대안을 고려해야 하는 경우:** 여러 타입에 걸친 스코프 수명, 팩토리 바인딩, 생성자 주입이 필요하다면 VContainer, Zenject 등을 고려하세요. 이 경우 DI 컴포지션 루트 안에서 다중 인터페이스 바인딩을 하려면 `MainInstaller`가 아닌 `Installer`를 직접 사용하세요.

---

## 시스템 요구사항

- **[Unity](https://unity.com/download)** 6000.0 이상 (Unity 6)
- **[Geuneda GameData](https://github.com/geuneda/geuneda-gamedata)** (v1.0.0) — 자동으로 해결됨
- **[Unity Addressables](https://docs.unity3d.com/Packages/com.unity.addressables@latest)** (≥ 1.21.20) — 자동으로 해결됨
- **[UniTask](https://github.com/Cysharp/UniTask)** (≥ 2.5.10) — 자동으로 해결됨

| Unity 버전 | 상태 |
|---|---|
| 6000.0 이상 (Unity 6) | ✅ 완전히 테스트됨 |
| 2022.3 LTS | ⚠️ 미검증 |

## 설치 방법

### Unity Package Manager (권장)

1. Unity Package Manager 열기 (`Window` → `Package Manager`)
2. `+` → `Add package from git URL` 클릭
3. 입력: `https://github.com/geuneda/geuneda-services.git`

### manifest.json 사용

```json
{
  "dependencies": {
    "com.geuneda.gamedata": "https://github.com/geuneda/geuneda-gamedata.git#v1.0.0",
    "com.geuneda.services": "https://github.com/geuneda/geuneda-services.git#v1.0.1"
  }
}
```

## 핵심 컴포넌트

| 컴포넌트 | 역할 |
|-----------|----------------|
| **MainInstaller** | 전역 스코프의 단일 인터페이스 바인딩을 위한 정적 서비스 로케이터 |
| **Installer** | 인스턴스 기반 DI 컨테이너 (다중 인터페이스 바인딩 지원) |
| **IMessageBrokerService** | 타입 안전 Pub/Sub 메시징 |
| **ITickService** | Update/FixedUpdate/LateUpdate 콜백 중앙 관리 |
| **ICoroutineService** | 순수 C# 클래스에서 코루틴 실행 |
| **IPoolService** | 오브젝트 풀 등록 및 관리 |
| **IDataService / IDataProvider** | 크로스 플랫폼 데이터 영속성 (읽기-쓰기 / 읽기 전용) |
| **ITimeService / ITimeManipulator** | 오프셋/동기화 조작이 가능한 통합 시간 접근 |
| **IRngService** | 결정론적 난수 생성 |
| **ICommandService\<TGameLogic\>** | 타입 지정 커맨드 실행 계층 |
| **VersionServices** | 빌드/git 메타데이터 런타임 접근 |
| **AssetResolverService** | id + 에셋 타입 기반 Addressables 타입 지정 에셋 로딩 |
| **IAssetLoader / ISceneLoader** | 저수준 addressable 로드/언로드/인스턴스화 인터페이스 |

---

## 빠른 시작

```csharp
using Geuneda.Services;

public class GameBootstrap : MonoBehaviour
{
    void Awake()
    {
        var messageBroker = new MessageBrokerService();
        var tickService   = new TickService();
        var dataService   = new DataService();

        MainInstaller.Bind<IMessageBrokerService>(messageBroker);
        MainInstaller.Bind<ITickService>(tickService);
        MainInstaller.Bind<IDataService>(dataService);
    }

    void OnDestroy()
    {
        MainInstaller.CleanDispose<ITickService>();
        MainInstaller.Clean();
    }
}

// 어디서든 해석
var broker = MainInstaller.Resolve<IMessageBrokerService>();
broker.Subscribe<PlayerDamagedMessage>(OnPlayerDamaged);

public struct PlayerDamagedMessage : IMessage
{
    public int PlayerId;
    public float Damage;
}
```

---

## 서비스 개요

전체 API 레퍼런스와 레시피는 [`docs/`](docs/README.md)에 있습니다. 아래는 간단한 예시입니다.

### 서비스 로케이터 (MainInstaller / Installer)

```csharp
MainInstaller.Bind<IMessageBrokerService>(new MessageBrokerService());
var broker = MainInstaller.Resolve<IMessageBrokerService>();
MainInstaller.TryResolve<IDataService>(out var ds);
MainInstaller.CleanDispose<ITickService>();
MainInstaller.Clean();

// 다중 인터페이스 바인딩 — Installer를 직접 사용
var installer = new Installer();
installer.Bind<TimeService, ITimeService, ITimeManipulator>(new TimeService());
```

### 메시지 브로커

```csharp
// 정적 메서드 구독은 지원되지 않습니다
broker.Subscribe<EnemyDefeatedMessage>(OnEnemyDefeated);
broker.Publish(new EnemyDefeatedMessage { EnemyId = 42 });
broker.PublishSafe(new EnemyDefeatedMessage { EnemyId = 42 }); // 발행 중에도 안전
broker.Unsubscribe<EnemyDefeatedMessage>(this);
broker.UnsubscribeAll(this);
```

### 틱 서비스

```csharp
var tick = new TickService();
tick.SubscribeOnUpdate(OnUpdate);
tick.SubscribeOnUpdate(OnThrottled, deltaTime: 0.1f); // 호출 빈도 제한
tick.SubscribeOnFixedUpdate(OnFixed);
tick.SubscribeOnLateUpdate(OnLate);
tick.UnsubscribeAll(this);
tick.Dispose(); // 호스트 GameObject 파괴
```

### 코루틴 서비스

```csharp
var cs = new CoroutineService();
IAsyncCoroutine handle = cs.StartAsyncCoroutine(MyRoutine());
handle.OnComplete(() => Debug.Log("Done!"));
cs.StartDelayCall(() => Debug.Log("2 s later"), delay: 2f);
cs.Dispose();
```

### 풀 서비스

```csharp
var pool = new PoolService();
pool.AddPool(new GameObjectPool<Bullet>(50, prefab));
var bullet = pool.Spawn<Bullet>();
pool.Despawn(bullet);
```

### 데이터 서비스

```csharp
var ds = new DataService();
PlayerData player = ds.LoadData<PlayerData>(); // PlayerPrefs에서 로드하거나 새로 생성
player.Level = 10;
ds.SaveData<PlayerData>();
```

### RNG 서비스

```csharp
RngData rngData = RngService.CreateRngData(seed: 42);
var rng = new RngService(rngData);
int roll = rng.Range(1, 7);         // 1–6
int saved = rng.Counter;
rng.Restore(saved);                 // 저장 지점부터 재현
```

### 시간 서비스

```csharp
var time = new TimeService();
DateTime utc  = time.DateTimeUtcNow;
float unity   = time.UnityTimeNow;
long unixMs   = time.UnixTimeNow;
time.AddTime(3600f);                // 1시간 빨리 감기 (ITimeManipulator)
```

### 커맨드 서비스

```csharp
public struct LevelUpCommand : IGameCommand<GameLogic>
{
    public void Execute(GameLogic gl, IMessageBrokerService mb)
    {
        gl.PlayerLevel++;
        mb.Publish(new PlayerLevelledUpMessage { Level = gl.PlayerLevel });
    }
}

ICommandService<GameLogic> cmd = new CommandService<GameLogic>(gameLogic, messageBroker);
cmd.ExecuteCommand(new LevelUpCommand());
```

### 버전 서비스

```csharp
// 별도의 설정 호출이 필요 없습니다 — 버전 메타데이터는 SubsystemRegistration 시점에 자동 로드되며,
// 첫 프로퍼티 접근 시 지연 로드로 폴백됩니다.
string branch = VersionServices.Branch;
string commit = VersionServices.Commit;
string ext    = VersionServices.VersionExternal; // 항상 안전, 로드 불필요

// 선택적 명시적 예열 (멱등 — 이미 로드됐다면 아무 동작 안 함):
// VersionServices.LoadVersionData();              // 동기, 권장 기본값
// await VersionServices.LoadVersionDataAsync();   // 비동기 — 큰 VersionData 블롭에만 유용
```

### 에셋 로딩

```csharp
// 저수준
var loader  = new AddressablesAssetLoader();
var texture = await loader.LoadAssetAsync<Texture2D>("Textures/hero");

// 고수준: id로 타입 지정
var resolver = new AssetResolverService();
resolver.AddConfigs(spriteConfigs); // AssetConfigsScriptableObject<SpriteId, Sprite>
var sprite = await resolver.RequestAsset<SpriteId, Sprite>(SpriteId.Hero, true, false);
await resolver.LoadSceneAsync<SceneId>(SceneId.MainMenu, LoadSceneMode.Single, true);
```

## 에디터 도구

이 패키지는 **Edit** 모드와 **Play** 모드 모두에서 동작하는 에디터 유틸리티 모음을 제공합니다.

### Services Explorer

`Tools > Geuneda > Services Explorer`로 엽니다.

서비스마다 탭이 하나씩 있는 도킹 가능한 UIToolkit 창입니다. Play 모드에서는 각 탭이 250ms 간격으로 실시간 갱신됩니다. Edit 모드에서는 스냅샷 배너가 표시되며 데이터는 요청 시점에 읽습니다.

| 탭 | 표시 내용 | 주요 CTA / 액션 |
|---|---|---|
| **Overview** | 바인딩/준비 상태와 직접 이동 링크가 있는 서비스별 카드 그리드 | Open (해당 탭으로 이동), 서비스별 기본 CTA |
| **Versioning** | `VersionExternal`, `VersionInternal`, `Branch`, `Commit`, `BuildNumber`; `version-data.txt` 미리보기 | **Reveal version-data.txt** |
| **Installer** | 모든 `MainInstaller` 바인딩 (인터페이스 → 구체 타입) | **Clean All**; 바인딩별 Clean, CleanDispose |
| **Message Broker** | 확장 가능한 구독자 목록과 함께 모든 `IMessage` 구독 | **Unsubscribe All**; 타입별 Unsubscribe, Publish default(T) 테스트 |
| **Tick** | 스로틀 설정과 함께 Update / FixedUpdate / LateUpdate 구독자 목록 | **Unsubscribe All**; 목록별 Clear |
| **Coroutine** | 활성 `IAsyncCoroutine` 핸들 (시작 시각, 실행 중, 완료) | **Stop All Coroutines**; 개별 Stop |
| **Pool** | 등록된 모든 풀: 스폰 개수, 샘플 엔티티 | **Clear All Pools**; DespawnAll, Dispose, RemovePool, 샘플 Ping |
| **Data** | 들여쓰기된 JSON 미리보기와 함께 로드된 모든 데이터 타입 | **Save All Data**; Save, Load, PlayerPrefs 키 Delete |
| **Time** | 실시간 `DateTimeUtcNow`, `UnityTimeNow`, `UnityScaleTimeNow`, `UnixTimeNow` | **Reset Time**; `AddTime` 슬라이더, `SetInitialTime` 피커 |
| **RNG** | Seed, Counter, 다음 N개 값 미리보기 | Restore(count) |
| **Asset Resolver** | `AssetMap` 트리: 에셋 타입 → id 타입 → (id → ref, 로드 상태) | **Unload All** (파괴적 토글 뒤에 위치); 에셋별 Unload |
| **Assets Importer** | 임포터별 경로 및 상태와 함께 발견된 `IAssetConfigsImporter` 목록 | **Import All**; Set Path, Import, 임포터별 Select |
| **Addressable Ids** | 출력 상태와 함께 제너레이터 설정 (`ScriptFilename`, `Namespace`, `AddressableLabel`) | **Generate Addressable Ids**; Open Addressables Groups |

### 커스텀 인스펙터

- **`AssetConfigsScriptableObject`** — 진단 패널 (중복 키, 빈 GUID) + 기본 필드 + "Regenerate Addressable Ids" 버튼.
- **`AddressablesIdGeneratorSettings`** — 설정은 이제 Services Explorer의 **Addressable Ids** 탭에서 구성합니다 (`Tools > Geuneda > Addressable Ids > Open in Explorer`).
- **`AssetReferenceScene`** (프로퍼티 드로어) — 해석된 씬 경로 레이블 + "Open in Addressables Groups" 버튼.

### 스캐폴더

`Assets > Create > Geuneda Services > …`

| 항목 | 생성 결과 |
|---|---|
| **Message** | `struct : IMessage` |
| **Command** | `struct : IGameCommand<TGameLogic>` |
| **Service** | `IMyService` + `MyService : IMyService, IDisposable` |
| **Pool Entity** | `IPoolEntitySpawn` + `IPoolEntityDespawn`를 구현하는 클래스 |

파일 이름과 네임스페이스는 Project 창에서 대화식으로 지정하며, Unity 기본 "Create > C# Script" 흐름과 동일합니다.

---

## 샘플

가져올 수 있는 샘플은 [`Samples~/`](Samples~/) 아래에 있으며 Unity Package Manager를 통해 노출됩니다:

| 샘플 | Addressables 필요? | 초점 |
|---|---|---|
| **Services Playground** | 아니오 | 모든 기반 서비스(`MainInstaller`, `MessageBroker`, `Tick`, `Coroutine`, `Pool`, `Data`, `Time`, `Rng`, `Commands`, `Versioning`)를 하나의 씬에 연결. **Services Explorer** 창을 위한 수동 종단 간(end-to-end) 프로토콜 역할도 겸함 |
| **Asset Resolver** | 예 (~2분 설정) | `AssetResolverService` + `AssetConfigsScriptableObject<TId, TAsset>`를 통한 타입 지정 에셋 로딩, Addressable Ids 제너레이터와 Assets Importer 파이프라인 포함 |

각 샘플은 프로그래밍 방식으로 구성된 UI를 갖춘 완전하고 실행 가능한 Unity 씬으로 제공되며, 별도의 임포트별 연결 단계가 없습니다(Asset Resolver 샘플은 스프라이트를 Addressable로 표시해야 함 — 해당 README 참고). 인덱스, AI 어시스턴트 흔한 실수 섹션, 그리고 샘플 전용 타입 전체 목록(패키지 공개 API에 포함되지 않음)은 [`Samples~/README.md`](Samples~/README.md)를 참고하세요.

샘플 가져오기: **Window > Package Manager > Geuneda Services > Samples > Import**.

## 패키지 구조

```
Runtime/
├── Installer.cs              # DI 컨테이너
├── MainInstaller.cs          # 정적 서비스 로케이터
├── MessageBrokerService.cs   # Pub/Sub 메시징
├── TickService.cs            # 업데이트 관리
├── CoroutineService.cs       # 코루틴 호스트
├── PoolService.cs            # 풀 서비스
├── ObjectPool.cs             # 풀 구현체
├── DataService.cs            # 데이터 영속성
├── TimeService.cs            # 시간 서비스
├── RngService.cs             # 결정론적 RNG
├── VersionServices.cs        # 버전 정보
└── CommandService.cs         # 커맨드 패턴
```

기여를 환영합니다! 버그 신고나 기능 요청은 [GitHub Issues](https://github.com/geuneda/geuneda-services/issues)를 확인하세요. 개발 환경 설정, 아키텍처 상세, 네임스페이스 컨벤션, 코딩 표준은 [AGENTS.md](AGENTS.md)를 참고하세요.

---

## 관련 문서

| 문서 | 용도 |
|---|---|
| [docs/README.md](docs/README.md) | 서비스별 전체 API 레퍼런스 |
| [AGENTS.md](AGENTS.md) | 기여자/에이전트 가이드 (아키텍처, 주의사항, 워크플로우) |
| [CHANGELOG.md](CHANGELOG.md) | 버전 이력 |
| [MIGRATION.md](MIGRATION.md) | v1.x → v2.0.0 마이그레이션 가이드 |

## 지원

- **이슈**: [버그 신고 또는 기능 요청](https://github.com/geuneda/geuneda-services/issues)
- **토론**: [질문하고 아이디어 공유하기](https://github.com/geuneda/geuneda-services/discussions)

## 라이센스

MIT — [LICENSE.md](LICENSE.md)를 참고하세요.

이 패키지는 CoderGamester(GameLovers)의 오픈소스 프로젝트에서 파생되었습니다. 원본 저작권: Miguel Tomas (GameLovers).
