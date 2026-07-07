# Version Services

[← Back to index](README.md)

Runtime access to build version and git metadata.

**Key Points:**
- Requires `version-data.txt` TextAsset in `Resources/` (resource name: `VersionServices.VersionDataFilename`)
- `VersionEditorUtils` writes `Assets/Configs/Resources/version-data.txt` on editor load and before builds; it uses git CLI
- `VersionExternal` is always safe (reads `Application.version` directly, no load required)
- **Auto-bootstrap**: version metadata loads automatically at `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`, before any scene `Awake` and before vendor-SDK `SubsystemRegistration` callbacks that read it (e.g. Sentry's Option Config Script). Consumers do **not** need to call `LoadVersionData()` / `LoadVersionDataAsync()` explicitly for the default flow.
- **Lazy-load fallback**: `VersionInternal` / `Branch` / `Commit` / `BuildNumber` getters trigger `LoadVersionData()` on first access if the auto-bootstrap hook has not yet fired. Covers the case where a sibling SDK's `SubsystemRegistration` callback fires before this package's (assembly ordering between `[RuntimeInitializeOnLoadMethod]` callbacks at the same phase is undefined).
- **Graceful degradation**: if `version-data.txt` is missing from `Resources/`, `VersionInternal` falls back to `Application.version` and the other accessors return `string.Empty`. No exception is raised.
- `LoadVersionData()` (sync) and `LoadVersionDataAsync()` (async) remain available for callers that want to pre-warm explicitly. Both are idempotent and share the same parse/assign pipeline; pick async only if you've extended `VersionData` with large blobs that would noticeably stall the main thread.

```csharp
using Geuneda.Services;

// No setup call needed — auto-bootstrap fires at SubsystemRegistration.
// Optional pre-warm if you want to control timing explicitly:
// VersionServices.LoadVersionData();
// await VersionServices.LoadVersionDataAsync();

// Safe at any time — reads Application.version directly.
string externalVersion = VersionServices.VersionExternal;   // "1.0.1"

// Auto-loaded; lazy-loads on first access if SubsystemRegistration race occurred.
string internalVersion = VersionServices.VersionInternal;   // "1.0.1-42.main.abc123"
string branch          = VersionServices.Branch;            // "main"
string commit          = VersionServices.Commit;            // "abc123"
string buildNumber     = VersionServices.BuildNumber;       // "42"

// Check if app is outdated against a remote version string
bool outdated = VersionServices.IsOutdatedVersion("1.2.0");
```

## Error Reference

| Call | Behaviour | Condition |
|------|-----------|-----------|
| `VersionInternal` | Falls back to `Application.version` | `version-data.txt` missing from `Resources/` or fails to parse |
| `Branch`, `Commit`, `BuildNumber` | Return `string.Empty` | `version-data.txt` missing from `Resources/` or fails to parse |

A `Debug.LogError` is emitted when load fails. None of the accessors throw.
