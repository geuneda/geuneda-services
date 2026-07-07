# Command Service

[← Back to index](README.md)

Typed, decoupled command execution with message broker integration.

**Key Points:**
- Commands implement `IGameCommand<TGameLogic>` and are executed **synchronously**
- Use structs for simple fire-and-forget commands; use classes when you need reference semantics
- Server-only variant: `IGameServerCommand<TGameLogic>` with `void ExecuteLogic(TGameLogic)` (no broker parameter)
- `CommandService<TGameLogic>` exposes `protected TGameLogic GameLogic` and `protected IMessageBrokerService MessageBroker` for subclassing
- `ExecuteCommand` is not declared `virtual`; to intercept execution, subclass and shadow with `new`, or implement `ICommandService<TGameLogic>` directly

```csharp
// Define your game logic container
public class GameLogic
{
    public int PlayerLevel;
}

// Define a command (struct for fire-and-forget)
public struct LevelUpCommand : IGameCommand<GameLogic>
{
    public void Execute(GameLogic gameLogic, IMessageBrokerService messageBroker)
    {
        gameLogic.PlayerLevel++;
        messageBroker.Publish(new PlayerLevelledUpMessage { Level = gameLogic.PlayerLevel });
    }
}

// Server-only command (no message broker)
public struct SyncLevelCommand : IGameServerCommand<GameLogic>
{
    public int NewLevel;
    public void ExecuteLogic(GameLogic gameLogic)
    {
        gameLogic.PlayerLevel = NewLevel;
    }
}

// Set up
var gameLogic = new GameLogic();
var messageBroker = new MessageBrokerService();
ICommandService<GameLogic> commandService = new CommandService<GameLogic>(gameLogic, messageBroker);

// Execute
commandService.ExecuteCommand(new LevelUpCommand());

// Extend CommandService for cross-cutting concerns
public class MyCommandService : CommandService<GameLogic>
{
    public MyCommandService(GameLogic logic, IMessageBrokerService broker) : base(logic, broker) { }

    public void CustomOperation()
    {
        // Access protected base properties
        GameLogic.PlayerLevel++;
        MessageBroker.Publish(new SomeMessage());
    }
}
```
