// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using ApiDependencyInjection.Services;
using NewRelic.Api.Agent;

// Alias required because 'NewRelic' inside this file would otherwise resolve to the
// root namespace. With the alias we can call the static NewRelic.Api.Agent.NewRelic
// class unambiguously as NRAgent.
using NRAgent = NewRelic.Api.Agent.NewRelic;

var builder = WebApplication.CreateBuilder(args);

// Register IAgent as a singleton using a factory that calls GetAgent() lazily.
//
// The factory defers the GetAgent() call until the container first resolves IAgent —
// by which point the profiler has fully initialized the agent and GetAgent() returns
// the live instance instead of the no-op placeholder it returns very early in startup.
//
// Singleton is correct: IAgent itself is a long-lived handle to the agent runtime and
// is safe to share across threads and requests. What is NOT safe to share is any
// request-scoped state fetched from IAgent (CurrentTransaction, CurrentSpan) —
// see WorkService and AntiPatternExamples below for why.
//
// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#GetAgent
builder.Services.AddSingleton<IAgent>(_ => NRAgent.GetAgent());

// Services that consume IAgent via constructor injection. Any lifetime works here;
// the important constraint is on how the service USES IAgent, not on how it's registered.
builder.Services.AddSingleton<WorkService>();
builder.Services.AddScoped<ScopedWorkService>();

var app = builder.Build();

app.Urls.Add("http://localhost:5004");

// GET /work/singleton
// Demonstrates the correct pattern: a singleton service with an injected IAgent that
// fetches CurrentTransaction fresh inside each request-handling method.
app.MapGet("/work/singleton", (WorkService service) => service.DoWork());

// GET /work/scoped
// Demonstrates a scoped service consuming the singleton IAgent. Scoped lifetime is a
// natural fit for per-request services, but the IAgent registration is still singleton.
app.MapGet("/work/scoped", (ScopedWorkService service) => service.DoWork());

app.Run();
