# New Relic .NET Agent API — Error Handling Example

This example demonstrates how to use the New Relic .NET agent's error reporting API to capture, categorize, and group errors.

For a broader tutorial on tracking and triaging errors in New Relic, see [Respond to service outages](https://docs.newrelic.com/docs/tutorial-error-tracking/respond-outages/).

## API Methods Covered

| Method | Documentation |
|--------|---------------|
| `NoticeError(Exception)` | [NoticeError](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#NoticeError) |
| `NoticeError(Exception, IDictionary)` | [NoticeError](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#NoticeError) |
| `NoticeError(string, IDictionary)` | [NoticeError](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#NoticeError) |
| `NoticeError(string, IDictionary, bool)` | [NoticeError](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#NoticeError) |
| `SetErrorGroupCallback(Func<...>)` | [SetErrorGroupCallback](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#SetErrorGroupCallback) |
| `[Transaction]` attribute | [Custom instrumentation via attributes](https://docs.newrelic.com/docs/apm/agents/net-agent/custom-instrumentation/custom-instrumentation-attributes-net/) |

## Key Concepts Demonstrated

### NoticeError Overloads

The `NoticeError` method has multiple overloads:

- **Exception-based** (`NoticeError(Exception)` and `NoticeError(Exception, IDictionary)`): Reports the exception with its stack trace. The agent determines the innermost exception using `Exception.GetBaseException()`.
- **String-based** (`NoticeError(string, IDictionary)`): Creates both error events and error traces from a message string. Only the first 1023 characters are retained in error events; error traces retain the full message.
- **Expected errors** (`NoticeError(string, IDictionary, bool)`): The `isExpected` parameter marks the error so it won't affect Apdex score and error rate. This parameter is only available on the string-based overloads.

### Inside vs. Outside a Transaction

- **Inside a transaction**: The error is reported within the parent transaction.
- **Outside a transaction**: The agent creates an error trace and categorizes it as a `NewRelic.Api.Agent.NoticeError` API call. The error will not contribute to the error rate of the application.

For each transaction, the agent only retains the exception and attributes from the **first call** to `NoticeError()`.

### When to Use the `[Transaction]` Attribute

The agent's built-in ASP.NET Core instrumentation automatically creates a transaction for each incoming HTTP request. Methods called during request handling (like `OrderService.ProcessOrder()`) are already within a transaction and do **not** need the `[Transaction]` attribute.

The `[Transaction]` attribute is needed when the agent's auto-instrumentation does not apply — for example, in a `BackgroundService`. This example includes `StartupHealthCheckService`, a `BackgroundService` whose periodic work method uses `[Transaction]` to ensure the agent creates a transaction.

### Error Group Callback

`SetErrorGroupCallback` provides a callback that the agent uses to determine the error group name for error events and traces. This name is used in the Errors Inbox to group errors into logical groups. The callback receives an `IReadOnlyDictionary<string, object>` containing error attributes. The following attributes should always be present:

- `error.class`
- `error.message`
- `stack_trace`
- `transactionName`
- `request.uri`
- `error.expected`

Requires .NET agent version 10.9.0 or higher.

## How to Run

### Prerequisites

- .NET 10 SDK
- A New Relic account with a license key

### NuGet Packages

This example references two NuGet packages:

- **`NewRelic.Agent`** — Bundles the agent runtime files (profiler, configuration, extensions) into the build output directory. This is one of several ways to install the agent; alternatives include OS package managers and container-based installation.
- **`NewRelic.Agent.Api`** — Provides the `NewRelic.Api.Agent` namespace used throughout this example (`NoticeError`, `SetErrorGroupCallback`, `[Transaction]`, etc.). This package is required for any application that calls the agent API directly.

### Run the Application

This example uses the `NewRelic.Agent` NuGet package, which places the agent files under the build output directory. You must set several environment variables to enable the .NET CLR profiler and point it to the agent.

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
$env:NEW_RELIC_APP_NAME="api-error-handling-example"

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
export NEW_RELIC_APP_NAME="api-error-handling-example"

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
| `/orders` | POST | Validates an order. Reports a string-based error with custom attributes on validation failure. |
| `/orders/risky` | POST | Calls a simulated failing service. Reports an exception-based error with custom attributes. |
| `/orders/flaky` | POST | Simulates a flaky dependency. Reports errors marked as expected (`isExpected: true`). |

The `StartupHealthCheckService` background service runs two alternating periodic checks every 15 seconds to demonstrate the contrast between in-transaction and out-of-transaction error reporting:
- `DoWorkInsideTransaction()` — decorated with `[Transaction]`, so `NoticeError` reports within a transaction
- `DoWorkOutsideTransaction()` — no `[Transaction]`, so `NoticeError` is called outside any transaction and the error will not contribute to the application's error rate. See [Errors outside transactions](https://docs.newrelic.com/docs/errors-inbox/apm-tab/#outside-transactions) for how to find these in the UI.

### Example Requests

**Linux / macOS (bash):**

```bash
# Successful order (200 OK) — verify the app is running
curl -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -d '{"orderId": "ORD-001", "customerId": "CUST-100", "totalAmount": 50.00, "items": [{"productId": "PROD-1", "name": "Widget", "price": 50.00, "quantity": 1}]}'

# Trigger a validation error (empty items list — returns 400)
curl -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -d '{"orderId": "ORD-002", "customerId": "CUST-100", "totalAmount": 50.00, "items": []}'

# Trigger a downstream service exception (returns 503)
curl -X POST http://localhost:5000/orders/risky \
  -H "Content-Type: application/json" \
  -d '{"orderId": "ORD-003", "customerId": "CUST-200", "totalAmount": 75.00, "items": [{"productId": "PROD-1", "name": "Widget", "price": 75.00, "quantity": 1}]}'

# Trigger a flaky dependency error marked as expected (50% chance of 504)
curl -X POST http://localhost:5000/orders/flaky \
  -H "Content-Type: application/json" \
  -d '{"orderId": "ORD-004", "customerId": "CUST-300", "totalAmount": 25.00, "items": [{"productId": "PROD-2", "name": "Gadget", "price": 25.00, "quantity": 1}]}'
```

**Windows (PowerShell):**

```powershell
# Successful order (200 OK) — verify the app is running
Invoke-RestMethod -Method POST -Uri http://localhost:5000/orders `
  -ContentType "application/json" `
  -Body '{"orderId": "ORD-001", "customerId": "CUST-100", "totalAmount": 50.00, "items": [{"productId": "PROD-1", "name": "Widget", "price": 50.00, "quantity": 1}]}'

# Trigger a validation error (empty items list — returns 400)
Invoke-RestMethod -Method POST -Uri http://localhost:5000/orders `
  -ContentType "application/json" `
  -Body '{"orderId": "ORD-002", "customerId": "CUST-100", "totalAmount": 50.00, "items": []}'

# Trigger a downstream service exception (returns 503)
Invoke-RestMethod -Method POST -Uri http://localhost:5000/orders/risky `
  -ContentType "application/json" `
  -Body '{"orderId": "ORD-003", "customerId": "CUST-200", "totalAmount": 75.00, "items": [{"productId": "PROD-1", "name": "Widget", "price": 75.00, "quantity": 1}]}'

# Trigger a flaky dependency error marked as expected (50% chance of 504)
Invoke-RestMethod -Method POST -Uri http://localhost:5000/orders/flaky `
  -ContentType "application/json" `
  -Body '{"orderId": "ORD-004", "customerId": "CUST-300", "totalAmount": 25.00, "items": [{"productId": "PROD-2", "name": "Gadget", "price": 25.00, "quantity": 1}]}'
```
