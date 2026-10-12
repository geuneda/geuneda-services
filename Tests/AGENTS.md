# Geuneda.Services 테스트 - AI 에이전트 가이드

`com.geuneda.services` 패키지의 테스트 규칙입니다. `Tests/` 아래 파일을 읽거나 고치거나 만들 때 이 파일이 기준입니다. 런타임 구조와 함정은 상위 [AGENTS.md](../AGENTS.md) 를 참고하세요.

## 1. 배치 규칙 (EditMode / PlayMode)

- **EditMode / Unit** (`EditMode/Unit/`): `MonoBehaviour` 나 `GameObject` 에 의존하지 않는 순수 로직. `[Test]` 사용. NSubstitute 는 EditMode asmdef 에만 참조되어 있습니다.
- **EditMode / Performance** (`EditMode/Performance/`): 플레이어 실행이 필요 없는 성능 측정. `PerformanceTestSetup` 필요(아래 참고).
- **PlayMode / Unit** (`PlayMode/Unit/`): `DontDestroyOnLoad` GameObject 를 만드는 서비스(`TickService`, `CoroutineService`, `GameObjectPool`, `GameObjectPool<T>`). `IEnumerator` 를 반환하는 `[UnityTest]` 사용.
- **PlayMode / Integration** (`PlayMode/Integration/`): 여러 서비스에 걸치거나 비동기인 흐름(예: 리소스를 로드하는 `VersionServicesIntegrationTest`).
- **PlayMode / Performance** (`PlayMode/Performance/`): 플레이어 실행이 필요한 성능 측정.
- **PlayMode / Smoke** (`PlayMode/Smoke/`): 서비스가 예외 없이 생성되고 기본 바인드/해석이 되는지 확인하는 가벼운 테스트.

판단 기준: 테스트 대상이 `GameObject` 를 만들거나 Unity 콜백에 의존하면 PlayMode, 아니면 EditMode.

어셈블리 이름: EditMode `Geuneda.Services.Editor.Tests`, PlayMode `Geuneda.Services.Tests.Playmode`.

## 2. 네임스페이스

모든 테스트 파일은 `namespace GeunedaEditor.Services.Tests` 를 쓰고 다음 주석을 붙입니다.

```csharp
// ReSharper disable once CheckNamespace
```

## 3. 이름 규칙

- 테스트 클래스: `{ServiceName}Test` (예: `ObjectPoolTest`, `TickServiceTest`). 성능은 `{ServiceName}PerformanceTest`, 통합은 `{ServiceName}IntegrationTest`.
- 테스트 메서드: `MethodOrBehavior_Condition_ExpectedResult` (예: `Spawn_Successfully`, `Range_MinEqualsMax_ReturnsMin`, `Despawn_NotSpawnedObject_ReturnsFalse`).
- SetUp 메서드: `Init()`.
- TearDown 메서드: `service.Dispose()` 를 호출하면 `Dispose()`, `Object.Destroy` / `MainInstaller.Clean()` 을 하면 `Cleanup()`.

## 4. Mock / 헬퍼 타입

- Mock 인터페이스와 클래스는 테스트 클래스 안의 **중첩 타입**으로 정의합니다(예: `IMockEntity`, `MockEntity`, `MockBehaviour`, `IMockSubscriber`).
- EditMode 는 NSubstitute(`Substitute.For<T>()`)로 인터페이스를 대체합니다. PlayMode 는 NSubstitute 를 참조하지 않으므로 수동 카운터를 가진 `MonoBehaviour` 스텁을 씁니다.

### Unity Mono 런타임에서의 NSubstitute 제약

NSubstitute 4.4.0(내장 Castle.Core DynamicProxy)은 타입 인자가 **자기 참조 제약 인터페이스**인 제네릭 인터페이스의 프록시를 만들지 못합니다. 예: `IMockEntity : IPoolEntityObject<IMockEntity>` 일 때 `Substitute.For<IObjectPool<IMockEntity>>()` 는 `Castle.DynamicProxy.Generators.Emitters.SimpleAST.LocalReference.Generate` → `ILGenerator.DeclareLocal(null)` 에서 `ArgumentNullException: localType` 으로 실패합니다. Mono 에서 타입 생성 중 제네릭 인자가 null 로 해석되는 Castle IL emitter 문제입니다.

