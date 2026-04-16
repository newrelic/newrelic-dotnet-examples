# New Relic .NET Agent API — Logs in Context Example

This example demonstrates how to manually inject New Relic trace correlation metadata into log entries using `GetLinkingMetadata()` and `ITraceMetadata`. It shows how these values change as execution crosses `[Trace]` span boundaries, and how to enrich structured logs with the fields New Relic needs to correlate log entries with distributed traces.

## API Methods Covered

| Method | Documentation |
|--------|---------------|
| `IAgent.GetLinkingMetadata()` | [GetLinkingMetadata](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#GetLinkingMetadata) |
| `IAgent.TraceMetadata` | [ITraceMetadata](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#ITraceMetadata) |
| `[Trace]` attribute | [Custom instrumentation via attributes](https://docs.newrelic.com/docs/apm/agents/net-agent/custom-instrumentation/custom-instrumentation-attributes-net/) |

## Key Concepts Demonstrated

### `GetLinkingMetadata()`

Returns a `Dictionary<string, string>` containing all fields needed to correlate a log entry with a New Relic entity and distributed trace. The dictionary only includes items with meaningful values — for example, `trace.id` is omitted when distributed tracing is disabled.
([docs](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#GetLinkingMetadata))

| Key | Description | Example Value |
|-----|-------------|---------------|
| `entity.guid` | Unique identifier for this service entity in New Relic | `"MXxBUE18..."` |
| `entity.name` | Application name (matches `NEW_RELIC_APP_NAME`) | `"api-logs-in-context-example"` |
| `hostname` | Host machine name | `"my-machine"` |
| `trace.id` | Distributed trace ID; constant across all spans in one transaction | `"abc123def456..."` |
| `span.id` | Current span ID; changes at each `[Trace]` method boundary | `"789ghi..."` |

([Key names sourced from](https://docs.newrelic.com/docs/logs/logs-context/net-configure-logs-context-all/))

All values are empty strings outside a transaction. The call never throws.

### `ITraceMetadata`

Provides targeted access to just the trace correlation fields. Use this when you only need `TraceId`, `SpanId`, and `IsSampled` — for example, to add trace IDs to an HTTP response header, or when you do not need the full entity context from `GetLinkingMetadata()`.
([docs](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#ITraceMetadata))

| Property | Type | Description |
|----------|------|-------------|
| `TraceId` | `string` | Distributed trace ID; empty string outside a transaction |
| `SpanId` | `string` | Current span ID; empty string outside a transaction |
| `IsSampled` | `bool` | Whether the transaction is sampled for distributed tracing |

### How SpanId Changes Across `[Trace]` Boundaries

A distributed trace is a tree of **spans**, where each span represents one unit of work. The `TraceId` identifies the tree; the `SpanId` identifies the specific node currently executing. When the agent enters a `[Trace]`-decorated method, it creates a new child span with its own ID and pushes it onto the active span stack. While that method is executing, `SpanId` returns the child's ID. When the method returns, the child span closes and `SpanId` reverts to the parent's ID.

The `POST /orders` endpoint produces this sequence:

1. **Root span** (`ProcessOrder`): the agent creates the root span for the ASP.NET Core request. SpanId = `"aaa..."`, TraceId = `"xyz..."`
2. **`[Trace]` entered** (`ValidateOrder`): the agent opens a child span. SpanId = `"bbb..."`, TraceId = `"xyz..."` — new span, same trace
3. **`[Trace]` returned** (back in `ProcessOrder`): the child span closes. SpanId = `"aaa..."` again
4. **`[Trace]` entered** (`FulfillOrder`): another child span opens. SpanId = `"ccc..."`, TraceId = `"xyz..."`
5. **`[Trace]` returned** (back in `ProcessOrder`): SpanId = `"aaa..."` again

This is why `GetLinkingMetadata()` must be called **at the moment a log entry is written** rather than cached at request start. If you cached the SpanId at the beginning of `ProcessOrder` and reused it in `ValidateOrder`, every log entry would carry the root span's ID — the log entries from inside `ValidateOrder` would not be associated with the span that actually produced them.

### Outside a Transaction

`GetLinkingMetadata()` and `ITraceMetadata` are safe to call at any time. The startup log emitted before the first request shows all values as empty strings. This means log entries produced outside a transaction will not correlate to any distributed trace in New Relic — but no exception is thrown.

### The `NewRelicLogEnricher` Service

`NewRelicLogEnricher` is a thin wrapper around `GetLinkingMetadata()` and `ITraceMetadata` registered as a DI service. It represents the integration point you would build in your own application to feed New Relic trace context into your logging pipeline.

In this example it is injected into `OrderService` so that each method can read the current trace and span IDs at the moment a log entry is written. In a production application you would wire this into your logging infrastructure instead — for example:

- A middleware that adds `trace.id` and `span.id` to the `ILogger` scope for every request, so all log entries automatically carry the correlation fields without each class needing to call the API directly.
- A custom log formatter or log enricher (e.g., a Serilog enricher) that calls `GetLinkingMetadata()` and appends the returned fields to every structured log event.

The key point is that `GetLinkingMetadata()` and `ITraceMetadata` must be called **at the moment the log entry is written**, not cached ahead of time — because `span.id` changes as execution moves through `[Trace]` boundaries within a request.

### When `[Transaction]` Is NOT Needed

The service methods in this example do NOT use the `[Transaction]` attribute. The agent's built-in ASP.NET Core instrumentation creates a transaction for each incoming HTTP request automatically. The `[Transaction]` attribute is only needed when auto-instrumentation does not apply (e.g., in a `BackgroundService`).

## NuGet Packages

This example references two NuGet packages:

- **`NewRelic.Agent`** — Bundles the agent runtime files (profiler, configuration, extensions) into the build output directory. This is one of several ways to install the agent; alternatives include OS package managers and container-based installation.
- **`NewRelic.Agent.Api`** — Provides the `NewRelic.Api.Agent` namespace used throughout this example (`GetLinkingMetadata()`, `ITraceMetadata`, `[Trace]`, etc.). Required for any application that calls the agent API directly.

## How to Run

### Prerequisites

- .NET 10 SDK
- A New Relic account with a license key

### Run the Application

This example uses the `NewRelic.Agent` NuGet package, which places the agent files under the build output directory. You must set several [environment variables](https://docs.newrelic.com/docs/apm/agents/net-agent/other-installation/understanding-net-agent-environment-variables/) to enable the .NET CLR profiler and point it to the agent.

**Windows (PowerShell):**

```powershell
# Build first so the agent files are in the output directory
dotnet build -c Release

# Set the required environment variables for the .NET agent
$env:CORECLR_ENABLE_PROFILING=1
$env:CORECLR_PROFILER="{36032161-FFC0-4B61-B559-F6C5D41BAE5A}"
$env:CORECLR_PROFILER_PATH="$PWD\bin\Release\net10.0\newrelic\NewRelic.Profiler.dll"
$env:CORECLR_NEWRELIC_HOME="$PWD\bin\Release\net10.0\newrelic"
$env:NEW_RELIC_LICENSE_KEY="<your-new-relic-license-key>"
$env:NEW_RELIC_APP_NAME="api-logs-in-context-example"

# Run the application
dotnet run -c Release --no-build
```

**Linux / macOS (bash):**

```bash
# Build first so the agent files are in the output directory
dotnet build -c Release

# Set the required environment variables for the .NET agent
export CORECLR_ENABLE_PROFILING=1
export CORECLR_PROFILER="{36032161-FFC0-4B61-B559-F6C5D41BAE5A}"
export CORECLR_PROFILER_PATH="$PWD/bin/Release/net10.0/newrelic/libNewRelicProfiler.so"
export CORECLR_NEWRELIC_HOME="$PWD/bin/Release/net10.0/newrelic"
export NEW_RELIC_LICENSE_KEY="<your-new-relic-license-key>"
export NEW_RELIC_APP_NAME="api-logs-in-context-example"

# Run the application
dotnet run -c Release --no-build
```

| Variable | Purpose |
|----------|---------|
| `CORECLR_ENABLE_PROFILING` | Enables the .NET CLR profiler (must be `1`) |
| `CORECLR_PROFILER` | The CLSID of the New Relic profiler |
| `CORECLR_PROFILER_PATH` | Path to the profiler native library (`NewRelic.Profiler.dll` on Windows, `libNewRelicProfiler.so` on Linux) |
| `CORECLR_NEWRELIC_HOME` | Path to the directory containing the agent configuration and extensions |
| `NEW_RELIC_LICENSE_KEY` | Your New Relic license key |
| `NEW_RELIC_APP_NAME` | The application name as it will appear in New Relic |

## Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/orders` | POST | Processes an order through `ValidateOrder` → `FulfillOrder` stages, logging `trace.id` and `span.id` at each step. |
| `/metadata` | GET | Returns the raw output of `GetLinkingMetadata()` and `ITraceMetadata` as JSON. |

### Example Requests

**Linux / macOS (bash):**

```bash
# Process an order — observe trace.id and span.id in the application logs (200 OK)
curl -X POST http://localhost:5002/orders \
  -H "Content-Type: application/json" \
  -d '{"orderId": "ORD-001", "customerId": "CUST-100"}'

# Inspect the raw linking metadata and trace metadata for the current request (200 OK)
curl http://localhost:5002/metadata
```

**Windows (PowerShell):**

```powershell
# Process an order — observe trace.id and span.id in the application logs (200 OK)
Invoke-RestMethod -Method POST -Uri http://localhost:5002/orders `
  -ContentType "application/json" `
  -Body '{"orderId": "ORD-001", "customerId": "CUST-100"}'

# Inspect the raw linking metadata and trace metadata for the current request (200 OK)
Invoke-RestMethod -Uri http://localhost:5002/metadata
```

## What to Look For

### Application logs

Send a `POST /orders` request and watch the application's console output. Each log line produced by `OrderService` is a separate log entry written at a different point in the call stack. Look for these patterns:

- The `ProcessOrder` entry at the start and end carries the same `span.id` — the root span of the request.
- The `ValidateOrder` entry carries a **different** `span.id` — the agent opened a child span when it entered the `[Trace]`-decorated method.
- The `FulfillOrder` entry carries yet another **different** `span.id`.
- Every entry carries the **same** `trace.id` — all spans belong to the same distributed trace.
- The startup log (emitted before any request) shows **empty strings** for `trace.id` and `span.id` — no transaction was active.

Compare the `GET /metadata` response with the log output from a concurrent `POST /orders` request to see both APIs side by side.

### New Relic Logs

This example uses `Microsoft.Extensions.Logging`, which is supported by the agent's automatic log forwarding (agent 9.7.0+). When log forwarding is enabled, the agent automatically appends `span.id`, `trace.id`, `hostname`, `entity.guid`, and `entity.name` to every forwarded log entry and sends them to New Relic.
([Automatic logs in context for .NET](https://docs.newrelic.com/docs/logs/logs-context/net-configure-logs-context-all/))

In the New Relic Logs UI, filter by the `trace.id` value from a `POST /orders` response to see all log entries from that single request grouped together — including entries from `ValidateOrder` and `FulfillOrder` with their distinct `span.id` values.
