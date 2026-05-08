# New Relic .NET Agent API — Dependency Injection Example

This example demonstrates the correct way to register and consume `IAgent` through
Microsoft.Extensions.DependencyInjection, and documents the common anti-patterns
that silently break telemetry.

## The short version

```csharp
using NRAgent = NewRelic.Api.Agent.NewRelic;

// ✅ Register IAgent as a singleton with a factory so GetAgent() is deferred
builder.Services.AddSingleton<IAgent>(_ => NRAgent.GetAgent());
```

```csharp
// ✅ Inject IAgent and fetch CurrentTransaction / CurrentSpan FRESH per call
public class MyService
{
    private readonly IAgent _agent;

    public MyService(IAgent agent) => _agent = agent;

    public void DoWork()
    {
        var transaction = _agent.CurrentTransaction;  // request-scoped — local only
        transaction.AddCustomAttribute("key", "value");
    }
}
```

## Why singleton is correct

`IAgent` is a stateless handle to the agent runtime. The per-request state —
`CurrentTransaction`, `CurrentSpan` — is stored in `AsyncLocal` under the hood, so
the same `IAgent` instance correctly returns the current request's transaction to
each caller. Registering `IAgent` as scoped or transient works but wastes
allocations; there is no safety benefit.

## Why the factory (`_ => NRAgent.GetAgent()`) matters

`NewRelic.Api.Agent.NewRelic.GetAgent()` must be called AFTER the profiler has
finished attaching. A factory lambda defers the call until the container first
resolves `IAgent`, which happens on the first request — well after the profiler
is ready. Resolving `GetAgent()` eagerly at registration time risks caching the
no-op placeholder the API returns before the agent is live, silently disabling
all custom instrumentation for the lifetime of the process.

## The namespace alias

`NewRelic.Api.Agent.NewRelic` is both a namespace prefix and a class name. Inside
a file whose root namespace is `NewRelic.*`, the identifier `NewRelic` resolves
to the namespace, not the class, and the compiler reports an error. Aliasing it:

```csharp
using NRAgent = NewRelic.Api.Agent.NewRelic;
```

...gives you an unambiguous name for the static class. This is only needed in
files where the ambiguity exists; in most applications `NewRelic.Api.Agent.NewRelic.GetAgent()`
works as written.

## Anti-patterns — what NOT to do

See [`Services/AntiPatternExamples.cs`](Services/AntiPatternExamples.cs) for
compile-checked counter-examples. Summary:

### ❌ Capturing `CurrentTransaction` in a field

```csharp
public class BuggyService
{
    private readonly ITransaction _transaction;

    public BuggyService(IAgent agent)
    {
        _transaction = agent.CurrentTransaction; // ❌ wrong transaction forever
    }
}
```

For a singleton, the constructor runs during the first request. `_transaction`
then holds that request's transaction permanently. Every subsequent call writes
attributes, events, and errors to a transaction that has already ended — the
data never appears on the transactions the user is actually looking at.

### ❌ Capturing `CurrentSpan` in a field

Same failure mode, one level deeper. A span belongs to a specific frame in a
specific request and cannot be reused.

### ❌ Resolving `GetAgent()` eagerly at registration time

```csharp
// ❌ Eager — GetAgent() runs while the container is being built
builder.Services.AddSingleton<IAgent>(NewRelic.Api.Agent.NewRelic.GetAgent());

// ✅ Lazy — GetAgent() runs on first resolution
builder.Services.AddSingleton<IAgent>(_ => NewRelic.Api.Agent.NewRelic.GetAgent());
```

The eager form can capture the no-op placeholder that `GetAgent()` returns
before the profiler has finished initializing and cache it as the singleton. The
app then runs with a silently disabled API.

## API surface covered

| Type / Method | Documentation |
|---|---|
| `IAgent` | [IAgent](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#IAgent) |
| `NewRelic.GetAgent()` | [GetAgent](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#GetAgent) |
| `IAgent.CurrentTransaction` | [CurrentTransaction](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#CurrentTransaction) |
| `ITransaction.CurrentSpan` | [CurrentSpan](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#CurrentSpan) |

## How to run

### Prerequisites

- .NET 10 SDK
- A New Relic account with a license key

### NuGet packages

- **`NewRelic.Agent`** — bundles the agent runtime (profiler, config, extensions) into the build output.
- **`NewRelic.Agent.Api`** — provides the `IAgent`, `ITransaction`, and `ISpan` types used in this example.

### Build and run

**Windows (PowerShell):**

```powershell
dotnet build -c Release

$env:CORECLR_ENABLE_PROFILING=1
$env:CORECLR_PROFILER="{36032161-FFC0-4B61-B559-F6C5D41BAE5A}"
$env:CORECLR_PROFILER_PATH="$PWD\bin\Release\net10.0\newrelic\NewRelic.Profiler.dll"
$env:CORECLR_NEWRELIC_HOME="$PWD\bin\Release\net10.0\newrelic"
$env:NEW_RELIC_LICENSE_KEY="<your-new-relic-license-key>"
$env:NEW_RELIC_APP_NAME="api-dependency-injection-example"

dotnet run -c Release --no-build
```

**Linux (bash):**

```bash
dotnet build -c Release

export CORECLR_ENABLE_PROFILING=1
export CORECLR_PROFILER="{36032161-FFC0-4B61-B559-F6C5D41BAE5A}"
export CORECLR_PROFILER_PATH="$PWD/bin/Release/net10.0/newrelic/libNewRelicProfiler.so"
export CORECLR_NEWRELIC_HOME="$PWD/bin/Release/net10.0/newrelic"
export NEW_RELIC_LICENSE_KEY="<your-new-relic-license-key>"
export NEW_RELIC_APP_NAME="api-dependency-injection-example"

dotnet run -c Release --no-build
```

### Endpoints

| Endpoint | Description |
|---|---|
| `GET /work/singleton` | Invokes `WorkService` (singleton) — writes an attribute to the current transaction via injected `IAgent`. |
| `GET /work/scoped` | Invokes `ScopedWorkService` (scoped) consuming the same singleton `IAgent`. |

### Example requests

```bash
curl http://localhost:5004/work/singleton
curl http://localhost:5004/work/scoped
```

```powershell
Invoke-RestMethod -Uri http://localhost:5004/work/singleton
Invoke-RestMethod -Uri http://localhost:5004/work/scoped
```

## What to look for in New Relic

Each request appears as its own transaction under the APM app, with its own
custom attributes. Call each endpoint several times and confirm via NRQL that
the `workService.lifetime` attribute varies per request — if it were captured
in a field, every request past the first would show the same (stale) value.

```sql
SELECT workService.lifetime, workService.step
FROM Transaction
WHERE appName = 'api-dependency-injection-example'
SINCE 10 minutes ago
```
