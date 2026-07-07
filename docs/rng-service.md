# RNG Service

[← Back to index](README.md)

Deterministic random number generation with state management.

**Key Points:**
- State can be saved and restored for replay or rollback (seed + counter)
- Uses `floatP` from `com.geuneda.gamedata` for deterministic float math
- `Peek`/`PeekRange` return the next value without advancing the counter
- `Range(int, int)` — max is **exclusive**; `Range(floatP, floatP)` — max is **inclusive**

```csharp
// Create with seed
RngData rngData = RngService.CreateRngData(seed: 12345);
var rng = new RngService(rngData);

// Generate values
int randomInt      = rng.Next;                          // 0 to int.MaxValue
floatP randomFloat = rng.Nextfloat;                     // 0 to floatP.MaxValue
int ranged         = rng.Range(1, 100);                 // 1–99 (max exclusive)
floatP rangedFloat = rng.Range((floatP)0f, (floatP)1f); // 0–1 (max inclusive)

// Peek without advancing state
int peeked      = rng.Peek;
int peekedRange = rng.PeekRange(1, 100);

// Save and restore state for determinism / rollback
int savedCount = rng.Counter;
// ... generate some values ...
rng.Restore(savedCount); // rewind to saved state

// Inspect state
IRngData state = rng.Data;
```
