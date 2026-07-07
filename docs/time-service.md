# Time Service

[← Back to index](README.md)

Unified time access with manipulation support.

**Key Points:**
- Bind as `ITimeManipulator` if you need to add offset or sync with server time; bind as `ITimeService` for read-only consumers
- All time getters account for any offset applied via `AddTime` or `SetInitialTime`
- `UnityTimeNow` uses `Time.realtimeSinceStartup` + offset; `UnityScaleTimeNow` uses `Time.time` + offset

```csharp
var timeService = new TimeService();

// Query current times
DateTime utcNow  = timeService.DateTimeUtcNow;    // current UTC datetime with offset
float unityTime  = timeService.UnityTimeNow;      // Time.realtimeSinceStartup + offset
float scaledTime = timeService.UnityScaleTimeNow; // Time.time + offset
long unixMs      = timeService.UnixTimeNow;       // Unix time in milliseconds

// Conversions
long unix    = timeService.UnixTimeFromDateTimeUtc(DateTime.UtcNow);
DateTime dt  = timeService.DateTimeUtcFromUnixTime(unix);
float unityT = timeService.UnityTimeFromUnixTime(unix);

// Manipulation (ITimeManipulator only)
timeService.AddTime(3600f);                     // fast-forward 1 hour
timeService.SetInitialTime(serverUtcNow);       // sync with server time
```

## ITimeService vs ITimeManipulator

| Interface | Methods | Use for |
|-----------|---------|---------|
| `ITimeService` | All getters + conversion methods | Read-only consumers |
| `ITimeManipulator : ITimeService` | adds `AddTime(float)`, `SetInitialTime(DateTime)` | Time server sync, test helpers |
