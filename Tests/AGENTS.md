# Geuneda.Services Tests - AI Agent Guide

This file contains testing conventions for the `com.geuneda.services` package. It is the source of truth when reading, editing, or creating test files under `Tests/`.

For runtime architecture, gotchas, and package-level context, see the parent [`AGENTS.md`](../AGENTS.md).

## 1. Placement Rules (EditMode vs PlayMode)
- **EditMode / Unit** (`EditMode/Unit/`): Pure-logic services with no `MonoBehaviour` or `GameObject` dependency. Use `[Test]`. NSubstitute is available (referenced only in the EditMode asmdef).
- **EditMode / Performance** (`EditMode/Performance/`): Perf benchmarks that do not need a running player. Require `PerformanceTestSetup` (see below).
- **PlayMode / Unit** (`PlayMode/Unit/`): Services that create `DontDestroyOnLoad` GameObjects (`TickService`, `CoroutineService`, `GameObjectPool`, `GameObjectPool<T>`). Use `[UnityTest]` returning `IEnumerator`.
- **PlayMode / Integration** (`PlayMode/Integration/`): Cross-service or async workflows (e.g., `VersionServicesIntegrationTest` loads resources).
- **PlayMode / Performance** (`PlayMode/Performance/`): Perf benchmarks that need a running player.
- **PlayMode / Smoke** (`PlayMode/Smoke/`): Lightweight "construct without throwing" tests that confirm services instantiate and basic bind/resolve works.

**Decision tree**: if the service under test creates a `GameObject` or relies on Unity callbacks → **PlayMode**; otherwise → **EditMode**.

## 2. Namespace and Suppression
All test files use `namespace GeunedaEditor.Services.Tests` with the suppression comment:
```csharp
// ReSharper disable once CheckNamespace
```

## 3. Naming
- **Test class**: `{ServiceName}Test` (e.g., `ObjectPoolTest`, `TickServiceTest`). Performance tests use `{ServiceName}PerformanceTest`. Integration tests use `{ServiceName}IntegrationTest`.
- **Test method**: `MethodOrBehavior_Condition_ExpectedResult` — e.g., `Spawn_Successfully`, `Range_MinEqualsMax_ReturnsMin`, `Despawn_NotSpawnedObject_ReturnsFalse`.
- **SetUp method**: Named `Init()`.
- **TearDown method**: Named `Dispose()` (when calling `service.Dispose()`) or `Cleanup()` (when doing `Object.Destroy` / `MainInstaller.Clean()`).

## 4. Mock / Helper Types
- Define mock interfaces and classes as **nested types** inside the test class (e.g., `IMockEntity`, `MockEntity`, `MockBehaviour`, `IMockSubscriber`).
- EditMode tests use **NSubstitute** (`Substitute.For<T>()`) for interface mocking. PlayMode tests use concrete `MonoBehaviour` stubs with manual counters (NSubstitute is not referenced in the PlayMode asmdef).

### NSubstitute limitation on Unity's Mono runtime
NSubstitute 4.4.0 (bundled Castle.Core DynamicProxy) cannot generate a proxy for a generic interface whose type argument is a **self-referentially-constrained interface**. Example: `Substitute.For<IObjectPool<IMockEntity>>()` where `IMockEntity : IPoolEntityObject<IMockEntity>` fails with `ArgumentNullException: localType` deep in `Castle.DynamicProxy.Generators.Emitters.SimpleAST.LocalReference.Generate` → `ILGenerator.DeclareLocal(null)`. Root cause is Castle's IL emitter resolving a generic parameter to `null` during type-building on Mono.

When a test would otherwise substitute such an interface, do ONE of:
- Use the real concrete implementation and verify via observable state (e.g., `new ObjectPool<IMockEntity>(...)` + assertions on `SpawnedReadOnly.Count`). This is preferred — see `EntityDespawn_Successfully` in `ObjectPoolTest`.
- Hand-write a minimal fake class implementing the interface.
Do not "work around" the proxy failure by restructuring the type hierarchy — `IMockEntity : IPoolEntityObject<IMockEntity>` is a legitimate modelling choice that the runtime code relies on.

## 5. Fields and Setup
- Fields are prefixed with `_` and use **concrete service types** (not interfaces): `private TickService _tickService;`, `private ObjectPool<IMockEntity> _pool;`.
- Constants use `PascalCase`: `private const int Seed = 12345;`.
- `[SetUp]` creates fresh service instances. Services that create GameObjects (`TickService`, `CoroutineService`) **must** call `Dispose()` in `[TearDown]`; `GameObjectPool` tests also `Object.Destroy` the sample GameObject.

## 6. Assertion Style
- NUnit classic model only: `Assert.AreEqual`, `Assert.AreSame`, `Assert.IsTrue`, `Assert.Throws<T>`, `Assert.DoesNotThrow`, etc.
- No constraint-model (`Assert.That(...)`) usage in the existing suite.

