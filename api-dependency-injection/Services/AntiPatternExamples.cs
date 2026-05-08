// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Api.Agent;

namespace ApiDependencyInjection.Services;

// -----------------------------------------------------------------------------
// DO NOT DO THIS
//
// The anti-patterns below are intentionally left UNREGISTERED with the DI
// container and kept in this file purely as documented counter-examples. The
// classes compile so that the exact shapes of the mistakes are visible, but
// they are never instantiated by the running app.
// -----------------------------------------------------------------------------

/// <summary>
/// ❌ ANTI-PATTERN: capturing <see cref="IAgent.CurrentTransaction"/> in a field.
///
/// Constructor injection runs once when the container first creates the service.
/// For a singleton, that happens during the FIRST request. CurrentTransaction at
/// that moment returns the transaction for request #1 — and every subsequent
/// request that resolves this singleton will use that stale, request-#1
/// transaction. Attributes, custom events, and errors get attached to a
/// transaction that has long since ended, and no data appears on the transactions
/// the user actually cares about.
///
/// Fix: store the IAgent, not the ITransaction. See <see cref="WorkService"/>.
/// </summary>
public class CapturedTransactionAntiPattern
{
    private readonly ITransaction _transaction;

    public CapturedTransactionAntiPattern(IAgent agent)
    {
        // ❌ Captures a single request's transaction and reuses it forever.
        _transaction = agent.CurrentTransaction;
    }

    public void DoWork()
    {
        _transaction.AddCustomAttribute("bug", "this attribute lands on the wrong transaction");
    }
}

/// <summary>
/// ❌ ANTI-PATTERN: capturing <see cref="ITransaction.CurrentSpan"/> in a field.
///
/// Same failure mode as <see cref="CapturedTransactionAntiPattern"/>, one level
/// deeper: the span belongs to a specific frame within a specific request. It
/// cannot be shared across requests or across unrelated method calls.
/// </summary>
public class CapturedSpanAntiPattern
{
    private readonly ISpan _span;

    public CapturedSpanAntiPattern(IAgent agent)
    {
        // ❌ Captures a single request's span and reuses it forever.
        _span = agent.CurrentTransaction.CurrentSpan;
    }

    public void DoWork()
    {
        _span.AddCustomAttribute("bug", "this attribute lands on the wrong span");
    }
}

/// <summary>
/// ❌ ANTI-PATTERN: calling <c>NewRelic.Api.Agent.NewRelic.GetAgent()</c> at
/// registration time instead of inside a factory.
///
/// The commented registration below resolves GetAgent() eagerly while the DI
/// container is being built. On some hosting models the profiler has not yet
/// finished attaching at that moment, so GetAgent() returns a no-op placeholder
/// that gets cached as the singleton forever. The app then runs with a silently
/// disabled API surface and no data reaches New Relic.
///
/// Fix: use a factory lambda so GetAgent() is deferred until first resolution —
/// see Program.cs.
/// <code>
/// // ❌ DO NOT DO THIS — eager resolution at registration time
/// builder.Services.AddSingleton<IAgent>(NewRelic.Api.Agent.NewRelic.GetAgent());
///
/// // ✅ DO THIS — factory defers GetAgent() until first resolution
/// builder.Services.AddSingleton<IAgent>(_ => NewRelic.Api.Agent.NewRelic.GetAgent());
/// </code>
/// </summary>
internal static class EagerRegistrationAntiPatternDoc
{
    // Intentionally empty — documentation only.
}
