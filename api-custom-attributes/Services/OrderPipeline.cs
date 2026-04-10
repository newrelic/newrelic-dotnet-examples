// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using NewRelic.Api.Agent;

namespace ApiCustomAttributes.Services;

/// <summary>
/// Demonstrates adding custom attributes to transactions and spans, setting transaction names,
/// and associating a user ID with a transaction.
///
/// These methods are called from ASP.NET Core minimal API endpoints. The agent's built-in
/// ASP.NET Core instrumentation automatically creates a transaction for each incoming HTTP
/// request, so these methods do NOT need the [Transaction] attribute.
///
/// Methods decorated with [Trace] create child spans within the auto-instrumented transaction,
/// providing detailed breakdowns of where time is spent.
///
/// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#ITransaction.AddCustomAttribute
/// See: https://docs.newrelic.com/docs/apm/agents/net-agent/custom-instrumentation/custom-instrumentation-attributes-net/
/// </summary>
public class OrderPipeline
{
    private readonly PricingService _pricingService;

    public OrderPipeline(PricingService pricingService)
    {
        _pricingService = pricingService;
    }

    /// <summary>
    /// Processes an order through multiple traced stages. Demonstrates:
    ///   - SetTransactionName to override the auto-generated transaction name
    ///   - ITransaction.SetUserId to associate the transaction with a customer
    ///   - ITransaction.AddCustomAttribute for transaction-level context (builder pattern)
    ///
    /// SetTransactionName must be called inside a transaction. If called multiple times within
    /// the same transaction, each call overwrites the previous one — the last call wins.
    ///
    /// Do not use unique values (URLs, session IDs, etc.) in transaction names. Use
    /// AddCustomAttribute for such data instead. Do not create more than 1000 unique
    /// transaction names.
    ///
    /// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#SetTransactionName
    /// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#SetUserId
    /// </summary>
    public IResult ProcessOrder(string customerId, OrderRequest request)
    {
        // Override the auto-generated transaction name with something meaningful.
        // The category becomes part of the metric name prefix. The name is limited to 255 characters.
        NewRelic.Api.Agent.NewRelic.SetTransactionName("Custom", "ProcessOrder");

        var transaction = NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction;

        // Associate a user ID with this transaction. The value is stored as the agent
        // attribute "enduser.id" and is surfaced in Errors Inbox, where it powers the
        // "Users impacted" metric — showing how many unique users are affected by each
        // error group.
        // Requires agent version 10.9.0+. Null, empty, and whitespace values are ignored.
        // See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#SetUserId
        // See: https://docs.newrelic.com/docs/errors-inbox/error-impacted/#set-users
        transaction.SetUserId(customerId);

        // Add custom attributes to the transaction using the builder pattern.
        // These are reported in errors and transaction traces.
        // Keys are limited to 255-bytes.
        //
        // Supported value types (see docs link below for full details):
        //   - Integral types: byte, Int16, Int32, Int64, sbyte, UInt16, UInt32, UInt64
        //   - Decimal types: float, double, decimal
        //   - string (truncated at 4096 characters at ingest; empty strings supported)
        //   - bool
        //   - DateTime (serialized as ISO-8601 with timezone)
        //   - TimeSpan (serialized as decimal seconds)
        //   - arrays, Lists, and other IEnumerable types (serialized as JSON array;
        //     null elements filtered out; requires agent version 10.50.0+)
        //   - All other types: ToString() is called
        //
        // See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#ITransaction.AddCustomAttribute
        // Scalar types
        transaction
            .AddCustomAttribute("orderId", request.OrderId)                              // string
            .AddCustomAttribute("itemCount", request.Items.Count)                        // int
            .AddCustomAttribute("region", request.Region)                                // string
            .AddCustomAttribute("isExpressShipping", request.Region == "US")             // bool
            .AddCustomAttribute("orderTimestamp", DateTime.UtcNow)                       // DateTime → ISO-8601
            .AddCustomAttribute("processingWindow", TimeSpan.FromMinutes(30));           // TimeSpan → seconds

        // Validate: reject empty orders so we can observe how SetUserId and custom
        // attributes appear on error events in the UI.
        if (request.Items.Count == 0)
        {
            NewRelic.Api.Agent.NewRelic.NoticeError(
                "Order validation failed: order must contain at least one item",
                new Dictionary<string, object>
                {
                    { "orderId", request.OrderId },
                    { "customerId", customerId }
                });

            return Results.BadRequest(new { error = "Order must contain at least one item" });
        }

        // Array types (requires agent version 10.50.0+)
        // All IEnumerable types are serialized as JSON arrays. Null elements are filtered out.
        // Elements are individually converted using the same type rules as scalar values.
        transaction
            .AddCustomAttribute("productIds", request.Items.Select(i => i.ProductId).ToArray())      // projected string[] via LINQ
            .AddCustomAttribute("quantities", new List<int> { 2, 1, 3 })                             // List<int>
            .AddCustomAttribute("prices", request.Items.Select(i => i.Price));                        // IEnumerable<decimal> (lazy)

        // Process the order through traced stages.
        // Each [Trace]-decorated method creates a child span within this transaction.
        ValidateOrder(request);
        var total = CalculatePrice(request);
        PersistOrder(request, total);

        return Results.Ok(new { message = "Order processed", orderId = request.OrderId, total });
    }

