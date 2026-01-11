# GameLovers Services

[![Unity Version](https://img.shields.io/badge/Unity-6000.0%2B-blue.svg)](https://unity3d.com/get-unity/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Version](https://img.shields.io/badge/version-1.0.0-green.svg)](CHANGELOG.md)

> **Quick Links**: [Installation](#installation) | [Quick Start](#quick-start) | [Services](#services-documentation) | [Contributing](#contributing)

## Why Use This Package?

Building robust game architecture in Unity often leads to tightly coupled systems, scattered initialization logic, and memory management headaches. This **Services** package solves these pain points:

| Problem | Solution |
|---------|----------|
| **Scattered dependencies** | Lightweight service locator (`MainInstaller`) for centralized dependency management |
| **Tightly coupled systems** | Message broker enables decoupled pub/sub communication |
| **Manual update management** | Tick service centralizes Update/FixedUpdate/LateUpdate callbacks |
| **Coroutines in pure C#** | Coroutine service runs Unity coroutines without MonoBehaviour |
| **Memory churn from instantiation** | Object pooling with lifecycle hooks for efficient reuse |
| **Inconsistent save/load** | Cross-platform data persistence with automatic serialization |
| **Non-deterministic gameplay** | Deterministic RNG service with state save/restore |
| **Version tracking complexity** | Build version service with git commit/branch metadata |

**Built for production:** Zero external dependencies beyond Unity. Minimal per-frame allocations. Used in real games.

### Key Features

- **🏗️ Service Locator** - Simple DI-lite pattern with `MainInstaller`
- **📨 Message Broker** - Type-safe decoupled pub/sub communication
- **⏱️ Tick Service** - Centralized Unity update cycle management
- **🔄 Coroutine Host** - Run coroutines from pure C# classes
- **🎯 Object Pooling** - Efficient GameObject and object reuse
- **💾 Data Persistence** - Cross-platform save/load with JSON serialization
- **🎲 Deterministic RNG** - Reproducible random number generation
- **📋 Version Services** - Runtime access to build/git metadata
- **🎮 Command Pattern** - Decoupled command execution layer
- **⏰ Time Service** - Unified access to Unity/Unix/DateTime

---

## System Requirements

- **[Unity](https://unity.com/download)** 6000.0+ (Unity 6)
- **[GameLovers DataExtensions](https://github.com/CoderGamester/com.gamelovers.dataextensions)** (v0.6.2) - Automatically resolved

### Compatibility Matrix

| Unity Version | Status | Notes |
|---------------|--------|-------|
| 6000.0+ (Unity 6) | ✅ Fully Tested | Primary development target |
| 2022.3 LTS | ⚠️ Untested | May require minor adaptations |

| Platform | Status | Notes |
|----------|--------|-------|
| Standalone (Windows/Mac/Linux) | ✅ Supported | Full feature support |
| WebGL | ✅ Supported | Full feature support |
| Mobile (iOS/Android) | ✅ Supported | Full feature support |
| Console | ⚠️ Untested | Should work without modifications |

## Installation

### Via Unity Package Manager (Recommended)

1. Open Unity Package Manager (`Window` → `Package Manager`)
2. Click the `+` button and select `Add package from git URL`
3. Enter the following URL:
   ```
   https://github.com/CoderGamester/com.gamelovers.services.git
   ```

### Via manifest.json

Add the following line to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.gamelovers.services": "https://github.com/CoderGamester/com.gamelovers.services.git"
  }
}
```

---

## Package Structure

```
Runtime/
├── Installer.cs              # Core DI container
├── MainInstaller.cs          # Static global service locator
├── MessageBrokerService.cs   # Pub/sub messaging
├── TickService.cs            # Update cycle management
├── CoroutineService.cs       # MonoBehaviour-free coroutines
├── PoolService.cs            # Pool registry
├── ObjectPool.cs             # Pool implementations
├── DataService.cs            # Persistence layer
├── TimeService.cs            # Time abstraction
├── RngService.cs             # Deterministic RNG
├── VersionServices.cs        # Build/git metadata
└── CommandService.cs         # Command pattern

Editor/
├── VersionEditorUtils.cs     # Version data generation
└── GitEditorProcess.cs       # Git CLI integration

Tests/
├── EditMode/                 # Unit tests
└── PlayMode/                 # Integration tests
```

### Key Files

| Component | Responsibility |
|-----------|----------------|
| **MainInstaller** | Static service locator for global scope bindings |
| **Installer** | Instance-based DI container (for scoped installations) |
| **IMessageBrokerService** | Type-safe pub/sub messaging interface |
| **ITickService** | Centralized Update/FixedUpdate/LateUpdate callbacks |
| **ICoroutineService** | Run coroutines without MonoBehaviour |
| **IPoolService** | Object pool registry and management |
| **IDataService** | Cross-platform data persistence |
| **ITimeService** | Unified time access (Unity/Unix/DateTime) |
| **IRngService** | Deterministic random number generation |
| **VersionServices** | Runtime build/git metadata |

---

## Quick Start

### 1. Initialize Services

```csharp
using UnityEngine;
using GameLovers.Services;

public class GameBootstrap : MonoBehaviour
{
    void Awake()
    {
        // Create service instances
        var messageBroker = new MessageBrokerService();
        var tickService = new TickService();
        var dataService = new DataService();
        
        // Bind to MainInstaller (interfaces only)
        MainInstaller.Bind<IMessageBrokerService>(messageBroker);
        MainInstaller.Bind<ITickService>(tickService);
        MainInstaller.Bind<IDataService>(dataService);
    }
    
    void OnDestroy()
    {
        // Clean up on shutdown
        MainInstaller.CleanDispose<ITickService>();
        MainInstaller.Clean();
    }
}
```

### 2. Use Services Anywhere

```csharp
using GameLovers.Services;

public class PlayerController
{
    public PlayerController()
    {
        // Resolve services
        var messageBroker = MainInstaller.Resolve<IMessageBrokerService>();
        
        // Subscribe to events
        messageBroker.Subscribe<PlayerDamagedMessage>(OnPlayerDamaged);
    }
    
    private void OnPlayerDamaged(PlayerDamagedMessage message)
    {
        // Handle event
    }
}

// Define messages as structs implementing IMessage
public struct PlayerDamagedMessage : IMessage
{
    public int PlayerId;
    public float Damage;
}
```

---

## Services Documentation

### Main Installer

Lightweight service locator for managing dependencies globally.

**Key Points:**
- Only **interfaces** can be bound (throws if you try to bind a concrete type)
- Binding is **instance-based** - you provide the instance, not the type
- `MainInstaller` is a static class wrapping a single `Installer`

```csharp
// Bind services (interfaces only)
MainInstaller.Bind<IMessageBrokerService>(new MessageBrokerService());
MainInstaller.Bind<IDataService>(new DataService());

// Resolve services
var messageBroker = MainInstaller.Resolve<IMessageBrokerService>();

// Safe resolve (doesn't throw)
if (MainInstaller.TryResolve<IDataService>(out var dataService))
{
    dataService.SaveData();
}

// Clean up
MainInstaller.Clean<IMessageBrokerService>(); // Remove single binding
MainInstaller.CleanDispose<ITickService>();   // Dispose + remove
MainInstaller.Clean();                         // Clear all bindings
```

---

### Message Broker Service

Decoupled pub/sub communication between game systems.

**Key Points:**
- Static method subscriptions are **not supported** (uses `action.Target`)
- Use `PublishSafe` when subscribers might subscribe/unsubscribe during handling

```csharp
// Define messages
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

// Use PublishSafe for chain subscriptions
broker.PublishSafe(new EnemyDefeatedMessage { EnemyId = 42 });

// Unsubscribe
broker.Unsubscribe<EnemyDefeatedMessage>(this);    // This subscriber only
broker.Unsubscribe<EnemyDefeatedMessage>();        // All subscribers
broker.UnsubscribeAll(this);                        // All messages for this subscriber
```

---

### Tick Service

Centralized control over Unity's update cycle.

**Key Points:**
- Creates a `DontDestroyOnLoad` GameObject to drive callbacks
- Call `Dispose()` to tear down (tests, game reset)
- Supports buffered ticking with overflow carry for reduced drift

```csharp
public class GameController : ITickable, IDisposable
{
    private readonly ITickService _tickService;
    
    public GameController()
    {
        _tickService = new TickService();
        _tickService.Add(this);              // Update callback
        _tickService.AddFixed(this);         // FixedUpdate callback
        _tickService.Add(this, 0.1f);        // Buffered: every 0.1 seconds
    }
    
    public void OnTick(float deltaTime, double time)
    {
        // Called every frame (or at specified interval)
    }
    
    public void Dispose()
    {
        _tickService.Remove(this);
        _tickService.Dispose();
    }
}
```

---

### Coroutine Service

Run Unity coroutines from pure C# classes without MonoBehaviour.

```csharp
var coroutineService = new CoroutineService();

// Start coroutine with completion callback
coroutineService.StartCoroutine(MyRoutine(), () => Debug.Log("Done!"));

// Delayed execution
coroutineService.StartDelayCall(2f, () => Debug.Log("2 seconds later"));

// Get coroutine reference
var asyncCoroutine = coroutineService.StartCoroutine(LongTask());
if (asyncCoroutine.IsRunning)
{
    coroutineService.StopCoroutine(asyncCoroutine);
}

IEnumerator MyRoutine()
{
    yield return new WaitForSeconds(1f);
    Debug.Log("Coroutine step");
}
```

---

### Pool Service

Efficient object pooling with lifecycle hooks.

```csharp
var poolService = new PoolService();

// Create pools
var bulletPool = new GameObjectPool<Bullet>(bulletPrefab, initialSize: 50);
poolService.AddPool(bulletPool);

// Spawn/Despawn
var bullet = poolService.Spawn<Bullet>();
poolService.Despawn(bullet);

// Spawn with data (implement IPoolEntitySpawn<T>)
var bullet = poolService.Spawn<Bullet, BulletData>(new BulletData { Damage = 100 });

// Direct pool access
var pool = poolService.GetPool<Bullet>();
pool.DespawnAll();
```

**Lifecycle Hooks:**
- `IPoolEntitySpawn` - Called on spawn
- `IPoolEntitySpawn<TData>` - Called on spawn with data
- `IPoolEntityDespawn` - Called on despawn

---

### Data Service

Cross-platform persistent data storage with JSON serialization.

**Key Points:**
- Uses `PlayerPrefs` + `Newtonsoft.Json`
- Keys are `typeof(T).Name` (watch for name collisions)
- `LoadData<T>` requires parameterless constructor if no data exists

```csharp
[Serializable]
public class PlayerData
{
    public string Name;
    public int Level;
}

var dataService = new DataService();

// Save
var player = new PlayerData { Name = "Hero", Level = 10 };
dataService.AddOrReplaceData("player", player);
await dataService.SaveData();

// Load
await dataService.LoadData();
var loaded = dataService.GetData<PlayerData>("player");
```

---

### RNG Service

Deterministic random number generation with state management.

**Key Points:**
- State can be saved/restored for replay or rollback
- Uses `floatP` from DataExtensions for deterministic float math
- Peek methods return next value without advancing state

```csharp
// Create with seed
var rngData = RngService.CreateRngData(seed: 12345);
var rng = new RngService(rngData);

// Generate values
int randomInt = rng.Next;                    // 0 to int.MaxValue
floatP randomFloat = rng.Nextfloat;          // 0 to floatP.MaxValue
int ranged = rng.Range(1, 100);              // 1-99 (exclusive max)
floatP rangedFloat = rng.Range(0f, 1f);      // 0-1 (inclusive max)

// Peek without advancing
int peeked = rng.Peek;                       // Same value on repeated calls

// Save/restore state for determinism
int savedCount = rng.Counter;
// ... generate some values ...
rng.Restore(savedCount);                     // Restore to saved state
```

---

### Version Services

Runtime access to build version and git metadata.

**Key Points:**
- Requires `version-data.txt` in Resources (generated by Editor tools)
- Call `LoadVersionDataAsync()` early in app startup

```csharp
using GameLovers.Services;

// Load version data (call once at startup)
await VersionServices.LoadVersionDataAsync();

// Access version info
string externalVersion = VersionServices.VersionExternal;  // "1.0.0"
string internalVersion = VersionServices.VersionInternal;  // "1.0.0-42.main.abc123"
string branch = VersionServices.Branch;                     // "main"
string commit = VersionServices.Commit;                     // "abc123"
string buildNumber = VersionServices.BuildNumber;           // "42"

// Check if app is outdated
bool outdated = VersionServices.IsOutdatedVersion("1.1.0");
```

---

### Time Service

Unified time access with manipulation support.

```csharp
var timeService = new TimeService();

// Get current times
float unityTime = timeService.UnityTime;        // Time.time equivalent
long unixTime = timeService.UnixTime;           // Unix timestamp
DateTime dateTime = timeService.DateTime;       // DateTime.UtcNow

// Conversions
long unix = timeService.DateTimeToUnix(DateTime.UtcNow);
DateTime dt = timeService.UnixToDateTime(unix);
```

---

### Command Service

Decoupled command execution layer with message broker integration.

```csharp
// Define commands
public struct MovePlayerCommand : ICommand
{
    public int PlayerId;
    public Vector3 Direction;
}

var commandService = new CommandService(messageBroker);

// Execute commands
await commandService.ExecuteCommand(new MovePlayerCommand 
{
    PlayerId = 1,
    Direction = Vector3.forward
});

// Fire and forget
commandService.ExecuteCommand(new MovePlayerCommand { PlayerId = 2 });
```

---

## Contributing

We welcome contributions! Here's how you can help:

### Reporting Issues

- Use the [GitHub Issues](https://github.com/CoderGamester/com.gamelovers.services/issues) page
- Include Unity version, package version, and reproduction steps
- Attach relevant code samples, error logs, or screenshots

### Development Setup

1. Fork the repository on GitHub
2. Clone your fork: `git clone https://github.com/yourusername/com.gamelovers.services.git`
3. Create a feature branch: `git checkout -b feature/amazing-feature`
4. Make your changes with tests
5. Commit: `git commit -m 'Add amazing feature'`
6. Push: `git push origin feature/amazing-feature`
7. Create a Pull Request

### Code Guidelines

- Follow C# 9.0 syntax with explicit namespaces (no global usings)
- Add XML documentation to all public APIs
- Include unit tests for new features
- Runtime code must not reference `UnityEditor`
- Update CHANGELOG.md for notable changes

---

## Support

- **Issues**: [Report bugs or request features](https://github.com/CoderGamester/com.gamelovers.services/issues)
- **Discussions**: [Ask questions and share ideas](https://github.com/CoderGamester/com.gamelovers.services/discussions)
- **Changelog**: See [CHANGELOG.md](CHANGELOG.md) for version history

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.

---

**Made with ❤️ for the Unity community**

*If this package helps your project, please consider giving it a ⭐ on GitHub!*
