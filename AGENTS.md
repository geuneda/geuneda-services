# Geuneda.Services - AI 에이전트 가이드

## 1. 패키지 개요
- **패키지**: `com.geuneda.services`
- **Unity**: 6000.0+
- **의존성** (`package.json` 참조)
  - `com.geuneda.gamedata` (**1.0.0**) (`RngService`에서 사용하는 `floatP` 포함)

이 패키지는 Unity 프로젝트를 위한 소규모 모듈식 "기반 서비스" 세트를 제공합니다 (서비스 로케이터/경량 DI, 메시징, 틱, 코루틴, 풀링, 영속성, RNG, 시간, 빌드 버전 헬퍼).

사용자 대상 문서는 `README.md`를 기본 진입점으로 사용하세요. 이 파일은 패키지 자체를 작업하는 기여자/에이전트를 위한 것입니다.

## 2. 런타임 아키텍처 (상위 수준)
- **서비스 로케이터 / 바인딩**: `Runtime/Installer.cs`, `Runtime/MainInstaller.cs`
  - `Installer`는 인터페이스 타입 → 인스턴스의 `Dictionary<Type, object>`를 저장합니다.
  - `MainInstaller`는 단일 `Installer` 인스턴스에 대한 **정적** 래퍼입니다 (전역 스코프).
  - 바인딩은 **인스턴스 기반**(`Bind<T>(T instance)`)이며, "타입 대 타입" 또는 수명 관리 DI가 아닙니다.
  - **인터페이스**만 바인딩 가능합니다 (인터페이스가 아닌 타입을 바인딩하면 예외가 발생합니다).
- **메시징**: `Runtime/MessageBrokerService.cs`
  - 메시지 계약: `IMessage`
  - `IMessageBrokerService`를 통한 Pub/Sub (`Publish`, `PublishSafe`, `Subscribe`, `Unsubscribe`, `UnsubscribeAll`)
  - 구독자를 `action.Target` 키로 저장합니다 (따라서 **정적 메서드 구독은 지원되지 않습니다**).
- **틱 / 업데이트 팬아웃**: `Runtime/TickService.cs`
  - `DontDestroyOnLoad` GameObject에 `TickServiceMonoBehaviour`를 생성하여 Update/LateUpdate/FixedUpdate 콜백을 구동합니다.
  - "버퍼링된" 틱(`deltaTime`)과 드리프트를 줄이기 위한 선택적 오버플로우 이월(`timeOverflowToNextTick`)을 지원합니다.
  - `realTime`에 따라 스케일된(`Time.time`) 또는 스케일되지 않은(`Time.realtimeSinceStartup`) 시간을 사용합니다.
- **코루틴 호스트**: `Runtime/CoroutineService.cs`
  - `DontDestroyOnLoad` GameObject에 `CoroutineServiceMonoBehaviour`를 생성하여 순수 C# 코드에서 코루틴을 실행합니다.
  - `IAsyncCoroutine` / `IAsyncCoroutine<T>`는 Unity `Coroutine`을 래핑하고 완료 콜백 + 상태 플래그를 제공합니다.
- **풀링**:
  - 풀 레지스트리: `Runtime/PoolService.cs` (`PoolService : IPoolService`)
  - 풀 구현: `Runtime/ObjectPool.cs`
    - 제네릭 `ObjectPool<T>`
    - Unity 전용: `GameObjectPool`, `GameObjectPool<TBehaviour>`
  - 생명주기 훅: `IPoolEntitySpawn`, `IPoolEntitySpawn<TData>`, `IPoolEntityDespawn`, `IPoolEntityObject<T>`
- **영속성**: `Runtime/DataService.cs`
  - `Type` 키 기반 인메모리 저장소
  - `PlayerPrefs` + `Newtonsoft.Json` 직렬화를 통한 디스크 영속성
- **시간 + 조작**: `Runtime/TimeService.cs`
  - 시간 조회(Unity / Unix / DateTime UTC) 및 오프셋 적용을 위한 `ITimeService` + `ITimeManipulator`.
