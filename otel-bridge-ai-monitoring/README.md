# New Relic .NET Agent — AI Monitoring via the OpenTelemetry Bridge Example

This example demonstrates how the New Relic .NET agent captures **AI Monitoring** data for [`Microsoft.Extensions.AI`](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) through its **OpenTelemetry bridge** (sometimes called the "hybrid agent"). A Razor Pages chat application calls a language model, the AI client is instrumented with OpenTelemetry, and the agent ingests the resulting GenAI spans as AI Monitoring events — no OTLP exporter pointed at New Relic is required. The underlying model provider is selectable at runtime: the same instrumentation works with **Azure OpenAI** or **OpenAI**.

## How It Works

```
Browser ──WebSocket──► ChatHub.SendMessage  [Transaction]
                             │
                             ▼
                IChatClient (Microsoft.Extensions.AI)
                             │  .UseOpenTelemetry(EnableSensitiveData = true)
                             ▼
                ActivitySource "OtelBridgeAiMonitoring"  ──► GenAI spans (gen_ai.* tags)
                             │
                             ▼
        New Relic .NET agent — OpenTelemetry bridge + AI Monitoring
                             │
                             ▼
                     New Relic ── APM + AI Monitoring
```

1. **The AI client is instrumented with OpenTelemetry.** `ChatClientFactory` wraps the provider's `IChatClient` with `.UseOpenTelemetry(...)`. The Microsoft.Extensions.AI instrumentation emits [GenAI semantic-convention](https://opentelemetry.io/docs/specs/semconv/gen-ai/) spans (model name, token usage, and — because `EnableSensitiveData = true` — the prompt and completion text) to a custom `ActivitySource`.

2. **The New Relic agent reads those spans via its OpenTelemetry bridge.** When the bridge is enabled, the agent listens to the application's `ActivitySource`s directly, parses the `gen_ai.*` tags, and produces New Relic AI Monitoring events (`LlmChatCompletionSummary`, `LlmChatCompletionMessage`, etc.). The application does **not** send OTLP to New Relic — the agent does the export. The bundled console exporter exists only so you can see the spans locally.

3. **Hub methods carry the `[Transaction]` attribute.** SignalR hub invocations arrive over a WebSocket and are not HTTP requests, so they are not auto-instrumented. `[Transaction]` (from `NewRelic.Agent.Api`) starts a New Relic transaction for each call so the GenAI spans have a parent transaction to nest under. This is a consequence of SignalR's hosting model, not an AI Monitoring requirement — in a controller-based or minimal-API web app, the agent auto-instruments each HTTP request and no `[Transaction]` attribute is needed.

## Key Concepts Demonstrated

### Why the OpenTelemetry Bridge

`Microsoft.Extensions.AI` is a provider-agnostic abstraction over many model providers. Rather than instrument each provider SDK natively, the agent consumes the standardized GenAI OpenTelemetry spans that `Microsoft.Extensions.AI` already emits. Any provider you plug in behind `IChatClient` — Azure OpenAI and OpenAI are shown here — is captured the same way.

### Choosing a Provider: Azure OpenAI or OpenAI

The provider is selected by the `AI:Provider` configuration value. `ChatClientFactory` reads it and builds the appropriate client:

| `AI:Provider` | Backend | Client type | Settings used |
|---------------|---------|-------------|---------------|
| `azure` (default) | Azure OpenAI | `AzureOpenAIClient` (`Azure.AI.OpenAI`) | `AzureOpenAI:Endpoint`, `AzureOpenAI:ApiKey`, `AzureOpenAI:DeploymentName` |
| `openai` | OpenAI | `OpenAIClient` (`OpenAI`) | `OpenAI:ApiKey`, `OpenAI:Model` |

Both `AzureOpenAIClient.GetChatClient(...)` and `OpenAIClient.GetChatClient(...)` return the same `OpenAI.Chat.ChatClient` type, so a single code path in `ChatClientFactory` instruments either provider identically. The agent's GenAI parsing requires the `OpenAI` package version **2.8.0 or later** (and `Microsoft.Extensions.AI` / `Microsoft.Extensions.AI.OpenAI` **10.3.0 or later**).

### The Custom ActivitySource Name

The example emits spans to a custom, application-owned `ActivitySource` named `OtelBridgeAiMonitoring`. The bridge listens to custom sources by default, so no include list is needed. Do **not** reuse the OpenAI SDK's own built-in source `OpenAI.ChatClient` — the agent excludes that source by default to avoid duplicate spans.

### Required Agent Settings

AI Monitoring for `Microsoft.Extensions.AI` through the OpenTelemetry bridge requires New Relic .NET agent **version 10.50.0 or later**.

It also depends on three agent settings. **Two of them are off by default**, so AI Monitoring will not work until you turn them on:

