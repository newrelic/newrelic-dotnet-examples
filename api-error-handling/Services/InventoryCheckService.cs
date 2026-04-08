// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace ApiErrorHandling.Services;

/// <summary>
/// Simulates an inventory service to demonstrate NoticeError behavior both inside
/// and outside of a transaction context.
/// </summary>
public class InventoryCheckService
{
    /// <summary>
    /// Simulates a downstream inventory check that fails. This method is called from within
    /// OrderService.ProcessRiskyOrder(), which executes inside a transaction created by the
    /// agent's built-in ASP.NET Core instrumentation. Since this method is already invoked
    /// within an existing transaction, it does not need its own [Transaction] attribute.
    /// </summary>
    public bool CheckInventory(string productId)
    {
        // Simulate a downstream service failure
        throw new InvalidOperationException(
            $"Inventory service unavailable: connection refused while checking product '{productId}'");
    }

}