## 7. Performance Tests
- Annotate with `[Test, Performance]` and `[Category("Performance")]`.
- Apply `[PrebuildSetup(typeof(PerformanceTestSetup))]` at the class level and call `PerformanceTestSetup.InitializePerformanceTestMetadata()` in `[OneTimeSetUp]`.
- Use `Measure.Method(() => { ... }).WarmupCount(n).MeasurementCount(n).Run()`.

### `PerformanceTestSetup` PlayerPref contract (do NOT regress)
`InitializePerformanceTestMetadata()` MUST prime **two** PlayerPref keys before any `Measure.Method(...).Run()` call — dropping either one ships a latent NRE that masks the actual perf-test logic:
- `PT_Run` — full Run metadata (editor info, dependencies, build settings); consumed by `Metadata.SetRuntimeSettings()` when results are emitted.
- `PT_Settings` — RunSettings JSON (use `"{\"MeasurementCount\":-1}"`); consumed by `MethodMeasurement.SettingsOverride()` *before* the first warmup.

Why both keys: `RunSettings.Instance` is a lazy-loaded singleton (`ResourcesLoader.Load<RunSettings>("PerformanceTestRunSettings", "PT_Settings")`). In Editor it falls back to `PlayerPrefs.GetString("PT_Settings")`; if the value is empty, `JsonUtility.FromJson` throws, the loader silently swallows the exception and returns `null`, and `SettingsOverride()` then NREs at `RunSettings.Instance.MeasurementCount`. The failure surfaces at `MethodMeasurement.cs:288` with no hint that the setup is incomplete.

`MeasurementCount = -1` is the package's "no override" sentinel — `SettingsOverride()` early-returns when `count < 0`, so each fixture's per-test `WarmupCount(...).MeasurementCount(...)` is preserved.

`PerformanceTestSetupTest.MeasureMethod_AfterInitialize_DoesNotThrow` is the regression sentinel for this contract: a no-op `Measure.Method(() => {}).WarmupCount(1).MeasurementCount(1).Run()` wrapped in `Assert.DoesNotThrow`. If a future change to `PerformanceTestSetup` drops either PlayerPref, this test fails first with a class name that points directly at the harness — keep it green.

## 8. Integration Tests
- Use `[Order(n)]` when tests must run in sequence (e.g., `VersionServicesIntegrationTest` resets static state, then loads, then reads).
- Reset shared static state in `[SetUp]` (reflection into private fields is acceptable for static classes like `VersionServices`).

### Authorized reflection sites (storage-assertion exception)
Reflection on private state is also authorized when a setter has no observable readback path through the public API and exercising the side-effect would require a runtime environment the EditMode harness cannot provide (e.g., a partially-loaded `AssetReference`). In those cases, asserting the storage field directly via `BindingFlags.NonPublic | BindingFlags.Instance` is acceptable and preferable to a Red-testability skip. The test method MUST be a single setter-storage assertion (not a multi-step behavioural assertion); if behaviour is what you need to verify, refactor to expose an `internal` accessor under `InternalsVisibleTo` instead.

Currently authorized:
- `AssetResolverServiceTest.AddDebugConfigs_StoresAllProvided` — reads the private `AssetResolverService._errorMaterial` field to confirm `AddDebugConfigs` stored its argument. The fallback-material lookup path (`AssetResolverService.Convert<T>` at `Runtime/AssetResolverService.cs:474`) only fires when `!assetReference.IsDone`, which the EditMode harness cannot fabricate without a real Addressables catalog. Documented here per the Type B audit run on 2026-05-04 (Referee §4 missed-anti-pattern finding, parent picked option A).

## 9. Test Directory Layout

| Directory | Contents |
|-----------|----------|
| `EditMode/Unit/` | NUnit + NSubstitute; tests all non-MonoBehaviour services, incl. `AddressableConfigTest`, `AssetLoaderUtilsTest`, `AssetResolverServiceTest` |
| `EditMode/Performance/` | `Unity.PerformanceTesting`; ObjectPool, MessageBroker perf |
| `PlayMode/Unit/` | TickService, CoroutineService, GameObjectPool, GameObjectPool\<T\> (require a runtime) |
| `PlayMode/Integration/` | `ServiceLifecycleTest` full bootstrap/teardown, `VersionServicesIntegrationTest` async resource loading |
| `PlayMode/Performance/` | TickService, GameObjectPool perf |
| `PlayMode/Smoke/` | `ServicesBootstrapSmokeTest` |

### Note on `AddressablesAssetLoader` coverage
`AddressablesAssetLoader` is intentionally not covered by automated integration tests. It is a thin wrapper over `UnityEngine.AddressableAssets.Addressables` static APIs with no branching logic — every method is `LoadAssetAsync → ToUniTask → throw-on-failure → return`. Live integration would require a pre-built Addressables catalog plus a manually registered asset in the host project, and would validate Unity code rather than package code. The consumer layer (`AssetResolverService`) has full unit coverage via `AssetResolverServiceTest`, and the wrapper's behaviour is documented in `docs/asset-loading.md`.

## 10. Update Policy
Update this file when:
- Test conventions change (new asmdef references, assertion style, naming patterns, new test categories)
- New test directories or categories are added
- Mock/stub patterns change (e.g., NSubstitute added to PlayMode asmdef)
