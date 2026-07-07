# Data Service

[← Back to index](README.md)

Cross-platform persistent data storage with JSON serialization.

**Key Points:**
- Data is keyed by **type** (`typeof(T)`) — no string keys at the API level
- Only **reference types** (`class`) are supported; value types (`struct`) are not
- Disk keys in `PlayerPrefs` use `typeof(T).Name` — watch for name collisions across assemblies with types sharing the same short name
- `GetData<T>()` throws `KeyNotFoundException` if the type has not been loaded or added — use `HasData<T>()` to guard
- `LoadData<T>` calls `Activator.CreateInstance<T>()` when no saved data exists; `T` must have a **parameterless constructor**

```csharp
[Serializable]
public class PlayerData
{
    public string Name;
    public int Level;
    public PlayerData() { }  // required for LoadData<T> when no saved data exists
}

var dataService = new DataService();

// Load from disk (or create fresh if not saved yet)
PlayerData player = dataService.LoadData<PlayerData>();

// Modify in memory
player.Name = "Hero";
player.Level = 10;

// Save one type to disk
dataService.SaveData<PlayerData>();

// Save all loaded types to disk
dataService.SaveAllData();

// Add or replace in memory without saving to disk
dataService.AddOrReplaceData(new PlayerData { Name = "Alt", Level = 5 });

// Read back from memory
PlayerData loaded = dataService.GetData<PlayerData>();

// Guard against missing data
if (dataService.HasData<PlayerData>())
{
    var data = dataService.GetData<PlayerData>();
}
```

## IDataProvider vs IDataService

| Interface | Methods | Use for |
|-----------|---------|---------|
| `IDataProvider` | `GetData<T>()`, `HasData<T>()` | Read-only consumers |
| `IDataService : IDataProvider` | adds `AddOrReplaceData`, `LoadData`, `SaveData`, `SaveAllData` | Full read-write access |

Bind `IDataService` for systems that need to save; bind `IDataProvider` for read-only consumers to enforce discipline.

## Error Reference

| Call | Exception | Condition |
|------|-----------|-----------|
| `GetData<T>()` | `KeyNotFoundException` | `T` not loaded or added |
| `LoadData<T>()` | `MissingMethodException` | `T` has no parameterless constructor |
