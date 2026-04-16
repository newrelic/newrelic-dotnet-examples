// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using ApiLogsInContext.Logging;
using ApiLogsInContext.Services;

var builder = WebApplication.CreateBuilder(args);

// NewRelicLogEnricher is a service that wraps GetLinkingMetadata() and ITraceMetadata.
// It represents the integration point you would build in your own application to feed
// New Relic trace context into your logging pipeline — for example, as middleware that
// adds trace fields to every log scope, or as a helper injected into the classes that
// produce log entries. Inject it wherever log entries are produced.
builder.Services.AddSingleton<NewRelicLogEnricher>();
builder.Services.AddSingleton<OrderService>();

var app = builder.Build();

app.Urls.Add("http://localhost:5002");

// Log the linking metadata before any request is processed. At this point there is no
// active transaction, so all metadata values will be empty strings. This demonstrates
// that the API is safe to call at any time — it never throws.
var enricher = app.Services.GetRequiredService<NewRelicLogEnricher>();
var logger = app.Logger;
enricher.LogOutsideTransaction(logger);

// POST /orders
// Processes an order through multiple traced stages. Each [Trace]-decorated method in
// OrderService creates a child span within the auto-instrumented ASP.NET Core transaction,
// logging the TraceId and SpanId at each step to show how they change across span boundaries.
app.MapPost("/orders", (OrderRequest request, OrderService orderService, ILogger<Program> log) =>
    orderService.ProcessOrder(request, log));

// GET /metadata
// Returns the raw output of GetLinkingMetadata() and ITraceMetadata side by side.
// Because this endpoint is served by ASP.NET Core, the agent creates a transaction for the
// request, so the metadata values are populated when this handler executes.
app.MapGet("/metadata", (NewRelicLogEnricher enricher, ILogger<Program> log) =>
{
    var linkingMetadata = enricher.GetLogContext();
    var (traceId, spanId, isSampled) = enricher.GetTraceContext();

    // trace.id from GetLinkingMetadata() and TraceId from ITraceMetadata refer to the same
    // distributed trace — both identify the current transaction's trace.
    return Results.Ok(new
    {
        linkingMetadata,
        traceMetadata = new
        {
            traceId,
            spanId,
            isSampled
        }
    });
});

app.Run();

// Simple request record used by POST /orders.
public record OrderRequest(string OrderId, string CustomerId);
