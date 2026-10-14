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

## 바인딩 변경 알림 (`BindingChanged`)

`Installer.BindingChanged` 와 `MainInstaller.BindingChanged`(정적) 이벤트는 바인딩이 추가되거나 지워진 뒤 그 인터페이스 타입과 함께 발생합니다. 화면 컴포넌트처럼 서비스보다 먼저 만들어질 수 있거나, 서비스가 교체될 때(로그아웃 후 새 인스턴스 등) 따라가야 하는 쪽이 구독합니다. 레드닷 `RedDotBinding` 이 이렇게 씁니다.

```csharp
private void OnEnable()
{
    MainInstaller.BindingChanged += OnBindingChanged;
    OnBindingChanged(typeof(IMyService));   // 지금 상태로 한 번 맞춘다
}

private void OnDisable() => MainInstaller.BindingChanged -= OnBindingChanged;

private void OnBindingChanged(Type type)
{
    if (type != typeof(IMyService))
    {
        return;
    }

    MainInstaller.TryResolve<IMyService>(out var service);   // 없으면 null
    Show(service);
}
```

- 처리기 안에서 해석하면 바뀐 뒤 상태가 보입니다. `Bind<T, T1, T2>` 는 인터페이스마다, `Clean()` 은 모두 지운 뒤 지운 타입마다 한 번씩 발생합니다.
- 예외로 실패한 인터페이스 바인딩이나 지운 것이 없는 `Clean<T>()` 에서는 발생하지 않습니다. 다중 인터페이스 `Bind<T, T1, T2>` 는 인터페이스마다 차례로 바인딩하므로, 뒤쪽에서 예외가 나도 앞서 성공한 바인딩은 남고 그 알림도 이미 발생했습니다.
- 처리기 예외는 로그(`Geuneda.Services.Installer` 모듈)로 남기고 나머지 처리기를 계속 부릅니다. `Bind`/`Clean` 호출자에게는 전파하지 않습니다.
- 처리기 목록은 발생 시점의 스냅샷으로 순회합니다. 처리기 안에서 해제한 다른 처리기도 이번 알림에서는 불릴 수 있으니(일반 C# 이벤트와 같음) 처리기에서 자기 상태를 확인합니다.
- 일반 이벤트와 달리 += / -= 가 처리기 배열을 새로 만들지 않아, 화면 컴포넌트 수백 개가 구독해도 등록·해제가 가비지를 만들지 않습니다. 해제는 같은 델리게이트 중 마지막에 등록한 하나를 지우고, 합성 델리게이트(a + b)는 처리기 하나로 다룹니다.
- `MainInstaller` 의 처리기는 플레이 세션이 시작될 때 바인딩과 함께 알림 없이 버립니다. 구독한 쪽은 `OnDisable`/`OnDestroy` 에서 직접 해제하세요.
- `IInstaller` 인터페이스에는 없습니다. `Installer` 클래스와 `MainInstaller` 에만 있습니다.

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
| `BindingChanged` (`event Action<Type>`) | 있음 | 있음 (정적) |

## 주의사항

- `MainInstaller.Clean()` 은 바인딩만 지우고 `Dispose()` 를 호출하지 않습니다. `TickService`, `CoroutineService`, `PoolService` 처럼 정리가 필요한 서비스는 `CleanDispose<T>()` 로 먼저 정리하세요.
- `MainInstaller` 는 정적 상태지만, 플레이 세션이 시작될 때(`RuntimeInitializeLoadType.SubsystemRegistration`) 바인딩과 `BindingChanged` 처리기를 비우므로 도메인 리로드를 끈 에디터(Enter Play Mode Options)에서도 이전 세션의 바인딩이 남지 않습니다(Unreleased). 이전 세션의 객체는 `Dispose` 하지 않고 참조만 버립니다. 정리가 필요한 서비스는 세션이 끝날 때 `CleanDispose<T>()` 로 직접 정리하세요.
- 같은 이유로 `SubsystemRegistration` 콜백에서 바인딩하면 지워질 수 있습니다(어셈블리 간 실행 순서가 정해져 있지 않음). 바인딩은 `AfterAssembliesLoaded` 이후(씬 로드, `Awake` 등)에 하세요.
- 에디터의 Services Explorer 는 `MainInstaller` 에 바인딩된 서비스만 표시합니다.

## 예외

| 호출 | 예외 | 조건 |
|------|------|------|
| `Bind<T>(instance)` | `ArgumentException` | `T` 가 인터페이스가 아님 |
| `Bind<T>(instance)` | `ArgumentException` | `T` 가 이미 바인딩됨 |
| `Resolve<T>()` | `ArgumentException` | `T` 가 바인딩되지 않음 |