이런 인터페이스를 대체해야 하면 다음 중 하나를 쓰세요.

- 실제 구현을 쓰고 관찰 가능한 상태로 검증합니다(예: `new ObjectPool<IMockEntity>(...)` 후 `SpawnedReadOnly.Count` 검사). 권장 방법이며 `ObjectPoolTest` 의 `EntityDespawn_Successfully` 를 참고하세요.
- 최소한의 가짜 클래스를 직접 작성합니다.

프록시 실패를 피하려고 타입 계층을 바꾸지 마세요. `IMockEntity : IPoolEntityObject<IMockEntity>` 는 런타임 코드가 기대하는 정상적인 모델링입니다.

## 5. 필드와 SetUp

- 필드는 `_` 접두사와 **구체 서비스 타입**을 씁니다: `private TickService _tickService;`, `private ObjectPool<IMockEntity> _pool;`
- 상수는 `PascalCase`: `private const int Seed = 12345;`
- `[SetUp]` 에서 새 서비스 인스턴스를 만듭니다. GameObject 를 만드는 서비스(`TickService`, `CoroutineService`)는 `[TearDown]` 에서 반드시 `Dispose()` 하고, `GameObjectPool` 테스트는 샘플 GameObject 도 `Object.Destroy` 합니다.

## 6. Assert 스타일

- NUnit 클래식 모델만 사용합니다: `Assert.AreEqual`, `Assert.AreSame`, `Assert.IsTrue`, `Assert.Throws<T>`, `Assert.DoesNotThrow` 등.
- 기존 테스트에는 제약 모델(`Assert.That(...)`)을 쓰지 않습니다.

## 7. 성능 테스트

- `[Test, Performance]` 와 `[Category("Performance")]` 를 붙입니다.
- 클래스에 `[PrebuildSetup(typeof(PerformanceTestSetup))]` 를 붙이고 `[OneTimeSetUp]` 에서 `PerformanceTestSetup.InitializePerformanceTestMetadata()` 를 호출합니다.
- `Measure.Method(() => { ... }).WarmupCount(n).MeasurementCount(n).Run()` 형태로 측정합니다.

### `PerformanceTestSetup` 의 PlayerPrefs 계약 (되돌리지 말 것)

`InitializePerformanceTestMetadata()` 는 `Measure.Method(...).Run()` 전에 PlayerPrefs 키 **두 개**를 채워야 합니다. 하나라도 빠지면 실제 테스트 로직을 가리는 NRE 가 납니다.

- `PT_Run`: 전체 Run 메타데이터(에디터 정보, 의존성, 빌드 설정). 결과를 낼 때 `Metadata.SetRuntimeSettings()` 가 읽습니다.
- `PT_Settings`: RunSettings JSON(`"{\"MeasurementCount\":-1}"`). 첫 워밍업 **전에** `MethodMeasurement.SettingsOverride()` 가 읽습니다.

두 키가 모두 필요한 이유: `RunSettings.Instance` 는 지연 로드 싱글톤(`ResourcesLoader.Load<RunSettings>("PerformanceTestRunSettings", "PT_Settings")`)입니다. 에디터에서는 `PlayerPrefs.GetString("PT_Settings")` 로 폴백하는데, 값이 비어 있으면 `JsonUtility.FromJson` 이 던진 예외를 로더가 삼키고 `null` 을 반환하며, 이어서 `SettingsOverride()` 가 `RunSettings.Instance.MeasurementCount` 에서 NRE 를 냅니다. 오류 위치(`MethodMeasurement.cs`)만으로는 설정 누락이 원인이라는 단서가 없습니다.

`MeasurementCount = -1` 은 "재정의 없음" 값입니다. `SettingsOverride()` 는 `count < 0` 이면 바로 반환하므로 각 테스트의 `WarmupCount(...).MeasurementCount(...)` 가 유지됩니다.

