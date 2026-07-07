# Service Locator — MainInstaller / Installer

[← Back to index](README.md)

Lightweight service locator for managing dependencies globally.

**Key Points:**
- Only **interfaces** can be bound (throws `ArgumentException` if you try to bind a concrete type)
- Binding is **instance-based** — you provide the instance, not the type
- `MainInstaller` is a static class exposing single-interface `Bind<T>`; use `Installer` directly for **multi-interface binding**
- Re-binding the same interface throws (`Dictionary.Add` — no overwrite semantics)

```csharp
// Bind a single interface
MainInstaller.Bind<IMessageBrokerService>(new MessageBrokerService());
MainInstaller.Bind<IDataService>(new DataService());

// Resolve
var messageBroker = MainInstaller.Resolve<IMessageBrokerService>();

// Safe resolve (returns false instead of throwing)
if (MainInstaller.TryResolve<IDataService>(out var dataService))
{
    dataService.SaveAllData();
}

// Clean up
MainInstaller.Clean<IMessageBrokerService>();   // Remove single binding
MainInstaller.CleanDispose<ITickService>();      // Dispose + remove
MainInstaller.Clean();                           // Clear all bindings

// Multi-interface binding — use Installer directly
var installer = new Installer();
var timeService = new TimeService();
installer.Bind<TimeService, ITimeService, ITimeManipulator>(timeService);
// Three-interface overload also exists:
// installer.Bind<T, T1, T2, T3>(instance)

// Bind calls are chainable
installer.Bind<IMessageBrokerService>(new MessageBrokerService())
         .Bind<ITickService>(new TickService());
```

## Error Reference

| Call | Exception | Condition |
|------|-----------|-----------|
| `Bind<T>(instance)` | `ArgumentException` | `T` is not an interface |
| `Bind<T>(instance)` duplicate | `ArgumentException` | `T` already bound |
| `Resolve<T>()` | `KeyNotFoundException` | `T` not bound |
