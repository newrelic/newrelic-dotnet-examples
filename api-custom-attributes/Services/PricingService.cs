// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using NewRelic.Api.Agent;

namespace ApiCustomAttributes.Services;

/// <summary>
/// Demonstrates nested [Trace] spans. This service is called from OrderPipeline.CalculatePrice(),
/// which is itself a [Trace] method, creating a second level of nesting:
///
///   auto-instrumented web transaction
///     → CalculatePrice [Trace]
///       → GetPrice [Trace]
///
/// This nesting is visible in the distributed trace waterfall as nested child spans.
/// </summary>
public class PricingService
{
    /// <summary>
    /// Calculates the base price for a list of items. Demonstrates a nested [Trace] span
    /// with custom attributes for the pricing calculation details.
    ///
    /// The [MethodImpl(MethodImplOptions.NoInlining)] attribute prevents the JIT compiler from
    /// inlining this method, which would prevent the [Trace] attribute from working.
    ///
    /// See: https://docs.newrelic.com/docs/apm/agents/net-agent/custom-instrumentation/custom-instrumentation-attributes-net/
    /// </summary>
    [Trace]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public decimal GetPrice(List<OrderItem> items)
    {
        var span = NewRelic.Api.Agent.NewRelic.GetAgent().CurrentSpan;
        span.SetName("GetPrice");

        var total = 0m;
        foreach (var item in items)
        {
            total += item.Price * item.Quantity;
        }

        span
            .AddCustomAttribute("lineItemCount", items.Count)
            .AddCustomAttribute("calculatedTotal", total);

        // Simulate pricing lookup latency
        Thread.Sleep(15);

        return total;
    }
}
