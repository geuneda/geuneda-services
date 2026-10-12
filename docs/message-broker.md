# 메시지 브로커 서비스

[목차로 돌아가기](README.md)

게임 시스템 사이를 느슨하게 연결하는 타입 안전 Pub/Sub 입니다. 네임스페이스는 `Geuneda.Services` 입니다.

## 핵심 규칙

- 메시지 타입은 `IMessage` 를 구현해야 합니다. 보통 `struct` 로 만듭니다.
- 구독자는 `action.Target` 을 키로 저장됩니다. 그래서
  - **정적 메서드는 구독할 수 없습니다** (`Target` 이 null 이라 `ArgumentException`).
  - 같은 객체가 같은 메시지 타입을 다시 구독하면 **이전 핸들러를 덮어씁니다** (객체당 메시지 타입별 핸들러 1개).
  - 지역 변수를 캡처한 람다는 `Target` 이 컴파일러가 만든 클로저 객체가 되므로 `Unsubscribe<T>(this)` 로 해제되지 않습니다. 해제가 필요하면 인스턴스 메서드나 `this` 만 캡처하는 람다를 쓰세요.
- `Publish<T>` 는 구독자 컬렉션을 직접 순회합니다. 핸들러 안에서 `Subscribe` / `Unsubscribe(subscriber)` / `UnsubscribeAll(subscriber)` 를 호출하면 `InvalidOperationException` 이 발생합니다.
- 핸들러가 발행 중에 구독/해제를 할 수 있다면 `PublishSafe<T>` 를 쓰세요. 구독자 스냅샷을 `ArrayPool` 에서 빌린 배열에 복사한 뒤 순회합니다.
- `Unsubscribe<T>()` (인자 없음 또는 null)는 그 메시지 타입의 구독자를 **전부** 지우고, `UnsubscribeAll()` 은 브로커 전체를 비웁니다.

## 사용 예

```csharp
using Geuneda.Services;
using UnityEngine;

public struct EnemyDefeatedMessage : IMessage
{
    public int EnemyId;
    public Vector3 Position;
}

public class ScoreSystem
{
    private readonly IMessageBrokerService _broker;

    public ScoreSystem(IMessageBrokerService broker)
    {
        _broker = broker;
        _broker.Subscribe<EnemyDefeatedMessage>(OnEnemyDefeated); // 인스턴스 메서드만 가능
    }

    public void Dispose()
    {
        _broker.UnsubscribeAll(this); // 이 객체의 모든 구독 해제
    }

    private void OnEnemyDefeated(EnemyDefeatedMessage message)
    {
        Debug.Log($"Enemy {message.EnemyId} defeated");
    }
}

// 발행
var broker = new MessageBrokerService();
var score = new ScoreSystem(broker);

broker.Publish(new EnemyDefeatedMessage { EnemyId = 42, Position = Vector3.zero });

// 핸들러가 발행 중 구독/해제할 수 있을 때
broker.PublishSafe(new EnemyDefeatedMessage { EnemyId = 42 });

// 특정 메시지 타입의 구독자 전부 해제
broker.Unsubscribe<EnemyDefeatedMessage>();

// 브로커 전체 비우기
broker.UnsubscribeAll();
```

## 주의사항

- 핸들러가 예외를 던지면 내부의 "발행 중" 플래그가 해제되지 않아, 이후 `Subscribe` / `Unsubscribe` 호출이 계속 `InvalidOperationException` 을 낼 수 있습니다. 핸들러 안에서 예외를 처리하세요.
- 구독 순서는 보장되지 않습니다(내부적으로 `Dictionary` 를 사용).

## 예외

| 호출 | 예외 | 조건 |
|------|------|------|
| `Subscribe(정적 메서드)` | `ArgumentException` | `action.Target` 이 null |
| `Publish` 핸들러 안에서 `Subscribe` / `Unsubscribe(subscriber)` / `UnsubscribeAll(subscriber)` | `InvalidOperationException` | 발행 중 컬렉션 변경 |
