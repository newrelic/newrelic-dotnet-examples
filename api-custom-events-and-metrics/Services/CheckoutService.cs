// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace ApiCustomEventsAndMetrics.Services;

/// <summary>
/// Demonstrates RecordCustomEvent and IncrementCounter for tracking business events
/// and failure counts.
///
/// RecordCustomEvent creates individual queryable events (like database rows) with
/// arbitrary attributes — ideal for business analytics where you need to filter and
/// facet by specific values.
///
/// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#RecordCustomEvent
/// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#IncrementCounter
/// </summary>
public class CheckoutService
{
    /// <summary>
    /// Processes a checkout request. This method is called from a minimal API endpoint,
    /// which means the ASP.NET Core auto-instrumentation already creates a transaction —
    /// no [Transaction] attribute is needed.
    ///
    /// Demonstrates RecordCustomEvent for recording business-level event data with attributes.
    /// </summary>
    public IResult ProcessCheckout(CheckoutRequest request)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            RecordCheckoutFailure("EmptyCart");
            return Results.BadRequest(new { error = "Cart is empty" });
        }

        if (request.TotalAmount <= 0)
        {
            RecordCheckoutFailure("InvalidAmount");
            return Results.BadRequest(new { error = "Total amount must be greater than zero" });
        }

        // RecordCustomEvent creates an event row queryable via NRQL. Unlike metrics (which
        // are pre-aggregated), custom events retain individual attribute values — you can
        // filter, facet, and alert on any attribute. The event type name must not start with
        // reserved prefixes and is limited to 255 characters.
        // See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#RecordCustomEvent
        NewRelic.Api.Agent.NewRelic.RecordCustomEvent("CheckoutCompleted", new Dictionary<string, object>
        {
            { "customerId", request.CustomerId },
            { "totalAmount", request.TotalAmount },
            { "itemCount", request.Items.Count },
            { "paymentMethod", request.PaymentMethod },
            { "region", request.Region }
        });

        return Results.Ok(new
        {
            orderId = Guid.NewGuid().ToString("N")[..8],
            status = "completed",
            customerId = request.CustomerId,
            totalAmount = request.TotalAmount
        });
    }

    /// <summary>
    /// Records a custom event and increments a counter for checkout failures.
    ///
    /// IncrementCounter increments by exactly 1 per call — there is no "increment by N"
    /// variant. Use RecordMetric if you need to record a duration value.
    ///
    /// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#IncrementCounter
    /// </summary>
    private void RecordCheckoutFailure(string reason)
    {
        // IncrementCounter creates a metric timeslice that counts occurrences. The counter
        // name must start with "Custom/". Useful for simple occurrence tracking without
        // needing the full custom event payload.
        NewRelic.Api.Agent.NewRelic.IncrementCounter("Custom/Checkout/Failures");

        // Also record a custom event so we can query failures by reason, customer, etc.
        NewRelic.Api.Agent.NewRelic.RecordCustomEvent("CheckoutFailed", new Dictionary<string, object>
        {
            { "reason", reason }
        });
    }
}

public record CheckoutRequest(
    string CustomerId,
    decimal TotalAmount,
    List<CheckoutItem>? Items,
    string PaymentMethod,
    string Region);

public record CheckoutItem(string ProductId, string Name, decimal Price, int Quantity);
