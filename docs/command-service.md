# 커맨드 서비스

[목차로 돌아가기](README.md)

게임 로직 객체에 대한 변경을 커맨드 객체로 감싸 실행하는 계층입니다. 커맨드는 메시지 브로커를 함께 받아 결과를 알릴 수 있습니다.

## 네임스페이스

- `CommandService<TGameLogic>` (구체 클래스): `Geuneda.Services`
- `ICommandService<TGameLogic>`, `IGameCommand<TGameLogic>`, `IGameServerCommand<TGameLogic>`, `IGameCommandBase`: `Geuneda.Services.Commands`

## 핵심 규칙

- 커맨드는 `IGameCommand<TGameLogic>` 의 `void Execute(TGameLogic gameLogic, IMessageBrokerService messageBroker)` 를 구현합니다. `TGameLogic` 은 참조 타입이어야 합니다.
- `ExecuteCommand` 는 **동기**로 바로 `Execute` 를 호출합니다. 큐, 되돌리기, 네트워크 전송은 없습니다.
- `IGameServerCommand<TGameLogic>` (`void ExecuteLogic(TGameLogic)`)은 계약만 정의되어 있고, `CommandService` 는 이를 실행하지 않습니다. 서버 측 실행기는 직접 구현해야 합니다.
- `CommandService<TGameLogic>` 는 파생 클래스용으로 `protected GameLogic`, `protected MessageBroker` 프로퍼티를 노출합니다.
- `ExecuteCommand` 는 `virtual` 이 아닙니다. 실행을 가로채려면 `ICommandService<TGameLogic>` 를 직접 구현하세요.

## 사용 예

```csharp
using Geuneda.Services;
using Geuneda.Services.Commands;

public class GameLogic
{
    public int PlayerLevel;
}

public struct PlayerLevelledUpMessage : IMessage
{
    public int Level;
}

public struct LevelUpCommand : IGameCommand<GameLogic>
{
    public void Execute(GameLogic gameLogic, IMessageBrokerService messageBroker)
    {
        gameLogic.PlayerLevel++;
        messageBroker.Publish(new PlayerLevelledUpMessage { Level = gameLogic.PlayerLevel });
    }
}

// 서버 측 실행기에서 쓰는 커맨드 계약 (CommandService 는 실행하지 않음)
public struct SyncLevelCommand : IGameServerCommand<GameLogic>
{
    public int NewLevel;

    public void ExecuteLogic(GameLogic gameLogic)
    {
        gameLogic.PlayerLevel = NewLevel;
    }
}

// 파생 클래스에서 protected 멤버 사용
public class MyCommandService : CommandService<GameLogic>
{
    public MyCommandService(GameLogic logic, IMessageBrokerService broker) : base(logic, broker) { }

    public void ResetLevel()
    {
        GameLogic.PlayerLevel = 1;
        MessageBroker.Publish(new PlayerLevelledUpMessage { Level = 1 });
    }
}

// 실행
var gameLogic = new GameLogic();
var messageBroker = new MessageBrokerService();
ICommandService<GameLogic> commandService = new CommandService<GameLogic>(gameLogic, messageBroker);

commandService.ExecuteCommand(new LevelUpCommand());
```

## struct 커맨드와 class 커맨드

`ExecuteCommand<TCommand>` 는 제네릭이라 `struct` 커맨드도 박싱 없이 전달됩니다. 다만 값 복사로 전달되므로 `Execute` 안에서 커맨드 자신의 필드를 바꿔도 호출자 쪽 변수에는 반영되지 않습니다. 실행 후 커맨드에 결과를 담아 돌려받아야 하면 `class` 로 정의하세요.
