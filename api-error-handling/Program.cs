// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using ApiErrorHandling;
using ApiErrorHandling.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<InventoryCheckService>();
builder.Services.AddSingleton<OrderService>();

// Register the background health check service. This demonstrates [Transaction] usage:
// unlike web request handlers (which are auto-instrumented), BackgroundService methods
// need the [Transaction] attribute for the agent to create a transaction.
builder.Services.AddHostedService<StartupHealthCheckService>();

var app = builder.Build();

app.Urls.Add("http://localhost:5000");

// Register the error group callback at startup, before the app starts handling requests.
// The agent uses this callback to determine the error group name for error events and traces.
// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#SetErrorGroupCallback
NewRelic.Api.Agent.NewRelic.SetErrorGroupCallback(ErrorGroupCallback.DetermineErrorGroup);

// POST /orders - Demonstrates NoticeError(string, IDictionary<string, object>)
// Reports a validation error with a message and custom attributes.
// The transaction is created automatically by the agent's ASP.NET Core instrumentation.
app.MapPost("/orders", (OrderRequest request, OrderService orderService) =>
    orderService.ProcessOrder(request));

// POST /orders/risky - Demonstrates NoticeError(Exception, IDictionary<string, object>)
// Catches an exception from a downstream service and reports it with custom context.
app.MapPost("/orders/risky", (OrderRequest request, OrderService orderService) =>
    orderService.ProcessRiskyOrder(request));

// POST /orders/flaky - Demonstrates NoticeError(string, IDictionary<string, string>, isExpected: true)
// Reports an error from a known-flaky dependency, marked as expected so it won't affect
// Apdex score and error rate.
app.MapPost("/orders/flaky", (OrderRequest request, OrderService orderService) =>
    orderService.ProcessFlakyOrder(request));

app.Run();
