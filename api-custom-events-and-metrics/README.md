# New Relic .NET Agent API — Custom Events and Metrics Example

This example demonstrates how to use the New Relic .NET agent's custom event and metric APIs to record business-level data and numeric measurements.

## API Methods Covered

| Method | Documentation |
|--------|---------------|
| `RecordCustomEvent` | [RecordCustomEvent](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#RecordCustomEvent) |
| `RecordMetric` | [RecordMetric](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#RecordMetric) |
| `IncrementCounter` | [IncrementCounter](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#IncrementCounter) |
| `[Transaction]` attribute | [Custom instrumentation via attributes](https://docs.newrelic.com/docs/apm/agents/net-agent/custom-instrumentation/custom-instrumentation-attributes-net/) |

## Key Concepts Demonstrated

### Custom Events vs. Custom Metrics

The agent API provides two fundamentally different ways to record custom data:

- **Custom events** (`RecordCustomEvent`) create individual, queryable event rows with arbitrary key-value attributes. Use them for business analytics where you need to filter, facet, and alert on specific attribute values — for example, "show me all checkouts over $100 in the US region."

- **Custom metrics** (`RecordMetric`, `IncrementCounter`) create pre-aggregated metric timeslices. Use them for numeric measurements where you care about averages, percentiles, and trends — not individual occurrences.

### RecordMetric

`RecordMetric(string name, float value)` records a duration in seconds. The metric name must start with `Custom/` ([RecordMetric docs](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#RecordMetric)). A companion method, `RecordResponseTimeMetric(string name, long millis)`, is also available and behaves identically except that its value is in milliseconds ([RecordResponseTimeMetric docs](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#RecordResponseTimeMetric)).

### IncrementCounter

`IncrementCounter(string name)` increments a metric counter by exactly 1 per call. There is no "increment by N" variant — use `RecordMetric` if you need to record a duration value. Counter names must start with `Custom/` ([IncrementCounter docs](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#IncrementCounter)).

### Custom Event Requirements

Event type names are limited to 255 characters and may contain alphanumeric characters, colons (`:`), and underscores (`_`). Certain event type name prefixes are reserved by New Relic and will be dropped — see [data requirements and limits](https://docs.newrelic.com/docs/data-apis/custom-data/custom-events/data-requirements-limits-custom-event-data/) for the full list. When using the APM agent API, each event supports a maximum of 64 attributes ([data requirements](https://docs.newrelic.com/docs/data-apis/custom-data/custom-events/data-requirements-limits-custom-event-data/)).

### When to Use the `[Transaction]` Attribute

The agent's built-in ASP.NET Core instrumentation automatically creates a transaction for each incoming HTTP request. Service methods called from these endpoints are already within a transaction and do **not** need the `[Transaction]` attribute.

The `[Transaction]` attribute is needed when the agent's auto-instrumentation does not apply — for example, in a `BackgroundService`. This example includes `PeriodicMaintenanceService`, a `BackgroundService` whose periodic work method uses `[Transaction]` to ensure the agent creates a transaction.

## How to Run

### Prerequisites

- .NET 10 SDK
- A New Relic account with a license key

### NuGet Packages

This example references two NuGet packages:

- **`NewRelic.Agent`** — Bundles the agent runtime files (profiler, configuration, extensions) into the build output directory. This is one of several ways to install the agent; alternatives include OS package managers and container-based installation.
- **`NewRelic.Agent.Api`** — Provides the `NewRelic.Api.Agent` namespace used throughout this example (`RecordCustomEvent`, `RecordMetric`, `IncrementCounter`, `[Transaction]`). This package is required for any application that calls the agent API directly.

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
$env:NEW_RELIC_APP_NAME="api-custom-events-and-metrics-example"

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
export NEW_RELIC_APP_NAME="api-custom-events-and-metrics-example"

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
| `/checkout` | POST | Processes a checkout. Records `CheckoutCompleted` or `CheckoutFailed` custom events and increments a failure counter on validation errors. |
| `/products/{id}` | GET | Retrieves a product with cache hit/miss tracking via `IncrementCounter`. |

The `PeriodicMaintenanceService` background service runs a simulated maintenance task every 30 seconds and records its duration (`Custom/Maintenance/TaskDuration`) in seconds using `RecordMetric`.

### Example Requests

**Linux / macOS (bash):**

```bash
# Successful checkout (200 OK) — verify the app is running
curl -X POST http://localhost:5003/checkout \
  -H "Content-Type: application/json" \
  -d '{"customerId": "CUST-100", "totalAmount": 79.99, "items": [{"productId": "PROD-1", "name": "Widget", "price": 49.99, "quantity": 1}, {"productId": "PROD-2", "name": "Gadget", "price": 30.00, "quantity": 1}], "paymentMethod": "credit_card", "region": "us-east"}'

# Failed checkout — empty cart (400 Bad Request)
curl -X POST http://localhost:5003/checkout \
  -H "Content-Type: application/json" \
  -d '{"customerId": "CUST-200", "totalAmount": 0, "items": [], "paymentMethod": "credit_card", "region": "us-west"}'

# Product lookup — first request is a cache miss, second is a cache hit
curl http://localhost:5003/products/PROD-42
curl http://localhost:5003/products/PROD-42
```

**Windows (PowerShell):**

```powershell
# Successful checkout (200 OK) — verify the app is running
Invoke-RestMethod -Method POST -Uri http://localhost:5003/checkout `
  -ContentType "application/json" `
  -Body '{"customerId": "CUST-100", "totalAmount": 79.99, "items": [{"productId": "PROD-1", "name": "Widget", "price": 49.99, "quantity": 1}, {"productId": "PROD-2", "name": "Gadget", "price": 30.00, "quantity": 1}], "paymentMethod": "credit_card", "region": "us-east"}'

# Failed checkout — empty cart (400 Bad Request)
Invoke-RestMethod -Method POST -Uri http://localhost:5003/checkout `
  -ContentType "application/json" `
  -Body '{"customerId": "CUST-200", "totalAmount": 0, "items": [], "paymentMethod": "credit_card", "region": "us-west"}'

# Product lookup — first request is a cache miss, second is a cache hit
Invoke-RestMethod -Uri http://localhost:5003/products/PROD-42
Invoke-RestMethod -Uri http://localhost:5003/products/PROD-42
```

## What to Look for in New Relic

- Custom events are queryable via NRQL using `FROM <EventType>` syntax
  ([custom events docs](https://docs.newrelic.com/docs/data-apis/custom-data/custom-events/apm-report-custom-events-attributes/)):
  ```
  SELECT * FROM CheckoutCompleted SINCE 1 hour ago
  SELECT average(totalAmount) FROM CheckoutCompleted FACET region SINCE 1 hour ago
  SELECT count(*) FROM CheckoutFailed FACET reason SINCE 1 hour ago
  ```

- Custom metrics (from `RecordMetric` and `IncrementCounter`) are queryable as APM metric timeslice data via `FROM Metric` using `newrelic.timeslice.value` and `metricTimesliceName`. An `entity.guid` (or `appName`) filter is required to scope the query to your application
  ([Query APM metric timeslice data with NRQL](https://docs.newrelic.com/docs/data-apis/understand-data/metric-data/query-apm-metric-timeslice-data-nrql/)):
  ```sql
  SELECT average(newrelic.timeslice.value) AS `Custom/Cache/Hits`
  FROM Metric
  WHERE metricTimesliceName = 'Custom/Cache/Hits'
    AND entity.guid = '<your-entity-guid>'
  SINCE 60 minutes ago

  -- Discover all custom metric names recorded by your app:
  SELECT uniques(metricTimesliceName) FROM Metric
  WHERE appName = '<your-app-name>'
    AND newrelic.timeslice.value IS NOT NULL
    AND metricTimesliceName LIKE 'Custom/%'
  SINCE 60 minutes ago
  ```
