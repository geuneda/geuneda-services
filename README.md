# Geuneda Services

[![Unity Version](https://img.shields.io/badge/Unity-6000.0%2B-blue.svg)](https://unity3d.com/get-unity/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Unity 게임 아키텍처를 위한 핵심 서비스 패키지입니다. DI 컨테이너, 메시지 브로커, 풀링 시스템 등 다양한 서비스를 제공합니다.

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

## 설치 방법

### Unity Package Manager (Git URL)

1. **Window → Package Manager** 열기
2. **+** 버튼 → **Add package from git URL...**
3. URL 입력:
```
https://github.com/geuneda/geuneda-services.git
```

또는 `Packages/manifest.json`에 직접 추가:
```json
{
  "dependencies": {
    "com.geuneda.gamedata": "https://github.com/geuneda/geuneda-gamedata.git#v1.0.0",
    "com.geuneda.services": "https://github.com/geuneda/geuneda-services.git#v1.0.1"
  }
}
```

## 요구 사항

- Unity 6000.0 이상
- [Geuneda GameData](https://github.com/geuneda/geuneda-gamedata) (v1.0.0)

## 주요 서비스

### Main Installer (서비스 로케이터)

인터페이스 기반의 간단한 DI 컨테이너입니다.

```csharp
using Geuneda.Services;

// 서비스 등록 (인터페이스만 가능)
MainInstaller.Bind<IMessageBrokerService>(new MessageBrokerService());
MainInstaller.Bind<ITickService>(new TickService());

// 서비스 해석
var broker = MainInstaller.Resolve<IMessageBrokerService>();

// 안전한 해석
if (MainInstaller.TryResolve<IDataService>(out var dataService))
{
    dataService.SaveData();
}

// 정리
MainInstaller.CleanDispose<ITickService>();  // Dispose 후 제거
MainInstaller.Clean();                        // 모든 바인딩 제거
```

### Message Broker (메시지 브로커)

타입 안전한 Pub/Sub 통신 시스템입니다.

```csharp
// 메시지 정의
public struct EnemyDefeatedMessage : IMessage
{
    public int EnemyId;
    public Vector3 Position;
}

var broker = new MessageBrokerService();

// 구독
broker.Subscribe<EnemyDefeatedMessage>(OnEnemyDefeated);

// 발행
broker.Publish(new EnemyDefeatedMessage { EnemyId = 42 });

// 구독 해제
broker.Unsubscribe<EnemyDefeatedMessage>(this);
```

### Tick Service (틱 서비스)

Unity 업데이트 사이클을 중앙 집중식으로 관리합니다.

```csharp
public class GameController : ITickable
{
    public GameController()
    {
        var tickService = new TickService();
        tickService.Add(this);              // Update
        tickService.AddFixed(this);         // FixedUpdate
        tickService.Add(this, 0.1f);        // 0.1초마다 버퍼드 호출
    }

    public void OnTick(float deltaTime, double time)
    {
        // 매 프레임 또는 지정된 간격으로 호출
    }
}
```

### Coroutine Service (코루틴 서비스)

MonoBehaviour 없이 코루틴을 실행합니다.

```csharp
var coroutineService = new CoroutineService();

// 완료 콜백과 함께 코루틴 시작
coroutineService.StartCoroutine(MyRoutine(), () => Debug.Log("완료!"));

// 지연 실행
coroutineService.StartDelayCall(2f, () => Debug.Log("2초 후"));

IEnumerator MyRoutine()
{
    yield return new WaitForSeconds(1f);
    Debug.Log("코루틴 단계");
}
```

### Pool Service (풀 서비스)

효율적인 오브젝트 풀링 시스템입니다.

```csharp
var poolService = new PoolService();

// 풀 생성
var bulletPool = new GameObjectPool<Bullet>(bulletPrefab, initialSize: 50);
poolService.AddPool(bulletPool);

// 스폰/디스폰
var bullet = poolService.Spawn<Bullet>();
poolService.Despawn(bullet);

// 데이터와 함께 스폰
var bullet = poolService.Spawn<Bullet, BulletData>(new BulletData { Damage = 100 });
```

### Data Service (데이터 서비스)

크로스 플랫폼 데이터 영속성을 제공합니다.

```csharp
[Serializable]
public class PlayerData
{
    public string Name;
    public int Level;
}

var dataService = new DataService();

// 저장
var player = new PlayerData { Name = "영웅", Level = 10 };
dataService.AddOrReplaceData("player", player);
await dataService.SaveData();

// 로드
await dataService.LoadData();
var loaded = dataService.GetData<PlayerData>("player");
```

### RNG Service (난수 서비스)

결정론적 난수 생성기입니다. 멀티플레이어 동기화나 리플레이에 유용합니다.

```csharp
var rngData = RngService.CreateRngData(seed: 12345);
var rng = new RngService(rngData);

int randomInt = rng.Next;                // 0 ~ int.MaxValue
int ranged = rng.Range(1, 100);          // 1~99
floatP rangedFloat = rng.Range(0f, 1f);  // 0~1

// 상태 저장/복원
int savedCount = rng.Counter;
rng.Restore(savedCount);
```

### Time Service (시간 서비스)

통합 시간 접근 인터페이스입니다.

```csharp
var timeService = new TimeService();

float unityTime = timeService.UnityTime;    // Time.time
long unixTime = timeService.UnixTime;       // Unix 타임스탬프
DateTime dateTime = timeService.DateTime;   // DateTime.UtcNow

// 변환
long unix = timeService.DateTimeToUnix(DateTime.UtcNow);
DateTime dt = timeService.UnixToDateTime(unix);
```

### Version Services (버전 서비스)

빌드 버전 및 Git 메타데이터에 접근합니다.

```csharp
await VersionServices.LoadVersionDataAsync();

string version = VersionServices.VersionExternal;   // "1.0.0"
string branch = VersionServices.Branch;             // "main"
string commit = VersionServices.Commit;             // "abc123"
```

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

## 라이센스

MIT License

원본 저작권: Miguel Tomas (GameLovers)