- **결정론적 RNG**: `Runtime/RngService.cs`
  - 결정론적 RNG 상태가 `RngData`에 저장되고 `IRngData`를 통해 노출됩니다.
  - Float API는 결정론적 부동소수점 연산을 위해 `floatP`(`com.geuneda.gamedata` 제공)를 사용합니다.
- **빌드/버전 정보**: `Runtime/VersionServices.cs`
  - Resources TextAsset에서 로드된 버전 문자열 및 git/빌드 메타데이터에 대한 런타임 접근.

## 3. 주요 디렉토리 / 파일
- **Runtime**: `Runtime/`
  - 진입점: `MainInstaller.cs`, `Installer.cs`
  - 서비스: `MessageBrokerService.cs`, `TickService.cs`, `CoroutineService.cs`, `PoolService.cs`, `DataService.cs`, `TimeService.cs`, `RngService.cs`, `VersionServices.cs`
  - 풀링: `ObjectPool.cs`
- **Editor**: `Editor/`
  - 버전 데이터 생성: `VersionEditorUtils.cs`, `GitEditorProcess.cs`
  - 에디터 전용이어야 합니다 (`UnityEditor` + git 프로세스 실행에 의존)
- **Tests**: `Tests/`
  - 서비스 동작을 검증하는 EditMode/PlayMode 테스트

## 4. 중요한 동작 / 주의사항
- **`MainInstaller` API vs README 코드 예제**
  - `MainInstaller`는 `Bind/Resolve/TryResolve/Clean`을 노출하는 정적 클래스입니다.
  - 문서/예제에서 `MainInstaller.Instance`나 플루언트 바인딩을 참조하는 경우, 런타임 코드와 대조하여 확인하세요 - 해당 코드 예제가 오래되었을 수 있습니다.
- **메시지 브로커 변경 안전성**
  - `Publish<T>`는 구독자를 직접 순회합니다; 발행 중 구독/구독 해제는 차단되며 예외가 발생합니다.
  - 메시지 처리 중 연쇄 구독/구독 해제가 있는 경우 `PublishSafe<T>`를 사용하세요 (추가 할당 비용으로 델리게이트를 먼저 복사합니다).
  - `Subscribe`는 `action.Target`을 구독자 키로 사용하므로, **정적 메서드는 구독할 수 없습니다**.
- **틱/코루틴 서비스는 전역 GameObject를 할당합니다**
  - `TickService`와 `CoroutineService`는 각각 `DontDestroyOnLoad` GameObject를 생성합니다. 해제하려면 `Dispose()`를 호출하세요 (테스트, 게임 리셋, 도메인 리로드 엣지 케이스).
  - 이 서비스들은 런타임에서 싱글톤을 강제하지 **않습니다**; 여러 인스턴스를 생성하면 여러 호스트 GameObject가 생성됩니다.
- **`IAsyncCoroutine.StopCoroutine(triggerOnComplete)`**
  - 현재 구현에서는 `triggerOnComplete`가 `false`여도 완료 콜백이 트리거됩니다 (매개변수가 존중되지 않음). 취소 의미론에 의존하는 경우 이 점을 유의하세요.
- **DataService 영속성 세부사항**
  - 키는 `PlayerPrefs`에서 `typeof(T).Name`입니다 (어셈블리/같은 이름의 타입 간 이름 충돌이 가능합니다).
  - `LoadData<T>`는 데이터가 없을 때 `T`에 매개변수 없는 생성자가 필요합니다 (`Activator.CreateInstance<T>()` 사용).
- **풀 생명주기**
  - `PoolService`는 **타입당 하나의 풀**을 유지합니다; `AddPool<T>()` 중복 호출에 대한 보호가 없습니다 (중복 추가 시 `Dictionary.Add`에서 예외 발생).
  - `GameObjectPool.Dispose(bool)`은 `SampleEntity` GameObject를 파괴합니다; `GameObjectPool.Dispose()`는 풀링된 인스턴스를 파괴하지만 샘플 참조를 반드시 파괴하지는 않습니다 - 풀 동작을 변경할 때 해제 기대치를 명확히 하세요.
