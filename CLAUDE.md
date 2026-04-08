# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Collection of example applications demonstrating how to install and use the New Relic .NET agent across various deployment scenarios (Docker, Kubernetes, AWS Elastic Beanstalk, console apps, custom instrumentation). Maintained by the New Relic DOTNET team.

## Build

All examples are in a single solution. Requires .NET 10 SDK.

```bash
dotnet build                              # build entire solution
dotnet build console/ConsoleSample.csproj  # build a single example
```

There are no unit tests in this repository. CI runs `dotnet build` only.

## Code Style

Enforced via `.editorconfig`:

- **File header required** (error-level): every `.cs` file must start with the Apache 2.0 license header:
  ```
  // Copyright 2023 New Relic, Inc. All rights reserved.
  // SPDX-License-Identifier: Apache-2.0
  ```
- Braces on new lines (Allman style)
- 4-space indentation, CRLF line endings
- PascalCase for constants
- `var` usage preferred
- Sort system usings first

## Repository Structure

Each top-level directory is an independent example with its own `.csproj` and often its own `Dockerfile` and/or `README.md`:

- **console/** — BackgroundService-based console app
- **custom-distributed-tracing/** — Sender/Receiver apps with Azure Service Bus and docker-compose
- **custom-instrumentation/xml-custom-instrumentation/** — XML-based custom instrumentation
- **docker-agent-installed/** — Agent installed via OS package manager (alpine/centos/ubuntu variants)
- **docker-agent-nuget/** — Agent installed via NuGet package (alpine/centos/ubuntu variants)
- **elastic-beanstalk/** — AWS Elastic Beanstalk (Linux and Windows)
- **ignore-errors/** — Error handling configuration
- **kubernetes/** — Helm chart with K8s operator-based agent injection

## New Relic Agent Patterns

Two main integration approaches shown across examples:

1. **NuGet package** (`NewRelic.Agent`): added as a project dependency, agent files bundled automatically
2. **OS package manager**: agent installed in Dockerfile via apt/dnf/apk or tarball extraction

Common environment variables used:
- `CORECLR_ENABLE_PROFILING=1`
- `CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A}`
- `CORECLR_PROFILER_PATH` and `CORECLR_NEWRELIC_HOME` (path varies by OS/install method)
- `NEW_RELIC_LICENSE_KEY`, `NEW_RELIC_APP_NAME`

Custom API instrumentation uses `NewRelic.Agent.Api` with `[Transaction]` attributes.

## Docker Examples

All Docker examples use multi-stage builds with `mcr.microsoft.com/dotnet/sdk` (build) and `mcr.microsoft.com/dotnet/aspnet` (runtime). Three OS variants: Alpine, CentOS Stream 9, Ubuntu Noble.

## Contributing

- CLA signature required (via CLA-Assistant)
- PRs require sign-off from 2 reviewers
- Code ownership: `@newrelic/DOTNET`
