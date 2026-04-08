// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Api.Agent;

namespace ApiErrorHandling.Services;

/// <summary>
/// A BackgroundService that periodically checks the health of a downstream dependency.
///
/// Unlike ASP.NET Core web requests (which are auto-instrumented by the agent), background
/// services do NOT automatically run within a transaction. The [Transaction] attribute is
/// required here so the agent creates a transaction for the work performed in each iteration.
///
/// This service demonstrates NoticeError behavior both inside and outside a transaction by
/// alternating between two periodic checks:
///   - DoWorkInsideTransaction: uses [Transaction], so NoticeError reports within a transaction.
///   - DoWorkOutsideTransaction: no [Transaction], so NoticeError reports outside a transaction.
///
/// See: https://docs.newrelic.com/docs/apm/agents/net-agent/custom-instrumentation/custom-instrumentation-attributes-net/
/// </summary>
public class StartupHealthCheckService : BackgroundService
{
    private readonly ILogger<StartupHealthCheckService> _logger;

    public StartupHealthCheckService(ILogger<StartupHealthCheckService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            DoWorkInsideTransaction();

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            DoWorkOutsideTransaction();
        }
    }

    /// <summary>
    /// Periodic health check that runs INSIDE a transaction.
    ///
    /// The [Transaction] attribute is required here because BackgroundService methods are not
    /// auto-instrumented by the agent. Without it, the agent would not create a transaction
    /// and NoticeError would behave as if called outside of a transaction.
    ///
    /// Compare this to the web API endpoints in OrderService, which do NOT need [Transaction]
    /// because ASP.NET Core request handling is auto-instrumented.
    /// </summary>
    [Transaction]
    private void DoWorkInsideTransaction()
    {
        _logger.LogInformation("Running periodic health check (inside a [Transaction])");

        // Because this is called within a [Transaction], the error will be associated with
        // this transaction.
        NewRelic.Api.Agent.NewRelic.NoticeError(
            "Periodic health check: inventory service returned elevated latency (>2s)",
            new Dictionary<string, object>
            {
                { "checkType", "periodic" },
                { "service", "inventoryService" },
                { "context", "insideTransaction" }
            });

        _logger.LogWarning("Health check detected elevated latency (inside transaction)");
    }

    /// <summary>
    /// Periodic health check that runs OUTSIDE a transaction.
    ///
    /// This method intentionally does NOT have a [Transaction] attribute. When NoticeError is
    /// called here, there is no active transaction.
    ///
    /// From the docs: "If it is invoked outside of a transaction, the agent creates an error trace
    /// and categorizes the error in the New Relic UI as a NewRelic.Api.Agent.NoticeError API call.
    /// If invoked outside of a transaction, the NoticeError() call will not contribute to the
    /// error rate of an application."
    ///
    /// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#NoticeError
    /// For finding these errors in the UI: https://docs.newrelic.com/docs/errors-inbox/apm-tab/#outside-transactions
    /// </summary>
    private void DoWorkOutsideTransaction()
    {
        _logger.LogInformation("Running periodic health check (outside a transaction)");

        // Because there is no [Transaction] on this method and BackgroundService is not
        // auto-instrumented, NoticeError is called outside of any transaction.
        NewRelic.Api.Agent.NewRelic.NoticeError(
            "Periodic health check: inventory service returned HTTP 503",
            new Dictionary<string, object>
            {
                { "checkType", "periodic" },
                { "service", "inventoryService" },
                { "context", "outsideTransaction" }
            });

        _logger.LogWarning("Health check detected service unavailable (outside transaction)");
    }
}
