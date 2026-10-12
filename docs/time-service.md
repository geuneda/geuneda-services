# 시간 서비스

[목차로 돌아가기](README.md)

게임 기준 시계를 제공하고, 오프셋 추가나 서버 시간 동기화를 지원합니다. 네임스페이스는 `Geuneda.Services` 입니다.

## 핵심 규칙

- `ITimeService` 는 읽기 전용(현재 시각 + 변환 메서드), `ITimeManipulator : ITimeService` 는 `AddTime(float)` 과 `SetInitialTime(DateTime)` 을 추가합니다. `TimeService` 는 `ITimeManipulator` 를 구현합니다.
- 기준점은 생성 시점의 `DateTime.Now` 와 `Time.realtimeSinceStartup` 입니다. `SetInitialTime(serverTime)` 으로 기준 시각을 서버 시각으로 바꿀 수 있습니다.
- `AddTime(seconds)` 로 누적한 오프셋은 현재 시각 프로퍼티 네 개(`DateTimeUtcNow`, `UnityTimeNow`, `UnityScaleTimeNow`, `UnixTimeNow`)에만 반영됩니다. 변환 메서드(`UnixTimeFromDateTimeUtc` 등)에는 반영되지 않습니다.
- `UnityTimeNow` 는 `Time.realtimeSinceStartup` + 오프셋, `UnityScaleTimeNow` 는 `Time.time` + 오프셋입니다.
- Unix 시간은 **밀리초** 단위 `long` 입니다.

## 사용 예

```csharp
using System;
using Geuneda.Services;

var timeService = new TimeService();

// 현재 시각
DateTime utcNow  = timeService.DateTimeUtcNow;
float unityTime  = timeService.UnityTimeNow;
float scaledTime = timeService.UnityScaleTimeNow;
long unixMs      = timeService.UnixTimeNow;

// 변환
long unix    = timeService.UnixTimeFromDateTimeUtc(DateTime.UtcNow);
DateTime dt  = timeService.DateTimeUtcFromUnixTime(unix);
float unityT = timeService.UnityTimeFromUnixTime(unix);

// 조작 (ITimeManipulator)
timeService.AddTime(3600f);                               // 1시간 앞으로
timeService.SetInitialTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)); // 서버 시각 동기화
```

## 인터페이스 선택

| 인터페이스 | 멤버 | 용도 |
|------------|------|------|
| `ITimeService` | 현재 시각 프로퍼티 + 변환 메서드 6개 | 읽기 전용 소비자 |
| `ITimeManipulator` | `AddTime(float)`, `SetInitialTime(DateTime)` 추가 | 서버 동기화, 치트/테스트 도구 |

`MainInstaller` 로 두 인터페이스를 모두 제공하려면 같은 인스턴스를 각각 바인딩하세요([main-installer.md](main-installer.md) 참고).
