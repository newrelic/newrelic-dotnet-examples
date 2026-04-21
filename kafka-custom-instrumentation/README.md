# New Relic .NET Agent — Kafka Custom Instrumentation Example

This example demonstrates how transaction placement — where you open a New Relic transaction relative to the Kafka `Consume()` call — determines what gets measured in APM. Two patterns are shown: one where the transaction spans the full message lifecycle from consume to completion, and one where the transaction covers only the processing work.

## API Methods Covered

| Method | Documentation |
|--------|---------------|
| `[Transaction]` attribute | [Custom instrumentation via attributes](https://docs.newrelic.com/docs/apm/agents/net-agent/custom-instrumentation/custom-instrumentation-attributes-net/) |
| `[Trace]` attribute | [Custom instrumentation via attributes](https://docs.newrelic.com/docs/apm/agents/net-agent/custom-instrumentation/custom-instrumentation-attributes-net/) |
| `ITransaction.InsertDistributedTraceHeaders()` | [InsertDistributedTraceHeaders](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#InsertDistributedTraceHeaders) |
| `ITransaction.AcceptDistributedTraceHeaders()` | [AcceptDistributedTraceHeaders](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#AcceptDistributedTraceHeaders) |
| `NewRelic.SetTransactionName()` | [SetTransactionName](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#SetTransactionName) |
| `NewRelic.IgnoreTransaction()` | [IgnoreTransaction](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#IgnoreTransaction) |
| `ITransaction.AddCustomAttribute()` | [AddCustomAttribute](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#ITransaction.AddCustomAttribute) |

## Key Concepts Demonstrated

### Transaction Placement and What It Measures

The .NET agent can automatically instrument calls to `consumer.Consume()` from the Confluent.Kafka client, but only when the call is made from inside an active transaction. When `Consume()` runs inside a transaction, the agent:

- Creates a **MessageBroker segment** in the APM trace timeline, showing the consume step as a distinct span
- **Automatically accepts** any New Relic distributed trace headers present in the message, linking the consumer transaction to the upstream producer

When `Consume()` runs outside a transaction, neither of those things happens. The choice of where to open the transaction therefore determines both what the transaction duration measures and what appears in the trace timeline.

### Pattern 1: Consume-to-Complete (`ConsumeToCompleteConsumerService`)

Use this pattern when you want APM to show both the consume step and the processing step as distinct segments in the same transaction — for example, when you want to see how much of a transaction's duration is spent in the Confluent.Kafka `Consume()` call versus in your own processing code.

Note that the transaction duration includes the time `Consume()` spent polling (up to the 200ms timeout), not the time the message spent waiting in the Kafka queue before this poll. True produce-to-consume queue latency is not captured in the transaction duration. It is, however, visible indirectly: because distributed trace headers are automatically accepted from the message, the producer HTTP transaction and this consumer transaction are linked in New Relic's distributed tracing view. You can inspect individual traces to see the wall-clock gap between the producer span and the consumer span, but this is not automatically aggregated as a metric.

The structure:

```
ExecuteAsync() — tight loop, no transaction
  └─ ConsumeAndProcess()  [Transaction]  ← transaction opens here, before Consume()
       └─ consumer.Consume()             ← auto-instrumented: MessageBroker segment created,
       │                                    DT headers accepted automatically
       └─ ProcessMessage()  [Trace]      ← child span for processing work
```

Because `ConsumeAndProcess()` is decorated with `[Transaction]`, a transaction is active when `Consume()` executes. When no message arrives within the poll timeout, `IgnoreTransaction()` discards the empty transaction so that idle polling cycles do not appear in New Relic.

The transaction duration includes both the time `Consume()` took (up to the 200ms poll timeout) and the time `ProcessMessage()` took. In the APM trace timeline you will see a `MessageBroker/Kafka/Topic/Consume/Named/consume-to-complete-messages` segment followed by the `ProcessMessage` segment.

### Pattern 2: Processing-Only (`ProcessingOnlyConsumerService`)

Use this pattern when **the consumer acts as a trigger for work** and you want APM to reflect the cost of that work — for example, when a message kicks off a report generation job or an ETL pipeline and you want to measure how long the job takes, independent of how long the consumer polled before the message arrived.

The structure:

```
ExecuteAsync() — loop, no transaction
  └─ consumer.Consume(stoppingToken)    ← NOT instrumented: no transaction active,
  │                                        blocks until a message arrives
  └─ ProcessMessage()  [Transaction]    ← transaction opens only when a message exists;
       │                                   duration = processing cost only
       └─ AcceptDistributedTraceHeaders()  ← must be called manually (the agent did not
       │                                      intercept Consume(), so it did not do this)
       └─ SetTransactionName()          ← must be called explicitly (no MessageBroker
                                           segment to derive a name from)
```

Because `Consume()` ran outside a transaction, the agent never saw the call and did not accept the distributed trace headers. `AcceptDistributedTraceHeaders()` must be called manually at the start of `ProcessMessage()` to establish the link back to the producer. Without it, the consumer transaction appears isolated in New Relic with no connection to the trace that produced the message.

The transaction duration reflects only the actual processing work, regardless of how long the consumer polled before the message arrived.

### Producer: Injecting Distributed Trace Headers

Both produce endpoints are auto-instrumented by the agent as ASP.NET Core web transactions. `KafkaProducerService.ProduceWithTraceHeadersAsync()` calls `InsertDistributedTraceHeaders()` to write the current transaction's trace context into the Kafka message headers as UTF-8 byte arrays. This connects the producer-side HTTP transaction to the consumer-side transaction in New Relic's distributed tracing view.

### `IgnoreTransaction()` for Empty Polling Cycles (Pattern 1 Only)

In Pattern 1, `ConsumeAndProcess()` opens a `[Transaction]` before every `Consume()` call, including calls that return null (no message within the timeout). `IgnoreTransaction()` tells the agent to discard the current transaction and not report it to New Relic. This keeps the APM transaction list clean — only message-processing transactions appear, not empty polling cycles.

## NuGet Packages

- **`Confluent.Kafka`** — The official .NET client for Apache Kafka. Provides `IProducer<>`, `IConsumer<>`, and related types.
- **`NewRelic.Agent`** — Bundles the agent runtime files (profiler, configuration, extensions) into the build output directory.
- **`NewRelic.Agent.Api`** — Provides the `NewRelic.Api.Agent` namespace used throughout this example.

## Prerequisites

- .NET 10 SDK
- Docker (for the local Kafka broker)
- A New Relic account with a license key

## How to Run

### 1. Start Kafka

Start a local single-node Kafka broker using the provided `docker-compose.yml`:

```bash
docker compose up -d
```

The broker listens on `localhost:9092`. To stop it later: `docker compose down`.

### 2. Run the Application

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
$env:NEW_RELIC_APP_NAME="kafka-custom-instrumentation-example"

# Run the application
dotnet run -c Release --no-build
```

**Linux (bash):**

```bash
# Build first so the agent files are in the output directory
dotnet build -c Release

# Set the required environment variables for the .NET agent
export CORECLR_ENABLE_PROFILING=1
export CORECLR_PROFILER="{36032161-FFC0-4B61-B559-F6C5D41BAE5A}"
export CORECLR_PROFILER_PATH="$PWD/bin/Release/net10.0/newrelic/libNewRelicProfiler.so"
export CORECLR_NEWRELIC_HOME="$PWD/bin/Release/net10.0/newrelic"
export NEW_RELIC_LICENSE_KEY="<your-new-relic-license-key>"
export NEW_RELIC_APP_NAME="kafka-custom-instrumentation-example"

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
| `KAFKA_BOOTSTRAP_SERVERS` | (Optional) Kafka bootstrap servers. Defaults to `localhost:9092` |

## Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/consume-to-complete/produce` | POST | Sends a message to the consume-to-complete topic. The consumer's transaction spans from `Consume()` to end of processing; transaction duration includes both consume time and processing time. |
| `/processing-only/produce` | POST | Sends a message to the processing-only topic. The consumer's transaction opens only when a message arrives; transaction duration reflects processing cost only. |

Both endpoints inject distributed trace headers into the message before producing, so the producer HTTP transaction and consumer transaction appear as a linked distributed trace in New Relic.

## Example Requests

**Linux (bash):**

```bash
# Pattern 1 (consume-to-complete): Consume() is called inside a [Transaction], so the
# agent auto-instruments it. The transaction duration includes both the Consume() poll
# time and the processing time, and a MessageBroker segment appears in the APM trace.
curl -X POST http://localhost:5004/consume-to-complete/produce \
  -H "Content-Type: text/plain" \
  -d "order-12345 ready for fulfillment"

# Pattern 2 (processing-only): Consume() is called outside any transaction, so the agent
# does not instrument it. The transaction opens only when a message arrives and covers
# processing time only. Distributed trace headers are accepted manually in ProcessMessage().
curl -X POST http://localhost:5004/processing-only/produce \
  -H "Content-Type: text/plain" \
  -d "generate monthly report for account 9876"
```

**Windows (PowerShell):**

```powershell
# Pattern 1 (consume-to-complete): Consume() is called inside a [Transaction], so the
# agent auto-instruments it. The transaction duration includes both the Consume() poll
# time and the processing time, and a MessageBroker segment appears in the APM trace.
Invoke-RestMethod -Method POST -Uri http://localhost:5004/consume-to-complete/produce `
  -ContentType "text/plain" `
  -Body "order-12345 ready for fulfillment"

# Pattern 2 (processing-only): Consume() is called outside any transaction, so the agent
# does not instrument it. The transaction opens only when a message arrives and covers
# processing time only. Distributed trace headers are accepted manually in ProcessMessage().
Invoke-RestMethod -Method POST -Uri http://localhost:5004/processing-only/produce `
  -ContentType "text/plain" `
  -Body "generate monthly report for account 9876"
```

## What to Look For

### APM Transactions

After sending requests, navigate to **APM > (your app) > Transactions** to find the two consumer transaction types.

**Pattern 1 — Consume-to-Complete** transactions are named `OtherTransaction/Kafka/ConsumeToComplete/consume-to-complete-messages`. Open one in the trace details — you will see a `MessageBroker/Kafka/Topic/Consume/Named/consume-to-complete-messages` segment followed by the `ProcessMessage` segment. The transaction duration includes both.

**Pattern 2 — Processing-Only** transactions are named `OtherTransaction/Kafka/ProcessingOnly/processing-only-messages`. Open one in the trace details — there is no MessageBroker segment because `Consume()` was not instrumented. The transaction duration reflects only the processing work.

Because Pattern 1 transactions include the `Consume()` poll window (up to 200ms) while Pattern 2 transactions do not, their average durations will differ even when the underlying processing work is similar. This difference illustrates the core tradeoff: Pattern 1 duration includes poll time plus processing time — not queue latency. Pattern 2 duration reflects only processing cost.

### Custom Attributes

Both consumers add Kafka delivery metadata as custom attributes on each transaction: `kafka.topic`, `kafka.partition`, `kafka.offset`, and `kafka.message.key`. These are visible in the transaction attributes panel in APM.

### Distributed Tracing

Each produce endpoint call generates one HTTP transaction (the producer) and one Kafka consumer transaction. Both carry the same trace ID and appear as a single connected trace in New Relic's distributed tracing view.

Navigate to **APM > (your app) > Distributed tracing** and select a trace that includes both an HTTP span (the producer endpoint) and a Kafka consumer span. In Pattern 1, the consumer span will have a `MessageBroker` child segment. In Pattern 2, it will not — but both traces will show the full producer-to-consumer link because distributed trace headers were accepted in both cases (automatically in Pattern 1, manually via `AcceptDistributedTraceHeaders()` in Pattern 2).
