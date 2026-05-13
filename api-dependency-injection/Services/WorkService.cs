// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Api.Agent;

namespace ApiDependencyInjection.Services;

/// <summary>
/// Demonstrates the correct way to use a DI-injected <see cref="IAgent"/> from a
/// singleton service.
///
/// The rule: inject IAgent once and keep it in a field, but fetch
/// <see cref="IAgent.CurrentTransaction"/> and <see cref="ITransaction.CurrentSpan"/>
/// fresh on every method call. Those properties return the transaction and span for
/// the CURRENT request — capturing them in a field would poison every subsequent
/// request that resolves this singleton.
///
/// Because this method is invoked from an ASP.NET Core minimal API endpoint, the
/// agent's built-in auto-instrumentation has already created a transaction for the
/// request. Inside DoWork the agent's notion of "current transaction" is the one
/// tied to the currently-executing request, carried via AsyncLocal under the hood.
/// </summary>
public class WorkService
{
    private readonly IAgent _agent;

    public WorkService(IAgent agent)
    {
        _agent = agent;
    }

    public IResult DoWork()
    {
        // Fetch CurrentTransaction FRESH here — do not cache this value in a field.
        // Each request gets its own transaction; the AsyncLocal context on this call
        // resolves to the transaction for THIS request.
        var transaction = _agent.CurrentTransaction;
        transaction.AddCustomAttribute("workService.lifetime", "singleton");

        // Same rule applies to CurrentSpan — fetch it per method call.
        var span = transaction.CurrentSpan;
        span.AddCustomAttribute("workService.step", "DoWork");

        return Results.Ok(new { message = "Work completed (singleton service)" });
    }
}

/// <summary>
/// Demonstrates that the rule is about USAGE, not the consuming service's lifetime.
/// A scoped service consuming the singleton IAgent must still fetch CurrentTransaction
/// per method call.
///
/// A scoped service is created once per request, so in theory capturing
/// CurrentTransaction in a field at construction would be safe here. In practice we
/// still don't do that, because (a) it would diverge from the singleton pattern and
/// invite the bug if someone later changes the registration to singleton, and (b) any
/// method called outside an active transaction (e.g. during shutdown) would see a
/// stale reference.
/// </summary>
public class ScopedWorkService
{
    private readonly IAgent _agent;

    public ScopedWorkService(IAgent agent)
    {
        _agent = agent;
    }

    public IResult DoWork()
    {
        var transaction = _agent.CurrentTransaction;
        transaction.AddCustomAttribute("workService.lifetime", "scoped");

        return Results.Ok(new { message = "Work completed (scoped service)" });
    }
}
