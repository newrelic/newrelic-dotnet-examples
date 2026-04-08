// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace ApiErrorHandling.Services;

/// <summary>
/// Demonstrates different NoticeError overloads within a web transaction.
///
/// These methods are called from ASP.NET Core minimal API endpoints. The agent's built-in
/// ASP.NET Core instrumentation automatically creates a transaction for each incoming HTTP
/// request, so these methods do NOT need the [Transaction] attribute — they already execute
/// within a transaction.
///
/// Important: for each transaction, the agent only retains the exception and attributes from the
/// first call to NoticeError().
///
/// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#NoticeError
/// </summary>
public class OrderService
{
    private readonly InventoryCheckService _inventoryCheckService;

    public OrderService(InventoryCheckService inventoryCheckService)
    {
        _inventoryCheckService = inventoryCheckService;
    }

    /// <summary>
    /// Demonstrates NoticeError(string, IDictionary&lt;string, object&gt;):
    /// reporting a validation error with a message string and custom attributes.
    ///
    /// The string overload creates both error events and error traces. Only the first 1023
    /// characters of the message are retained in error events, while error traces retain the
    /// full message.
    /// </summary>
    public IResult ProcessOrder(OrderRequest request)
    {
        // Validate the order
        if (request.Items is null || request.Items.Count == 0)
        {
            // Report a message-based error with custom attributes.
            // The IDictionary<string, object> overload allows both string and numeric attribute values.
            NewRelic.Api.Agent.NewRelic.NoticeError(
                "Order validation failed: order must contain at least one item",
                new Dictionary<string, object>
                {
                    { "orderId", request.OrderId },
                    { "customerId", request.CustomerId },
                    { "errorSource", "validation" }
                });

            return Results.BadRequest(new { error = "Order must contain at least one item" });
        }

        if (request.TotalAmount <= 0)
        {
            NewRelic.Api.Agent.NewRelic.NoticeError(
                $"Order validation failed: invalid total amount {request.TotalAmount}",
                new Dictionary<string, object>
                {
                    { "orderId", request.OrderId },
                    { "customerId", request.CustomerId },
                    { "totalAmount", request.TotalAmount },
                    { "errorSource", "validation" }
                });

            return Results.BadRequest(new { error = "Total amount must be greater than zero" });
        }

        return Results.Ok(new { message = "Order processed successfully", orderId = request.OrderId });
    }

    /// <summary>
    /// Demonstrates NoticeError(Exception, IDictionary&lt;string, object&gt;):
    /// catching an exception from a downstream service and reporting it with custom context.
    ///
    /// When passing an exception, the agent determines the innermost exception using
    /// Exception.GetBaseException() and reports that as the root cause. The entire stack trace
    /// from the top-level exception is reported.
    /// </summary>
    public IResult ProcessRiskyOrder(OrderRequest request)
    {
        try
        {
            _inventoryCheckService.CheckInventory(request.Items?.FirstOrDefault()?.ProductId ?? "unknown");
        }
        catch (Exception ex)
        {
            // Report the exception along with custom attributes for additional context.
            // The exception-based overload includes the stack trace automatically.
            NewRelic.Api.Agent.NewRelic.NoticeError(ex,
                new Dictionary<string, object>
                {
                    { "orderId", request.OrderId },
                    { "customerId", request.CustomerId },
                    { "failedOperation", "inventoryCheck" }
                });

            return Results.StatusCode(503);
        }

        return Results.Ok(new { message = "Risky order processed", orderId = request.OrderId });
    }

    /// <summary>
    /// Demonstrates NoticeError(string, IDictionary&lt;string, string&gt;, bool isExpected):
    /// reporting an error from a known-flaky dependency and marking it as expected.
    ///
    /// The isExpected parameter marks the error so it won't affect Apdex score and error rate.
    /// This is useful for known issues (e.g., a flaky third-party service) where errors are
    /// anticipated and should not trigger alerts.
    ///
    /// Note: the isExpected parameter is only available on the string-based overloads,
    /// not the exception-based overloads.
    /// </summary>
    public IResult ProcessFlakyOrder(OrderRequest request)
    {
        // Simulate a flaky external dependency that intermittently fails
        var random = new Random();
        if (random.Next(100) < 50)
        {
            // Report the error as expected using the string overload with isExpected: true.
            // This overload accepts IDictionary<string, string> for custom attributes.
            NewRelic.Api.Agent.NewRelic.NoticeError(
                "External payment gateway timed out after 30s",
                new Dictionary<string, string>
                {
                    { "orderId", request.OrderId },
                    { "customerId", request.CustomerId },
                    { "gateway", "stripe" },
                    { "errorSource", "externalDependency" }
                },
                isExpected: true);

            return Results.StatusCode(504);
        }

        return Results.Ok(new { message = "Flaky order processed", orderId = request.OrderId });
    }
}

public record OrderRequest(string OrderId, string CustomerId, decimal TotalAmount, List<OrderItem>? Items);

public record OrderItem(string ProductId, string Name, decimal Price, int Quantity);
