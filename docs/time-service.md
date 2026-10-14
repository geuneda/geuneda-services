# 시간 서비스

[목차로 돌아가기](README.md)

게임 기준 시계를 제공하고, 오프셋 추가나 서버 시각 기준점을 지원합니다. 네임스페이스는 `Geuneda.Services` 입니다.

## 핵심 규칙

- `ITimeService` 는 읽기 전용(현재 시각, `IsServerTime`, `TimeAdjusted`, 변환 메서드), `ITimeManipulator : ITimeService` 는 `AddTime(float)` 과 `SetServerTime(DateTime)` 을 추가합니다. `TimeService` 는 `ITimeManipulator` 를 구현합니다.
- 현재 UTC 는 **기준점(UTC 시각과 그때의 단조 시계 값)에 core 의 단조 시계(`IMonotonicClock`) 경과를 더한 값**입니다. 서비스를 만든 뒤에 기기 시계를 바꿔도 현재 시각은 바뀌지 않고, 기본 시계(`MonotonicClock`)는 Android·iOS·macOS 에서 기기가 잠든 동안에도 흐릅니다.
- 기준점은 처음에는 생성할 때의 기기 시계(`DateTime.UtcNow`)이고 `IsServerTime` 은 false 입니다. `SetServerTime(serverUtcNow)` 으로 지금 이 순간의 서버 시각을 넣으면 그 값이 기준점이 되고 `IsServerTime` 은 true 입니다.
- 서버에서 시각을 가져와 왕복 지연을 보정하고 주기적으로·포그라운드 복귀 때 다시 맞추는 일은 [com.geuneda.servertime](https://github.com/geuneda/geuneda-servertime) 이 합니다.
- `DateTime` 을 받는 메서드는 `Kind` 가 `Utc` 나 `Local`(UTC 로 바꿈)이어야 합니다. `Unspecified` 는 어느 시간대인지 알 수 없어 `ArgumentException` 입니다.
- `AddTime(seconds)` 로 누적한 오프셋은 현재 시각 프로퍼티 네 개(`DateTimeUtcNow`, `UnityTimeNow`, `UnityScaleTimeNow`, `UnixTimeNow`)에만 반영되고 변환 메서드에는 반영되지 않습니다. 오프셋은 `SetServerTime` 뒤에도 유지됩니다.
- `TimeAdjusted` 는 현재 시각이 흐름과 별개로 바뀌었을 때(`SetServerTime`, `AddTime`) 바뀐 양(새 값 - 이전 값)과 함께 발생합니다. 서버 재동기화로 조금 뒤로 갈 수 있어 음수일 수 있습니다. 처리기 예외는 로그만 남기고 다음 처리기로 넘어갑니다.
- `DateTimeUtcNow`, `UnixTimeNow`, `IsServerTime` 은 어느 스레드에서 읽어도 됩니다. 나머지(Unity 시간, 조작)는 메인 스레드에서 씁니다.
- `UnityTimeNow` 는 `Time.realtimeSinceStartup` + 오프셋, `UnityScaleTimeNow` 는 `Time.time` + 오프셋입니다. Unity 시간 변환은 기준점의 `Time.realtimeSinceStartup` 을 기준으로 합니다.
- Unix 시간은 **밀리초** 단위 `long` 입니다.

## 사용 예

```csharp
using System;
using Geuneda.Services;

var timeService = new TimeService();

// 현재 시각
DateTime utcNow  = timeService.DateTimeUtcNow;   // Kind = Utc
bool fromServer  = timeService.IsServerTime;     // 서버 시각을 넣기 전에는 false
float unityTime  = timeService.UnityTimeNow;
long unixMs      = timeService.UnixTimeNow;

// 변환
long unix    = timeService.UnixTimeFromDateTimeUtc(DateTime.UtcNow);
DateTime dt  = timeService.DateTimeUtcFromUnixTime(unix);
float unityT = timeService.UnityTimeFromUnixTime(unix);

// 시각이 갑자기 바뀌면 일일 초기화 같은 타이머를 다시 계산
timeService.TimeAdjusted += delta => RescheduleDailyReset();

// 조작 (ITimeManipulator)
timeService.AddTime(3600f);                                                       // 1시간 앞으로(치트·테스트)
timeService.SetServerTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));   // 서버 응답에 담긴 지금 시각
```

## 인터페이스 선택

| 인터페이스 | 멤버 | 용도 |
|------------|------|------|
| `ITimeService` | 현재 시각, `IsServerTime`, `TimeAdjusted`, 변환 메서드 6개 | 읽기 전용 소비자 |
| `ITimeManipulator` | `AddTime(float)`, `SetServerTime(DateTime)` 추가 | 서버 동기화(servertime), 치트/테스트 도구 |

`MainInstaller` 로 두 인터페이스를 모두 제공하려면 같은 인스턴스를 각각 바인딩하세요([main-installer.md](main-installer.md) 참고).

## 테스트

`new TimeService(IMonotonicClock clock)` 에 `Elapsed` 를 직접 정하는 가짜 시계를 넘기면 시간 흐름을 정확히 제어할 수 있습니다. 서버 동기화와 함께 쓸 때는 둘에 같은 시계를 넘깁니다.