| Setting | Default | Required value | Environment variable | `newrelic.config` |
|---------|---------|----------------|----------------------|-------------------|
| AI Monitoring | **off** | on | `NEW_RELIC_AI_MONITORING_ENABLED=1` | `<aiMonitoring enabled="true">` |
| OpenTelemetry bridge | **off** | on | `NEW_RELIC_OPENTELEMETRY_ENABLED=1` | `<openTelemetry enabled="true">` |
| AI Monitoring content recording | on | on (to see prompt/response text) | `NEW_RELIC_AI_MONITORING_RECORD_CONTENT_ENABLED=1` | `<recordContent enabled="true">` (under `aiMonitoring`) |

See the [.NET agent AI Monitoring configuration docs](https://docs.newrelic.com/docs/apm/agents/net-agent/configuration/net-agent-configuration/#ai_monitoring).

### Why the `[Transaction]` Attribute Is Needed Here

The agent's built-in ASP.NET Core instrumentation automatically creates a transaction for each incoming HTTP request. SignalR hub method invocations, however, arrive over a persistent WebSocket connection and are not individual HTTP requests, so auto-instrumentation does not apply. The `[Transaction]` attribute on each `ChatHub` method ensures the agent creates a transaction for the call, giving the GenAI spans a parent transaction to nest under.

In short, the attribute here addresses *how SignalR is hosted*, and has nothing to do with AI Monitoring specifically. If this example exposed its model calls through a controller action or minimal-API endpoint instead of a SignalR hub, the agent's automatic ASP.NET Core instrumentation would create the transaction and the attribute would be unnecessary.

## What the App Does

A minimal chat UI (one Razor page over a SignalR hub) with three buttons, each exercising a different path through the instrumented client:

| Button | Hub method | Demonstrates |
|--------|------------|--------------|
| **Send (Standard)** | `SendMessage` | A standard, non-streaming completion. |
| **Send (Streaming)** | `SendMessageStreaming` | A streaming completion (`GetStreamingResponseAsync`). |
| **Test Error** | `SendMessageFailure` | A call made with a deliberately invalid API key, so you can confirm errored GenAI calls appear in New Relic. |

## How to Run

### Prerequisites

- .NET 10 SDK
- A New Relic account with a license key
- New Relic .NET agent **version 10.50.0 or later** (the release that added AI Monitoring for `Microsoft.Extensions.AI` via the OpenTelemetry bridge)
- A model provider account: an Azure OpenAI resource with a deployed chat model, or an OpenAI API key

### NuGet Packages

This example references the following NuGet packages:

- **`NewRelic.Agent`** — Bundles the agent runtime files (profiler, configuration, extensions) into the build output directory. This is one of several ways to install the agent; alternatives include OS package managers and container-based installation.
- **`NewRelic.Agent.Api`** — Provides the `NewRelic.Api.Agent` namespace used in this example (the `[Transaction]` attribute on the hub methods). This package is required for any application that calls the agent API directly.
- **`Microsoft.Extensions.AI`**, **`Microsoft.Extensions.AI.OpenAI`**, **`Azure.AI.OpenAI`**, **`OpenAI`** — The GenAI client being instrumented. The versions referenced (`Azure.AI.OpenAI` `2.8.0-beta.1` and `OpenAI` `2.8.0`) are the combination verified by the agent's integration tests.
- **`OpenTelemetry.Extensions.Hosting`**, **`OpenTelemetry.Exporter.Console`** — Register the OpenTelemetry tracer provider and print spans to the console for local visibility. The console exporter is **not** required for New Relic ingestion.

### Configure the Provider

Select the provider and supply its credentials. The application reads these as standard [.NET configuration](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/), so you can provide them either as **user secrets** or as **environment variables** — either method is acceptable, and both override the placeholder values in `appsettings.json`. (Editing `appsettings.json` directly also works but is discouraged for credentials.)

The settings used per provider:

| Config key | Environment variable | Notes |
|------------|----------------------|-------|
| `AI:Provider` | `AI__Provider` | `azure` (default) or `openai` |
| `AzureOpenAI:Endpoint` | `AzureOpenAI__Endpoint` | Azure only |
| `AzureOpenAI:ApiKey` | `AzureOpenAI__ApiKey` | Azure only |
| `AzureOpenAI:DeploymentName` | `AzureOpenAI__DeploymentName` | Azure only; defaults to `gpt-4o-mini` |
| `OpenAI:ApiKey` | `OpenAI__ApiKey` | OpenAI only |
| `OpenAI:Model` | `OpenAI__Model` | OpenAI only; defaults to `gpt-4o-mini` |

Environment-variable names replace the `:` separator with a double underscore (`__`), per the .NET [environment variables configuration provider](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/#environment-variables).

#### Option A — user secrets

**Azure OpenAI:**

```bash
dotnet user-secrets init
dotnet user-secrets set "AI:Provider" "azure"
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "<your-azure-openai-key>"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-4o-mini"
```

**OpenAI:**

```bash
dotnet user-secrets init
dotnet user-secrets set "AI:Provider" "openai"
dotnet user-secrets set "OpenAI:ApiKey" "<your-openai-key>"
dotnet user-secrets set "OpenAI:Model" "gpt-4o-mini"
```

#### Option B — environment variables

**Azure OpenAI — Windows (PowerShell):**

```powershell
$env:AI__Provider="azure"
$env:AzureOpenAI__Endpoint="https://your-resource.openai.azure.com/"
$env:AzureOpenAI__ApiKey="<your-azure-openai-key>"
$env:AzureOpenAI__DeploymentName="gpt-4o-mini"
```

**Azure OpenAI — Linux / macOS (bash):**

```bash
export AI__Provider="azure"
export AzureOpenAI__Endpoint="https://your-resource.openai.azure.com/"
export AzureOpenAI__ApiKey="<your-azure-openai-key>"
export AzureOpenAI__DeploymentName="gpt-4o-mini"
```

**OpenAI — Windows (PowerShell):**

```powershell
$env:AI__Provider="openai"
$env:OpenAI__ApiKey="<your-openai-key>"
$env:OpenAI__Model="gpt-4o-mini"
```

**OpenAI — Linux / macOS (bash):**

```bash
export AI__Provider="openai"
export OpenAI__ApiKey="<your-openai-key>"
export OpenAI__Model="gpt-4o-mini"
```

Set these in the same shell session you use to run the app, alongside the agent variables below.

### Run the Application

This example uses the `NewRelic.Agent` NuGet package, which places the agent files under the build output directory. You must set several [environment variables](https://docs.newrelic.com/docs/apm/agents/net-agent/other-installation/understanding-net-agent-environment-variables/) to enable the .NET CLR profiler, the OpenTelemetry bridge, and AI Monitoring.

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
$env:NEW_RELIC_APP_NAME="otel-bridge-ai-monitoring-example"

# Enable AI Monitoring and the OpenTelemetry bridge (both OFF by default)
$env:NEW_RELIC_AI_MONITORING_ENABLED=1
$env:NEW_RELIC_OPENTELEMETRY_ENABLED=1
$env:NEW_RELIC_AI_MONITORING_RECORD_CONTENT_ENABLED=1

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
export NEW_RELIC_APP_NAME="otel-bridge-ai-monitoring-example"

# Enable AI Monitoring and the OpenTelemetry bridge (both OFF by default)
export NEW_RELIC_AI_MONITORING_ENABLED=1
export NEW_RELIC_OPENTELEMETRY_ENABLED=1
export NEW_RELIC_AI_MONITORING_RECORD_CONTENT_ENABLED=1

# Run the application
dotnet run -c Release --no-build
```

Then open the app (the console prints the URL, e.g. `https://localhost:7102`), type a prompt, and use the three buttons.

| Variable | Purpose |
|----------|---------|
| `CORECLR_ENABLE_PROFILING` | Enables the .NET CLR profiler (must be `1`) |
| `CORECLR_PROFILER` | The CLSID of the New Relic profiler |
| `CORECLR_PROFILER_PATH` | Path to the profiler native library (`NewRelic.Profiler.dll` on Windows, `libNewRelicProfiler.so` on Linux) |
| `CORECLR_NEWRELIC_HOME` | Path to the directory containing the agent configuration and extensions |
| `NEW_RELIC_LICENSE_KEY` | Your New Relic license key |
| `NEW_RELIC_APP_NAME` | The application name as it will appear in New Relic |
| `NEW_RELIC_AI_MONITORING_ENABLED` | Enables AI Monitoring. Off by default; required for this example |
| `NEW_RELIC_OPENTELEMETRY_ENABLED` | Enables the OpenTelemetry bridge. Off by default; required for this example |
| `NEW_RELIC_AI_MONITORING_RECORD_CONTENT_ENABLED` | Controls whether prompt/response content is recorded. On by default; set to `0` to suppress message content |

## What to Look for in New Relic

After exercising the buttons a few times:

- **APM → your app → Transactions** shows transactions named for the `ChatHub` methods (`SendMessage`, `SendMessageStreaming`, `SendMessageFailure`).
- **AI Monitoring** surfaces the GenAI calls, including model name, token counts, and — with content recording enabled — prompt and response content.
- The **Test Error** button produces errored GenAI calls you can inspect in **Errors Inbox** and AI Monitoring.

> **Note on sensitive data:** `EnableSensitiveData = true` (in the app) puts prompt and completion text on the spans, and `aiMonitoring.recordContent` (in the agent) controls whether the agent records it. Disable either if message content must not leave your environment.