    /// <summary>
    /// Demonstrates [Trace] to create a child span, and ISpan.AddCustomAttribute / ISpan.SetName.
    ///
    /// The [Trace] attribute instructs the agent to time this method invocation within the
    /// parent transaction. When invoked outside a transaction, no measurements are recorded.
    ///
    /// ISpan.AddCustomAttribute returns ISpan for method chaining (builder pattern).
    /// ISpan.SetName changes the name of the segment/span reported to New Relic.
    ///
    /// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#ISpan.AddCustomAttribute
    /// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#SetName
    /// </summary>
    [Trace]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ValidateOrder(OrderRequest request)
    {
        var span = NewRelic.Api.Agent.NewRelic.GetAgent().CurrentSpan;

        // Rename the span and add custom attributes using the builder pattern.
        // SetName requires agent version 10.1.0+.
        span
            .SetName("Validate")
            .AddCustomAttribute("validationType", "full")
            .AddCustomAttribute("itemCount", request.Items.Count);

        // Simulate validation work
        Thread.Sleep(10);
    }

    /// <summary>
    /// Demonstrates a [Trace] span that calls into another traced method, creating nested spans:
    /// [Transaction] → CalculatePrice [Trace] → GetPrice [Trace]
    ///
    /// This nesting is visible in the distributed trace waterfall.
    /// </summary>
    [Trace]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private decimal CalculatePrice(OrderRequest request)
    {
        var span = NewRelic.Api.Agent.NewRelic.GetAgent().CurrentSpan;
        span.SetName("CalculatePrice");

        var baseTotal = _pricingService.GetPrice(request.Items);

        // Apply a regional discount
        var discount = request.Region == "US" ? 0.0m : 0.05m;
        var total = baseTotal * (1 - discount);

        span.AddCustomAttribute("baseTotal", baseTotal)
            .AddCustomAttribute("discount", discount)
            .AddCustomAttribute("finalTotal", total);

        return total;
    }

    /// <summary>
    /// Demonstrates a [Trace] span for a simulated persistence operation.
    /// </summary>
    [Trace]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void PersistOrder(OrderRequest request, decimal total)
    {
        var span = NewRelic.Api.Agent.NewRelic.GetAgent().CurrentSpan;
        span
            .SetName("PersistOrder")
            .AddCustomAttribute("orderId", request.OrderId)
            .AddCustomAttribute("total", total);

        // Simulate database write
        Thread.Sleep(25);
    }
}

public record OrderRequest(string OrderId, string Region, List<OrderItem> Items);

public record OrderItem(string ProductId, string Name, decimal Price, int Quantity);
