// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using ApiLogsInContext.Logging;
using NewRelic.Api.Agent;

namespace ApiLogsInContext.Services;

/// <summary>
/// Demonstrates how TraceId and SpanId from the New Relic linking metadata change as execution
/// moves across [Trace] span boundaries within a single transaction.
///
/// These methods are called from ASP.NET Core minimal API endpoints. The agent's built-in
/// ASP.NET Core instrumentation automatically creates a transaction for each incoming HTTP
/// request, so these methods do NOT need the [Transaction] attribute.
///
/// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#ITraceMetadata
/// See: https://docs.newrelic.com/docs/apm/agents/net-agent/custom-instrumentation/custom-instrumentation-attributes-net/
/// </summary>
public class OrderService
{
    private readonly NewRelicLogEnricher _enricher;

    public OrderService(NewRelicLogEnricher enricher)
    {
        _enricher = enricher;
    }

    /// <summary>
    /// Processes an order through multiple traced stages while logging trace metadata at each step.
    ///
    /// This method runs inside the auto-instrumented ASP.NET Core transaction created for the
    /// POST /orders request. It calls ValidateOrder() and FulfillOrder(), both decorated with
    /// [Trace], to create child spans. The log output shows:
    ///   - The full linking metadata (entity.guid, entity.name, hostname, trace.id, span.id)
    ///     at the start of the method — this is the root span of the transaction.
    ///   - TraceId stays constant across all [Trace] boundaries within this transaction.
    ///   - SpanId changes when execution enters a [Trace]-decorated method — it reflects the
    ///     currently executing span, not the root.
    ///   - After the child methods return, SpanId reverts to the root span's ID.
    /// </summary>
    public IResult ProcessOrder(OrderRequest request, ILogger logger)
    {
        // Log the full linking metadata at the root span. All five keys should be populated
        // because this executes inside the auto-instrumented ASP.NET Core transaction.
        var metadata = _enricher.GetLogContext();
        var (rootTraceId, rootSpanId, _) = _enricher.GetTraceContext();

        logger.LogInformation(
            "ProcessOrder start — orderId={OrderId} customerId={CustomerId} trace.id={TraceId} span.id={SpanId} linkingMetadata={Metadata}",
            request.OrderId,
            request.CustomerId,
            rootTraceId,
            rootSpanId,
            string.Join(", ", metadata.Select(kvp => $"{kvp.Key}={kvp.Value}")));

        ValidateOrder(request, logger);
        FulfillOrder(request, logger);

        // TraceId is the same as at the start — it identifies the entire distributed trace.
        // SpanId is back to the root span's ID now that the child [Trace] methods have returned.
        var (endTraceId, endSpanId, _) = _enricher.GetTraceContext();
        logger.LogInformation(
            "ProcessOrder end — orderId={OrderId} trace.id={TraceId} span.id={SpanId} (same TraceId, SpanId reverted to root span)",
            request.OrderId,
            endTraceId,
            endSpanId);

        return Results.Ok(new { message = "Order processed", orderId = request.OrderId });
    }

    /// <summary>
    /// Validates an order. The [Trace] attribute creates a child span within the current
    /// transaction. The SpanId logged here is different from the root span's SpanId logged in
    /// ProcessOrder — this is the key teaching moment: SpanId always reflects the currently
    /// executing span, which changes at each [Trace] boundary.
    ///
    /// The TraceId logged here is identical to the one in ProcessOrder — TraceId is constant
    /// for all spans within a single distributed transaction.
    ///
    /// [MethodImpl(MethodImplOptions.NoInlining)] prevents the JIT compiler from inlining this
    /// method, which would prevent the agent from creating a child span for it.
    ///
    /// See: https://docs.newrelic.com/docs/apm/agents/net-agent/custom-instrumentation/custom-instrumentation-attributes-net/
    /// </summary>
    [Trace]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ValidateOrder(OrderRequest request, ILogger logger)
    {
        var (traceId, spanId, _) = _enricher.GetTraceContext();

        // SpanId is different from the root span — [Trace] created a new child span.
        // TraceId is the same as the root — it identifies the enclosing distributed trace.
        logger.LogInformation(
            "ValidateOrder — orderId={OrderId} trace.id={TraceId} span.id={SpanId} (new SpanId, same TraceId)",
            request.OrderId,
            traceId,
            spanId);
    }

    /// <summary>
    /// Fulfills an order. Same pattern as ValidateOrder — [Trace] creates another child span
    /// with its own SpanId, while TraceId remains constant across the transaction.
    /// </summary>
    [Trace]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void FulfillOrder(OrderRequest request, ILogger logger)
    {
        var (traceId, spanId, _) = _enricher.GetTraceContext();

        // SpanId is different from both the root span and the ValidateOrder span — each [Trace]
        // boundary produces a unique SpanId. TraceId is still the same.
        logger.LogInformation(
            "FulfillOrder — orderId={OrderId} trace.id={TraceId} span.id={SpanId} (new SpanId, same TraceId)",
            request.OrderId,
            traceId,
            spanId);
    }
}
