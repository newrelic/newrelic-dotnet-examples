// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace ApiErrorHandling;

/// <summary>
/// Demonstrates SetErrorGroupCallback, which provides a callback method that the agent uses
/// to determine the error group name for error events and traces. This name is used in the
/// Errors Inbox to group errors into logical groups.
///
/// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#SetErrorGroupCallback
///
/// Requires .NET agent version 10.9.0 or higher.
/// </summary>
public static class ErrorGroupCallback
{
    /// <summary>
    /// Assigns an error group name based on the error's attributes.
    ///
    /// The callback receives an IReadOnlyDictionary containing attribute data associated with each
    /// error event. The following attributes should always exist:
    ///   - error.class
    ///   - error.message
    ///   - stack_trace
    ///   - transactionName
    ///   - request.uri
    ///   - error.expected
    ///
    /// Additional attributes may be present depending on agent configuration and custom attributes.
    ///
    /// Return an empty string when the error can't be assigned to a logical error group.
    /// </summary>
    public static string DetermineErrorGroup(IReadOnlyDictionary<string, object> attributes)
    {
        // Group by exception class: categorize argument-related exceptions together
        if (attributes.TryGetValue("error.class", out var errorClass))
        {
            var className = errorClass.ToString() ?? string.Empty;

            if (className.Contains("ArgumentException") ||
                className.Contains("ArgumentNullException") ||
                className.Contains("ArgumentOutOfRangeException"))
            {
                return "ArgumentErrors";
            }

            if (className.Contains("InvalidOperationException"))
            {
                return "InvalidOperationErrors";
            }
        }

        // Group by transaction name: separate order-processing errors from other errors
        if (attributes.TryGetValue("transactionName", out var transactionName))
        {
            var txnName = transactionName.ToString() ?? string.Empty;

            if (txnName.Contains("ProcessOrder"))
            {
                return "OrderProcessingErrors";
            }
        }

        // Return empty string when no logical group applies
        return string.Empty;
    }
}