- `GameObjectPool`과 `GameObjectPool<T>`는 `CallOnSpawned`/`CallOnDespawned`(가상 메서드)를 오버라이드하여 **컴포넌트**의 생명주기 훅에 `GetComponent<IPoolEntitySpawn>()` / `GetComponent<IPoolEntityDespawn>()`를 사용합니다. 이는 엔티티를 직접 캐스팅하는 `ObjectPool<T>`와 다릅니다.
- **버전 데이터 파이프라인**
  - 런타임은 `version-data`(`VersionServices.VersionDataFilename`)라는 이름의 Resources TextAsset을 기대합니다.
  - `VersionEditorUtils`는 에디터 로드 시 `Assets/Configs/Resources/version-data.txt`를 작성하며 빌드 전에 호출될 수 있습니다. git CLI를 사용합니다; 실패는 적절히 처리되어야 합니다.
  - `VersionServices.VersionInternal` 같은 접근자는 버전 데이터가 아직 로드되지 않은 경우 예외를 발생시킵니다 - `VersionServices.LoadVersionDataAsync()`를 초기에 호출하세요 (로드 실패 처리 방법을 결정하세요).

## 5. 코딩 표준 (Unity 6 / C# 9.0)
- **C#**: C# 9.0 문법; 명시적 네임스페이스; 전역 using 없음.
- **어셈블리**
  - Runtime은 `UnityEditor`를 참조하면 안 됩니다.
  - 에디터 도구는 `Editor/` 하위에 위치해야 합니다 (반드시 필요한 경우 `#if UNITY_EDITOR`로 보호).
- **성능**
  - 핫 패스에서 할당에 주의하세요 (예: `PublishSafe`는 할당 발생; 틱 리스트 변경; 프레임당 할당 회피).

## 6. 외부 패키지 소스 (API 조회용)
필요 시 로컬 UPM 캐시 / 로컬 패키지를 우선 사용하세요:
- GameData: `Packages/com.geuneda.gamedata/` (예: `floatP`)
- Unity Newtonsoft JSON (Unity 패키지): 소스 세부 정보가 필요하면 `Library/PackageCache/`를 확인하세요

## 7. 개발 워크플로우 (일반적인 변경)
- **새 서비스 추가**
  - `Runtime/` 하위에 런타임 인터페이스 + 구현 추가 (가능하면 UnityEngine 사용을 최소화하세요).
  - `Tests/` 하위에 테스트 추가/조정.
  - 서비스에 Unity 콜백이 필요한 경우, `TickService`/`CoroutineService` 패턴을 따르세요 (단일 `DontDestroyOnLoad` 호스트 오브젝트 + `Dispose()`).
- **서비스 바인딩/해결**
  - `MainInstaller.Bind<IMyService>(myServiceInstance)`로 인스턴스를 바인딩합니다.
  - `MainInstaller.Resolve<IMyService>()` 또는 `TryResolve`로 해결합니다.
  - 리셋 시 `MainInstaller.Clean()` (또는 `Clean<T>()` / `CleanDispose<T>()`)으로 바인딩을 정리합니다.
- **버전 관리 업데이트**
  - `Assets/Configs/Resources/`에 `version-data.txt`가 올바르게 존재/업데이트되는지 확인하세요.
  - `VersionServices.VersionData`를 변경하는 경우, 런타임 파싱과 `VersionEditorUtils` 작성 로직을 모두 업데이트하세요.

## 8. 업데이트 정책
다음의 경우 이 파일을 업데이트하세요:
- 바인딩/서비스 로케이터 API가 변경될 때 (`Installer`, `MainInstaller`)
- 핵심 서비스 동작이 변경될 때 (발행 안전 규칙, 틱 타이밍, 코루틴 완료/취소 의미론, 풀링 생명주기)
- 버전 관리 파이프라인이 변경될 때 (리소스 파일명, 에디터 생성기 동작, 런타임 파싱)
- 의존성이 변경될 때 (`package.json`, `floatP` 같은 새 외부 타입)
