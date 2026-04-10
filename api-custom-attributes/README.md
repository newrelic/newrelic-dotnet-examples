# New Relic .NET Agent API — Custom Attributes Example

This example demonstrates how to add custom attributes to transactions and spans, set transaction names, associate a user ID with a transaction, and use the `[Trace]` attribute to create child spans within a transaction.

## API Methods Covered

| Method | Documentation |
|--------|---------------|
| `ITransaction.AddCustomAttribute(string, object)` | [AddCustomAttribute](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#ITransaction.AddCustomAttribute) |
| `ISpan.AddCustomAttribute(string, object)` | [AddCustomAttribute](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#ISpan.AddCustomAttribute) |
| `ISpan.SetName(string)` | [SetName](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#SetName) |
| `ITransaction.SetUserId(string)` | [SetUserId](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#SetUserId) |
| `NewRelic.SetTransactionName(string, string)` | [SetTransactionName](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#SetTransactionName) |
| `[Trace]` attribute | [Custom instrumentation via attributes](https://docs.newrelic.com/docs/apm/agents/net-agent/custom-instrumentation/custom-instrumentation-attributes-net/) |

## Key Concepts Demonstrated

### Transaction vs. Span Attributes

Custom attributes can be added at two levels:

- **Transaction attributes** (`ITransaction.AddCustomAttribute`): Attached to the entire transaction. Reported in errors and transaction traces.
- **Span attributes** (`ISpan.AddCustomAttribute`): Attached to the currently executing span. Useful for adding context to specific operations within a transaction.

Both methods return their respective interface (`ITransaction` / `ISpan`) to support method chaining (builder pattern). Keys are limited to 255-bytes.

Supported value types ([full details](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#ITransaction.AddCustomAttribute)):

| .NET Type | Representation |
|-----------|---------------|
| `byte`, `Int16`, `Int32`, `Int64`, `sbyte`, `UInt16`, `UInt32`, `UInt64` | Integral value |
| `float`, `double`, `decimal` | Decimal-based number |
| `string` | Truncated at 4096 characters at ingest; empty strings supported |
| `bool` | True or false |
| `DateTime` | ISO-8601 string with timezone (e.g., `2020-02-13T11:31:19.5767650-08:00`) |
| `TimeSpan` | Decimal number representing seconds |
| `arrays`, `Lists`, and other `IEnumerable` types | JSON array (null elements filtered; requires agent 10.50.0+) |
| Everything else | `ToString()` is called |

### The `[Trace]` Attribute

The `[Trace]` attribute creates a child span within an existing transaction. When invoked outside a transaction, no measurements are recorded.

- Apply `[Trace]` to concrete method implementations only — it cannot be applied to interfaces or base class definitions.
- Use `[MethodImpl(MethodImplOptions.NoInlining)]` alongside `[Trace]` to prevent the JIT compiler from inlining the method, which would prevent instrumentation.
- `[Trace]` spans nest naturally: `OrderPipeline.CalculatePrice` calls `PricingService.GetPrice`, both decorated with `[Trace]`, creating a parent-child relationship visible in the trace waterfall.

### `SetTransactionName`

Overrides the auto-generated transaction name. Must be called inside a transaction. Important constraints:

- If called multiple times in the same transaction, the last call wins.
- Do not use unique values (URLs, session IDs, hex values) in transaction names — use `AddCustomAttribute` instead.
- Do not create more than 1000 unique transaction names.
- Do not use brackets `[suffix]` at the end of the name.

### `SetUserId`

Associates a user ID with the current transaction. The value is stored as the agent attribute `enduser.id` and is surfaced in [Errors Inbox](https://docs.newrelic.com/docs/errors-inbox/errors-inbox/), where it powers the ["Users impacted"](https://docs.newrelic.com/docs/errors-inbox/error-impacted/#set-users) metric — showing how many unique users are affected by each error group.

Requires agent version 10.9.0+. Null, empty, and whitespace values are ignored.

### When `[Transaction]` Is NOT Needed

The web API endpoint in this example does NOT use the `[Transaction]` attribute. The agent's built-in ASP.NET Core instrumentation automatically creates a transaction for each incoming HTTP request. The `[Transaction]` attribute is only needed when auto-instrumentation does not apply (e.g., in a `BackgroundService`).

## NuGet Packages

This example references two NuGet packages:

- **`NewRelic.Agent`** — Bundles the agent runtime files (profiler, configuration, extensions) into the build output directory. This is one of several ways to install the agent; alternatives include OS package managers and container-based installation.
- **`NewRelic.Agent.Api`** — Provides the `NewRelic.Api.Agent` namespace used throughout this example (`AddCustomAttribute`, `SetName`, `SetUserId`, `[Trace]`, etc.). This package is required for any application that calls the agent API directly.

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
$env:NEW_RELIC_APP_NAME="api-custom-attributes-example"

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
export NEW_RELIC_APP_NAME="api-custom-attributes-example"

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
| `/orders/{customerId}` | POST | Processes an order through Validate → CalculatePrice → PersistOrder stages, each creating a traced child span with custom attributes. |

### Example Requests

**Linux / macOS (bash):**

```bash
# Successful order (200 OK)
curl -X POST http://localhost:5001/orders/CUST-100 \
  -H "Content-Type: application/json" \
  -d '{"orderId": "ORD-001", "region": "US", "items": [{"productId": "PROD-1", "name": "Widget", "price": 25.00, "quantity": 2}, {"productId": "PROD-2", "name": "Gadget", "price": 15.00, "quantity": 1}]}'

# Order from a different region (applies discount)
curl -X POST http://localhost:5001/orders/CUST-200 \
  -H "Content-Type: application/json" \
  -d '{"orderId": "ORD-002", "region": "EU", "items": [{"productId": "PROD-3", "name": "Sprocket", "price": 50.00, "quantity": 3}]}'

# Trigger a validation error (empty items — returns 400)
# Useful for observing how SetUserId and custom attributes appear on error events
curl -X POST http://localhost:5001/orders/CUST-300 \
  -H "Content-Type: application/json" \
  -d '{"orderId": "ORD-003", "region": "US", "items": []}'
```

**Windows (PowerShell):**

```powershell
# Successful order (200 OK)
Invoke-RestMethod -Method POST -Uri http://localhost:5001/orders/CUST-100 `
  -ContentType "application/json" `
  -Body '{"orderId": "ORD-001", "region": "US", "items": [{"productId": "PROD-1", "name": "Widget", "price": 25.00, "quantity": 2}, {"productId": "PROD-2", "name": "Gadget", "price": 15.00, "quantity": 1}]}'

# Order from a different region (applies discount)
Invoke-RestMethod -Method POST -Uri http://localhost:5001/orders/CUST-200 `
  -ContentType "application/json" `
  -Body '{"orderId": "ORD-002", "region": "EU", "items": [{"productId": "PROD-3", "name": "Sprocket", "price": 50.00, "quantity": 3}]}'

# Trigger a validation error (empty items — returns 400)
# Useful for observing how SetUserId and custom attributes appear on error events
Invoke-RestMethod -Method POST -Uri http://localhost:5001/orders/CUST-300 `
  -ContentType "application/json" `
  -Body '{"orderId": "ORD-003", "region": "US", "items": []}'
```
