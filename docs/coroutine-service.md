# Coroutine Service

[← Back to index](README.md)

Run Unity coroutines from pure C# classes without MonoBehaviour.

**Key Points:**
- Creates a `DontDestroyOnLoad` GameObject with `CoroutineServiceMonoBehaviour`
- `StartCoroutine` returns a plain Unity `Coroutine` handle (no callbacks)
- `StartAsyncCoroutine` returns `IAsyncCoroutine` with `OnComplete` callback, `IsRunning`, `IsCompleted`, `StopCoroutine(bool)`
- `StartDelayCall(action, delay)` — argument order: **action first, delay second**
- **Known limitation**: `StopCoroutine(triggerOnComplete: false)` always triggers callbacks regardless of the flag
- Does **not** enforce a singleton — call `Dispose()` to tear down the host GameObject

```csharp
var cs = new CoroutineService();

// Plain coroutine — returns Unity Coroutine handle
Coroutine handle = cs.StartCoroutine(MyRoutine());
cs.StopCoroutine(handle);

// Async coroutine — returns IAsyncCoroutine with callback and state
IAsyncCoroutine asyncHandle = cs.StartAsyncCoroutine(MyRoutine());
asyncHandle.OnComplete(() => Debug.Log("Done!"));

if (asyncHandle.IsRunning) { /* still running */ }
if (asyncHandle.IsCompleted) { /* finished naturally */ }

// Stop (note: triggerOnComplete flag not currently respected — callbacks always fire)
asyncHandle.StopCoroutine(triggerOnComplete: false);

// Async coroutine with typed result data
IAsyncCoroutine<int> typedHandle = cs.StartAsyncCoroutine(MyRoutine(), data: 42);
typedHandle.OnComplete(result => Debug.Log($"Finished with: {result}"));

// Delayed call — action fires after delay seconds
cs.StartDelayCall(() => Debug.Log("2 seconds later"), delay: 2f);

// Delayed call with typed data
cs.StartDelayCall<string>(msg => Debug.Log(msg), data: "Hello", delay: 1f);

// Stop all and tear down
cs.StopAllCoroutines();
cs.Dispose(); // destroys host GameObject

IEnumerator MyRoutine()
{
    yield return new WaitForSeconds(1f);
    Debug.Log("Step");
}
```
