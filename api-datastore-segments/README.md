# New Relic .NET Agent API — Datastore Segments Example

This example demonstrates how to use `ITransaction.RecordDatastoreSegment()` to manually instrument data stores that the agent does not auto-instrument. It shows the `using`/`IDisposable` pattern for measuring segment duration, how segment parameters map to the New Relic UI, and how multiple segments within a single transaction appear as separate nodes in the trace waterfall.

## API Methods Covered

| Method | Documentation |
|--------|---------------|
| `ITransaction.RecordDatastoreSegment(string, string, string, string?, string?, string?, string?)` | [RecordDatastoreSegment](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#RecordDatastoreSegment) |

## Key Concepts Demonstrated

### The `using` / `IDisposable` Pattern

`RecordDatastoreSegment()` returns a `SegmentWrapper?` that implements `IDisposable`. The segment's duration is automatically measured from creation to `Dispose()`. Use `using var segment = ...` so the segment ends when the variable goes out of scope:

```csharp
using var segment = txn.RecordDatastoreSegment(vendor, model, operation, ...);
// Actual datastore call goes here — the segment times this work
_store[doc.Id] = doc;
// segment.Dispose() is called automatically when the scope exits
```

The return value is **nullable** — it returns `null` if no transaction is active. The `using var` pattern handles this gracefully because `Dispose()` is not called on null references.

### Parameter Reference

| Parameter | Purpose | Example |
|-----------|---------|---------|
| `vendor` | Datastore vendor name. Note: the agent currently categorizes all `RecordDatastoreSegment` calls under `Other` in the Databases UI, but still pass a meaningful value for future compatibility | `"CustomDocStore"` |
| `model` | The table or collection equivalent; groups operations in reporting | `"documents"` |
| `operation` | The operation type (`select`, `insert`, `update`, `delete`); used for operation-level grouping | `"select"` |
| `commandText` | Optional; represents the query sent to the datastore. Avoid including sensitive data | `"SEARCH documents WHERE title = 'hello'"` |
| `host` | The datastore instance host | `"localhost"` |
| `portPathOrID` | The port, path, or identifier for the datastore instance | `"9999"` |
| `databaseName` | The database name | `"documents"` |

### Multiple Segments in One Transaction

Each call to `RecordDatastoreSegment()` within the same transaction creates a separate segment. The `Search` endpoint demonstrates this by creating two segments — one for the search query and one for loading matched document details. These appear as sibling nodes in the trace waterfall, which is useful for understanding which datastore calls dominate a transaction's time.

### When `[Transaction]` Is NOT Needed

The service methods in this example do NOT use the `[Transaction]` attribute. The agent's built-in ASP.NET Core instrumentation automatically creates a transaction for each incoming HTTP request. The `[Transaction]` attribute is only needed when auto-instrumentation does not apply (e.g., in a `BackgroundService`).

## NuGet Packages

This example references two NuGet packages:

- **`NewRelic.Agent`** — Bundles the agent runtime files (profiler, configuration, extensions) into the build output directory. This is one of several ways to install the agent; alternatives include OS package managers and container-based installation.
- **`NewRelic.Agent.Api`** — Provides the `NewRelic.Api.Agent` namespace used in this example (`RecordDatastoreSegment`). This package is required for any application that calls the agent API directly.

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
$env:NEW_RELIC_APP_NAME="api-datastore-segments-example"

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
export NEW_RELIC_APP_NAME="api-datastore-segments-example"

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
| `/documents` | POST | Creates a new document (datastore segment with operation `insert`) |
| `/documents/{id}` | GET | Retrieves a document by ID (datastore segment with operation `select`) |
| `/documents/{id}` | PUT | Updates an existing document (datastore segment with operation `update`) |
| `/documents/{id}` | DELETE | Deletes a document (datastore segment with operation `delete`) |
| `/documents/search?field={field}&value={value}` | GET | Searches documents by field (two datastore segments with `commandText`) |

### Example Requests

**Linux / macOS (bash):**

```bash
# Create a document (201 Created)
curl -X POST http://localhost:5002/documents \
  -H "Content-Type: application/json" \
  -d '{"id": "doc-1", "title": "Getting Started", "content": "Welcome to the document store.", "createdAt": "2026-01-15T10:00:00Z"}'

# Create a second document for search testing (201 Created)
curl -X POST http://localhost:5002/documents \
  -H "Content-Type: application/json" \
  -d '{"id": "doc-2", "title": "Getting Started with Agents", "content": "How to install the New Relic agent.", "createdAt": "2026-01-16T10:00:00Z"}'

# Read a document (200 OK)
curl http://localhost:5002/documents/doc-1

# Update a document (200 OK)
curl -X PUT http://localhost:5002/documents/doc-1 \
  -H "Content-Type: application/json" \
  -d '{"id": "doc-1", "title": "Getting Started (Updated)", "content": "Updated content.", "createdAt": "2026-01-15T10:00:00Z"}'

# Search documents by title (200 OK) — demonstrates commandText and multiple segments
curl "http://localhost:5002/documents/search?field=title&value=Getting"

# Delete a document (200 OK)
curl -X DELETE http://localhost:5002/documents/doc-1

# Read a deleted document (404 Not Found)
curl http://localhost:5002/documents/doc-1
```

**Windows (PowerShell):**

```powershell
# Create a document (201 Created)
Invoke-RestMethod -Method POST -Uri http://localhost:5002/documents `
  -ContentType "application/json" `
  -Body '{"id": "doc-1", "title": "Getting Started", "content": "Welcome to the document store.", "createdAt": "2026-01-15T10:00:00Z"}'

# Create a second document for search testing (201 Created)
Invoke-RestMethod -Method POST -Uri http://localhost:5002/documents `
  -ContentType "application/json" `
  -Body '{"id": "doc-2", "title": "Getting Started with Agents", "content": "How to install the New Relic agent.", "createdAt": "2026-01-16T10:00:00Z"}'

# Read a document (200 OK)
Invoke-RestMethod -Uri http://localhost:5002/documents/doc-1

# Update a document (200 OK)
Invoke-RestMethod -Method PUT -Uri http://localhost:5002/documents/doc-1 `
  -ContentType "application/json" `
  -Body '{"id": "doc-1", "title": "Getting Started (Updated)", "content": "Updated content.", "createdAt": "2026-01-15T10:00:00Z"}'

# Search documents by title (200 OK) — demonstrates commandText and multiple segments
Invoke-RestMethod -Uri "http://localhost:5002/documents/search?field=title&value=Getting"

# Delete a document (200 OK)
Invoke-RestMethod -Method DELETE -Uri http://localhost:5002/documents/doc-1

# Read a deleted document (404 Not Found — throws error in PowerShell)
Invoke-RestMethod -Uri http://localhost:5002/documents/doc-1
```

## What to Look for in New Relic

- **Databases UI**: The datastore appears under the vendor name `Other`. The agent's current implementation categorizes all `RecordDatastoreSegment()` calls as `Other` regardless of the `vendor` string passed — only the agent's built-in instrumentation for known databases (MySQL, PostgreSQL, MongoDB, etc.) uses specific vendor categories. The `model` value (`documents`) and operations (`select`, `insert`, `update`, `delete`) are reported correctly and can be used to filter and group activity.
  <!-- Verified via agent source: TransactionBridgeApi.StartDatastoreSegment() hardcodes DatastoreVendor.Other.
       Metric names follow the pattern Datastore/statement/Other/{model}/{operation}. -->
- **Transaction traces**: Datastore segments appear as children of the web transaction, with timing reflecting the code inside the `using` block.
- **Multiple segments**: The search endpoint creates two datastore segments in one transaction — these appear as separate sibling nodes in the trace waterfall.
- **Slow queries**: The search endpoint's first segment includes a `commandText` and is artificially slowed to exceed the default slow SQL threshold (500ms). The agent collects slow SQL traces for any `DatastoreSegmentData` segment with non-null `commandText` that exceeds the `explainThreshold` — there is no vendor-specific filtering.
  <!-- Verified via agent source: TransactionTransformer.GenerateAndCollectSqlTrace() iterates all DatastoreSegmentData
       segments checking CommandText != null and Duration >= SqlExplainPlanThreshold, with no vendor filter.
       Integration test RecordDatastoreSegmentTests confirms DatastoreVendor.Other segments appear in slow SQL traces. -->