`PerformanceTestSetupTest.MeasureMethod_AfterInitialize_DoesNotThrow` 가 이 계약의 회귀 감시용입니다. `Assert.DoesNotThrow` 로 감싼 빈 `Measure.Method(() => {}).WarmupCount(1).MeasurementCount(1).Run()` 이며, `PerformanceTestSetup` 에서 키가 빠지면 가장 먼저 실패합니다. 항상 통과 상태로 유지하세요.

## 8. 통합 테스트

- 순서가 필요한 테스트에는 `[Order(n)]` 을 씁니다(예: `VersionServicesIntegrationTest` 는 정적 상태 초기화 → 로드 → 읽기 순).
- 공유 정적 상태는 `[SetUp]` 에서 초기화합니다. `VersionServices` 같은 정적 클래스는 private 필드 리플렉션이 허용됩니다.

### 허용된 리플렉션 지점 (저장값 검증 예외)

setter 의 결과를 공개 API 로 읽어 올 방법이 없고, 부수 효과를 실행하려면 EditMode 환경이 제공할 수 없는 런타임(예: 일부만 로드된 `AssetReference`)이 필요한 경우에도 private 상태 리플렉션을 허용합니다. 이때 `BindingFlags.NonPublic | BindingFlags.Instance` 로 저장 필드를 직접 검사하는 편이 테스트 생략보다 낫습니다. 테스트 메서드는 setter 저장값 하나만 검사해야 하며, 동작을 검증해야 한다면 대신 `InternalsVisibleTo` 아래 internal 접근자를 추가하세요.

현재 허용된 지점:

- `AssetResolverServiceTest.AddDebugConfigs_StoresAllProvided`: `AddDebugConfigs` 가 인자를 저장했는지 확인하려고 private `AssetResolverService._errorMaterial` 을 읽습니다. 대체 머티리얼 경로(`AssetResolverService.Convert<T>` 의 `!assetReference.IsDone` 분기)는 실제 Addressables 카탈로그 없이 EditMode 에서 재현할 수 없습니다.

## 9. 디렉터리 구성

| 디렉터리 | 내용 |
|----------|------|
| `EditMode/Unit/` | NUnit + NSubstitute. MonoBehaviour 가 아닌 서비스 전체, 에셋 설정/리졸버(`AddressableConfigTest`, `AssetConfigsScriptableObjectTest`, `AssetLoaderUtilsTest`, `AssetResolverServiceTest`), 에디터 설정(`AddressableIdsEditorSettingsTest`, `AssetsImporterEditorSettingsTest`, `VersioningEditorSettingsTest`) |
| `EditMode/Performance/` | `Unity.PerformanceTesting`. ObjectPool, MessageBroker 성능 |
| `PlayMode/Unit/` | TickService, CoroutineService, GameObjectPool, GameObjectPool\<T\> |
| `PlayMode/Integration/` | `ServiceLifecycleTest`(전체 부트스트랩/정리), `VersionServicesIntegrationTest`(비동기 리소스 로드) |
| `PlayMode/Performance/` | TickService, GameObjectPool 성능 |
| `PlayMode/Smoke/` | `ServicesBootstrapSmokeTest` |

### `AddressablesAssetLoader` 커버리지

`AddressablesAssetLoader` 는 의도적으로 자동 통합 테스트를 두지 않습니다. 분기 없는 `Addressables` 정적 API 래퍼(로드 → `ToUniTask` → 실패 시 throw → 반환)이고, 실제 통합 테스트에는 미리 빌드한 Addressables 카탈로그와 호스트 프로젝트에 등록된 에셋이 필요해 패키지 코드가 아니라 Unity 코드를 검증하게 됩니다. 소비 계층인 `AssetResolverService` 는 `AssetResolverServiceTest` 로 커버하며, 래퍼 동작은 `docs/asset-loading.md` 에 정리되어 있습니다.

## 10. 갱신 정책

다음이 바뀌면 이 파일을 고치세요.

- 테스트 규칙(asmdef 참조, Assert 스타일, 이름 패턴, 테스트 분류)
- 테스트 디렉터리나 분류 추가
- Mock/스텁 방식(예: PlayMode asmdef 에 NSubstitute 추가)
