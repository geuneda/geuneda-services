# 서비스 로케이터 — MainInstaller / Installer

[목차로 돌아가기](README.md)

인터페이스 타입을 키로 인스턴스를 등록하고 꺼내 쓰는 가벼운 서비스 로케이터입니다. 네임스페이스는 `Geuneda.Services` 입니다.

- `Installer` (`IInstaller` 구현): 인스턴스 단위 컨테이너. 여러 개를 만들 수 있고, 한 인스턴스를 여러 인터페이스에 동시에 바인딩할 수 있습니다.
- `MainInstaller`: 내부에 `Installer` 하나를 가진 정적 클래스. 게임 전역 서비스용입니다.

## 핵심 규칙

- **인터페이스만** 바인딩할 수 있습니다. 제네릭 인자 `T` 가 인터페이스가 아니면 `ArgumentException` 이 발생합니다.
- 바인딩은 **인스턴스 기반**입니다. 타입 대 타입 매핑, 생성자 주입, 수명 관리는 없습니다.
- 같은 인터페이스를 다시 바인딩하면 `ArgumentException` 이 발생합니다(덮어쓰기 없음). 먼저 `Clean<T>()` 하세요.
- `MainInstaller.Bind<T>` 는 단일 인터페이스만 받습니다. 다중 인터페이스 바인딩(`Bind<T, T1, T2>`, `Bind<T, T1, T2, T3>`)은 `Installer` 에만 있습니다.
- `MainInstaller.Bind<T>` 는 `void` 를 반환하므로 체인 호출이 안 됩니다. `Installer.Bind` 는 `IInstaller` 를 반환해 체인 호출이 됩니다.

## 사용 예

```csharp
using Geuneda.Services;

// 단일 인터페이스 바인딩
MainInstaller.Bind<IMessageBrokerService>(new MessageBrokerService());
MainInstaller.Bind<IDataService>(new DataService());

// 같은 인스턴스를 여러 인터페이스로 쓰려면 MainInstaller 에서는 각각 바인딩한다
var timeService = new TimeService();
MainInstaller.Bind<ITimeService>(timeService);
MainInstaller.Bind<ITimeManipulator>(timeService);

// 해석
var messageBroker = MainInstaller.Resolve<IMessageBrokerService>();

// 안전한 해석 (없으면 예외 대신 false)
if (MainInstaller.TryResolve<IDataService>(out var dataService))
{
    dataService.SaveAllData();
}

// 정리
MainInstaller.Clean<IMessageBrokerService>();   // 바인딩 하나 제거
MainInstaller.CleanDispose<ITickService>();      // Dispose() 호출 후 제거 (T : IDisposable)
MainInstaller.Clean();                           // 전체 제거 (Dispose 는 호출하지 않음)

// 다중 인터페이스 바인딩: Installer 를 직접 사용
var installer = new Installer();
installer.Bind<TimeService, ITimeService, ITimeManipulator>(new TimeService());

// Installer.Bind 는 체인 호출 가능
installer.Bind<IMessageBrokerService>(new MessageBrokerService())
         .Bind<IDataService>(new DataService());
```

## API 요약

| 멤버 | `Installer` | `MainInstaller` |
|------|-------------|-----------------|
| `Bind<T>(T instance)` | `IInstaller` 반환 | `void` |
| `Bind<T, T1, T2>(T instance)` / `Bind<T, T1, T2, T3>(T instance)` | 있음 (`T` 는 구현 타입, `T1..T3` 는 인터페이스) | 없음 |
| `TryResolve<T>(out T instance)` | `bool` | `bool` |
| `Resolve<T>()` | `T` | `T` |
| `Clean<T>()` | `bool` | `bool` |
| `CleanDispose<T>()` | 없음 | `bool` (`T : class, IDisposable`) |
| `Clean()` | `void` | `void` |

## 주의사항

- `MainInstaller.Clean()` 은 바인딩만 지우고 `Dispose()` 를 호출하지 않습니다. `TickService`, `CoroutineService`, `PoolService` 처럼 정리가 필요한 서비스는 `CleanDispose<T>()` 로 먼저 정리하세요.
- `MainInstaller` 는 정적 상태지만, 플레이 세션이 시작될 때(`RuntimeInitializeLoadType.SubsystemRegistration`) 바인딩을 비우므로 도메인 리로드를 끈 에디터(Enter Play Mode Options)에서도 이전 세션의 바인딩이 남지 않습니다(Unreleased). 이전 세션의 객체는 `Dispose` 하지 않고 참조만 버립니다. 정리가 필요한 서비스는 세션이 끝날 때 `CleanDispose<T>()` 로 직접 정리하세요.
- 같은 이유로 `SubsystemRegistration` 콜백에서 바인딩하면 지워질 수 있습니다(어셈블리 간 실행 순서가 정해져 있지 않음). 바인딩은 `AfterAssembliesLoaded` 이후(씬 로드, `Awake` 등)에 하세요.
- 에디터의 Services Explorer 는 `MainInstaller` 에 바인딩된 서비스만 표시합니다.

## 예외

| 호출 | 예외 | 조건 |
|------|------|------|
| `Bind<T>(instance)` | `ArgumentException` | `T` 가 인터페이스가 아님 |
| `Bind<T>(instance)` | `ArgumentException` | `T` 가 이미 바인딩됨 |
| `Resolve<T>()` | `ArgumentException` | `T` 가 바인딩되지 않음 |
