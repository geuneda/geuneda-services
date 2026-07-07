# Message Broker Service

[← Back to index](README.md)

Decoupled pub/sub communication between game systems.

**Key Points:**
- Static method subscriptions are **not supported** (subscriber is keyed by `action.Target` — static methods have no target)
- `Publish<T>` iterates subscribers directly; calling `Subscribe`/`Unsubscribe` during publish **throws**
- Use `PublishSafe<T>` if handlers may subscribe/unsubscribe during message handling (copies delegates first, at allocation cost)
- `Unsubscribe<T>(null)` removes **all** subscribers for that message type
- `UnsubscribeAll(null)` clears **everything** from the broker

```csharp
public struct EnemyDefeatedMessage : IMessage
{
    public int EnemyId;
    public Vector3 Position;
}

var broker = new MessageBrokerService();

// Subscribe (instance methods only)
broker.Subscribe<EnemyDefeatedMessage>(OnEnemyDefeated);

// Publish
broker.Publish(new EnemyDefeatedMessage { EnemyId = 42, Position = Vector3.zero });

// Use PublishSafe when handlers may subscribe/unsubscribe during publish
broker.PublishSafe(new EnemyDefeatedMessage { EnemyId = 42 });

// Unsubscribe this object from one message type
broker.Unsubscribe<EnemyDefeatedMessage>(this);

// Unsubscribe ALL subscribers from one message type
broker.Unsubscribe<EnemyDefeatedMessage>();

// Unsubscribe this object from all message types
broker.UnsubscribeAll(this);

// Clear the entire broker
broker.UnsubscribeAll();
```

## Error Reference

| Call | Exception | Condition |
|------|-----------|-----------|
| `Subscribe(staticMethod)` | `ArgumentException` | `action.Target` is null |
| `Publish<T>()` during subscribe/unsubscribe | Exception | Mutation during iteration |
