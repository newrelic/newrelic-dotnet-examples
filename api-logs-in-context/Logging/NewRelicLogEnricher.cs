// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Api.Agent;

namespace ApiLogsInContext.Logging;

/// <summary>
/// Wraps the New Relic linking metadata and trace metadata APIs to provide a simple interface
/// for enriching log entries with trace correlation context.
///
/// Use this service to manually inject New Relic trace context into log entries when using a
/// custom logging pipeline or a log framework not supported by the agent's automatic log
/// forwarding. The automatic agent log forwarding (agent 9.7.0+) handles this enrichment
/// automatically for supported frameworks.
///
/// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#GetLinkingMetadata
/// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#ITraceMetadata
/// </summary>
public class NewRelicLogEnricher
{
    /// <summary>
    /// Returns a dictionary of metadata for enriching a log entry with New Relic linking context.
    ///
    /// GetLinkingMetadata() returns all fields needed to correlate a log entry with New Relic
    /// entities and distributed traces. The dictionary includes:
    ///   - "entity.guid"  — unique identifier of this service entity in New Relic
    ///   - "entity.name"  — application name as configured (matches NEW_RELIC_APP_NAME)
    ///   - "hostname"     — name of the host machine running this process
    ///   - "trace.id"     — distributed trace ID; constant across all spans in one transaction
    ///   - "span.id"      — current span ID; changes at each [Trace] method boundary
    ///
    /// The dictionary only contains items with meaningful values — for example, if distributed
    /// tracing is disabled, "trace.id" will not be included.
    ///
    /// All values are empty strings (not null) outside a transaction. This means the call is
    /// always safe but the returned metadata will not correlate to any trace.
    ///
    /// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#GetLinkingMetadata
    /// See: https://docs.newrelic.com/docs/logs/logs-context/net-configure-logs-context-all/
    /// </summary>
    public IDictionary<string, string> GetLogContext()
    {
        return NewRelic.Api.Agent.NewRelic.GetAgent().GetLinkingMetadata();
    }

    /// <summary>
    /// Returns just the trace-specific metadata from ITraceMetadata.
    ///
    /// Use ITraceMetadata when you only need the distributed trace and span IDs — for example,
    /// to add them to an HTTP response header for debugging, or to correlate a log entry when
    /// you do not need the full entity context that GetLinkingMetadata() provides.
    ///
    /// IsSampled indicates whether the current transaction is being sampled for distributed
    /// tracing. When false, the trace may not appear in the distributed tracing UI, but logs
    /// are still forwarded independently of sampling.
    ///
    /// All values are empty strings (TraceId, SpanId) or false (IsSampled) outside a transaction.
    ///
    /// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#ITraceMetadata
    /// </summary>
    public (string TraceId, string SpanId, bool IsSampled) GetTraceContext()
    {
        var traceMetadata = NewRelic.Api.Agent.NewRelic.GetAgent().TraceMetadata;
        return (traceMetadata.TraceId, traceMetadata.SpanId, traceMetadata.IsSampled);
    }

    /// <summary>
    /// Logs the linking metadata in a context where no transaction is active — such as during
    /// application startup. Demonstrates that GetLinkingMetadata() is safe to call at any time:
    /// it returns an empty dictionary (or a dictionary with empty-string values) rather than
    /// throwing an exception. Log entries enriched with this metadata will not correlate to any
    /// distributed trace.
    /// </summary>
    public void LogOutsideTransaction(ILogger logger)
    {
        var metadata = GetLogContext();
        var (traceId, spanId, isSampled) = GetTraceContext();

        // Outside a transaction, GetLinkingMetadata() returns items with empty-string values
        // (or omits items that require an active transaction, such as trace.id and span.id).
        // This log entry will not correlate to any trace in New Relic.
        logger.LogInformation(
            "Startup log (no transaction): trace.id={TraceId} span.id={SpanId} isSampled={IsSampled} linkingMetadata={LinkingMetadata}",
            traceId,
            spanId,
            isSampled,
            string.Join(", ", metadata.Select(kvp => $"{kvp.Key}={kvp.Value}")));
    }
}
