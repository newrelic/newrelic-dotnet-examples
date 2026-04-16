// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using ApiCustomEventsAndMetrics.Services;
using NewRelic.Api.Agent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<CheckoutService>();
builder.Services.AddSingleton<CacheService>();

// Register the background service. Unlike web request handlers (which are
// auto-instrumented), BackgroundService methods need the [Transaction] attribute
// for the agent to create a transaction.
builder.Services.AddHostedService<PeriodicMaintenanceService>();

var app = builder.Build();

app.Urls.Add("http://localhost:5003");

// POST /checkout
// Processes a checkout request. Demonstrates RecordCustomEvent and
// RecordResponseTimeMetric within an auto-instrumented web transaction.
app.MapPost("/checkout", (CheckoutRequest request, CheckoutService checkoutService) =>
    checkoutService.ProcessCheckout(request));

// GET /products/{id}
// Retrieves a product with cache hit/miss tracking via IncrementCounter.
app.MapGet("/products/{id}", (string id, CacheService cache) =>
{
    var cached = cache.Get(id);
    if (cached is not null)
    {
        return Results.Ok(cached);
    }

    // Simulate a slow database lookup on cache miss
    Thread.Sleep(Random.Shared.Next(20, 100));
    var product = new { id, name = $"Product {id}", price = Random.Shared.Next(10, 500) };
    cache.Set(id, product);
    return Results.Ok(product);
});

app.Run();

/// <summary>
/// A BackgroundService that periodically runs a simulated maintenance task and records
/// its duration using RecordMetric.
///
/// RecordMetric records a duration in seconds. Use it when you have a timing already
/// expressed in seconds (e.g., from Stopwatch.Elapsed.TotalSeconds). Use
/// RecordResponseTimeMetric when your timing is in milliseconds.
///
/// BackgroundService methods are not auto-instrumented by the .NET agent. The
/// [Transaction] attribute is required on the method that does the work so the
/// agent creates a transaction for metric correlation.
///
/// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#RecordMetric
/// See: https://docs.newrelic.com/docs/apm/agents/net-agent/custom-instrumentation/custom-instrumentation-attributes-net/
/// </summary>
public class PeriodicMaintenanceService : BackgroundService
{
    private static readonly Random Random = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            RunMaintenanceTask();
        }
    }

    /// <summary>
    /// Runs a simulated maintenance task and records its duration in seconds. This
    /// method has [Transaction] because BackgroundService is not auto-instrumented —
    /// without it, the agent would not create a transaction and the metric would not
    /// be correlated with APM data.
    ///
    /// [MethodImpl(MethodImplOptions.NoInlining)] prevents the JIT compiler from
    /// inlining this method, which would make the [Transaction] attribute ineffective.
    /// </summary>
    [Transaction]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void RunMaintenanceTask()
    {
        var stopwatch = Stopwatch.StartNew();

        // Simulate a maintenance operation (e.g., cache expiry sweep, index rebuild)
        Thread.Sleep(Random.Next(100, 500));

        stopwatch.Stop();

        // RecordMetric records a duration in seconds. The metric name must start with
        // "Custom/". Use this when your timing is naturally expressed in seconds; use
        // RecordResponseTimeMetric when your timing is in milliseconds.
        // See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#RecordMetric
        NewRelic.Api.Agent.NewRelic.RecordMetric(
            "Custom/Maintenance/TaskDuration",
            (float)stopwatch.Elapsed.TotalSeconds);
    }
}
