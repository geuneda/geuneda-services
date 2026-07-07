# Tick Service

[← Back to index](README.md)

Centralized control over Unity's update cycle.

**Key Points:**
- Creates a `DontDestroyOnLoad` GameObject with `TickServiceMonoBehaviour` to drive callbacks
- Subscribe with an `Action<float>` (receives elapsed `deltaTime`)
- `deltaTime > 0` enables rate-limited (buffered) ticking; `timeOverflowToNextTick` carries overflow to reduce drift
- `realTime=true` uses `Time.realtimeSinceStartup`; `false` (default) uses `Time.time`
- Does **not** enforce a singleton — constructing multiple instances creates multiple host GameObjects
- Call `Dispose()` to tear down the host GameObject (tests, game reset, domain reload)

```csharp
public class GameController : IDisposable
{
    private readonly ITickService _tickService;

    public GameController()
    {
        _tickService = new TickService();

        // Subscribe to every frame Update
        _tickService.SubscribeOnUpdate(OnUpdate);

        // Subscribe to Update, throttled to run at most every 0.1 seconds
        _tickService.SubscribeOnUpdate(OnUpdateBuffered, deltaTime: 0.1f);

        // Rate-limited with overflow carry (reduces drift)
        _tickService.SubscribeOnUpdate(OnThrottled, deltaTime: 0.1f, timeOverflowToNextTick: true);

        // Real-time version (not affected by Time.timeScale)
        _tickService.SubscribeOnUpdate(OnRealTime, realTime: true);

        // Subscribe to FixedUpdate
        _tickService.SubscribeOnFixedUpdate(OnFixedUpdate);

        // Subscribe to LateUpdate
        _tickService.SubscribeOnLateUpdate(OnLateUpdate);
    }

    private void OnUpdate(float deltaTime) { }
    private void OnUpdateBuffered(float deltaTime) { }
    private void OnThrottled(float deltaTime) { }
    private void OnRealTime(float deltaTime) { }
    private void OnFixedUpdate(float deltaTime) { }
    private void OnLateUpdate(float deltaTime) { }

    public void Dispose()
    {
        // Remove a single callback by reference (across all lists)
        _tickService.Unsubscribe(OnUpdate);

        // Type-specific removal
        _tickService.UnsubscribeOnUpdate(OnUpdateBuffered);
        _tickService.UnsubscribeOnFixedUpdate(OnFixedUpdate);
        _tickService.UnsubscribeOnLateUpdate(OnLateUpdate);

        // Bulk clear by subscriber object
        _tickService.UnsubscribeAll(this);

        // Bulk clear all lists entirely
        _tickService.UnsubscribeAllOnUpdate();
        _tickService.UnsubscribeAllOnFixedUpdate();
        _tickService.UnsubscribeAllOnLateUpdate();
        _tickService.UnsubscribeAll();

        _tickService.Dispose(); // destroys host GameObject
    }
}
```
