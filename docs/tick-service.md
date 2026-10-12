# 틱 서비스

[목차로 돌아가기](README.md)

Unity 의 Update / FixedUpdate / LateUpdate 콜백을 한곳에서 순수 C# 객체로 나눠 주는 서비스입니다. 네임스페이스는 `Geuneda.Services` 입니다.

## 핵심 규칙

- 생성자에서 `DontDestroyOnLoad` GameObject 를 만들고 `TickServiceMonoBehaviour` 를 붙여 콜백을 구동합니다. 따라서 **플레이 모드(런타임)에서 생성**해야 합니다.
- 싱글톤을 강제하지 않습니다. 인스턴스를 여러 개 만들면 호스트 GameObject 도 여러 개 생깁니다.
- `Dispose()` 는 호스트 GameObject 를 파괴하고 모든 구독을 지웁니다.
- 콜백은 `Action<float>` 입니다.
  - `SubscribeOnUpdate` / `SubscribeOnLateUpdate`: 인자는 **마지막 호출 이후 경과 시간**입니다.
  - `SubscribeOnFixedUpdate`: 인자는 경과 시간이 아니라 **`Time.fixedTime`(절대 시각)** 입니다. 고정 델타가 필요하면 `Time.fixedDeltaTime` 을 직접 읽으세요.
- `deltaTime > 0` 이면 최소 그 간격이 지났을 때만 호출합니다(호출 빈도 제한). `timeOverflowToNextTick: true` 는 초과분을 다음 틱으로 넘겨 드리프트를 줄입니다.
- `realTime: true` 는 `Time.realtimeSinceStartup`(timeScale 영향 없음), 기본값 `false` 는 `Time.time` 을 씁니다.
- 구독 리스트는 역순으로 순회하므로 콜백 안에서 구독 해제해도 안전합니다. 같은 프레임의 호출 순서는 구독 역순입니다.

## 사용 예

```csharp
using System;
using Geuneda.Services;

public class GameController : IDisposable
{
    private readonly ITickService _tickService;

    public GameController(ITickService tickService)
    {
        _tickService = tickService;

        _tickService.SubscribeOnUpdate(OnUpdate);                                       // 매 프레임
        _tickService.SubscribeOnUpdate(OnThrottled, deltaTime: 0.1f);                   // 최소 0.1초 간격
        _tickService.SubscribeOnUpdate(OnSteady, 0.1f, timeOverflowToNextTick: true);   // 초과분 이월
        _tickService.SubscribeOnUpdate(OnRealTime, realTime: true);                     // timeScale 무시
        _tickService.SubscribeOnFixedUpdate(OnFixedUpdate);
        _tickService.SubscribeOnLateUpdate(OnLateUpdate);
    }

    private void OnUpdate(float deltaTime) { }
    private void OnThrottled(float deltaTime) { }
    private void OnSteady(float deltaTime) { }
    private void OnRealTime(float deltaTime) { }
    private void OnFixedUpdate(float fixedTime) { } // Time.fixedTime 이 전달된다
    private void OnLateUpdate(float deltaTime) { }

    public void Dispose()
    {
        // 이 객체가 등록한 모든 콜백 해제 (Update / FixedUpdate / LateUpdate 전체)
        _tickService.UnsubscribeAll(this);
    }
}
```

## 구독 해제 API

| 메서드 | 동작 |
|--------|------|
| `Unsubscribe(action)` | 세 리스트 모두에서 해당 콜백을 하나씩 제거 |
| `UnsubscribeOnUpdate(action)` / `UnsubscribeOnFixedUpdate(action)` / `UnsubscribeOnLateUpdate(action)` | 해당 리스트에서 첫 번째 일치 항목 제거 |
| `UnsubscribeAllOnUpdate()` / `UnsubscribeAllOnFixedUpdate()` / `UnsubscribeAllOnLateUpdate()` | 해당 리스트 전체 비우기 |
| `UnsubscribeAllOnUpdate(subscriber)` 등 | 해당 리스트에서 `subscriber` 가 등록한 항목 전부 제거 |
| `UnsubscribeAll()` / `UnsubscribeAll(subscriber)` | 세 리스트 전체 / `subscriber` 항목 전부 제거 |

`subscriber` 는 구독 시점의 `action.Target` 입니다. 메시지 브로커와 마찬가지로 지역 변수를 캡처한 람다는 `this` 로 해제되지 않습니다.
