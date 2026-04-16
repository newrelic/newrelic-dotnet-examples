// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;

namespace ApiCustomEventsAndMetrics.Services;

/// <summary>
/// A simple in-memory cache that demonstrates IncrementCounter for tracking
/// cache hit/miss ratios.
///
/// IncrementCounter creates metric timeslices that increment by 1 per call. There
/// is no "increment by N" variant — use RecordMetric for arbitrary numeric values.
/// Counter names must start with "Custom/" for proper categorization.
///
/// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#IncrementCounter
/// </summary>
public class CacheService
{
    private readonly ConcurrentDictionary<string, object> _cache = new();

    /// <summary>
    /// Retrieves a value from the cache, incrementing hit or miss counters.
    /// </summary>
    public object? Get(string key)
    {
        if (_cache.TryGetValue(key, out var value))
        {
            // Increment the cache hit counter. Each call increments by exactly 1.
            NewRelic.Api.Agent.NewRelic.IncrementCounter("Custom/Cache/Hits");
            return value;
        }

        // Increment the cache miss counter.
        NewRelic.Api.Agent.NewRelic.IncrementCounter("Custom/Cache/Misses");
        return null;
    }

    /// <summary>
    /// Stores a value in the cache and increments the write counter.
    /// </summary>
    public void Set(string key, object value)
    {
        _cache[key] = value;
        NewRelic.Api.Agent.NewRelic.IncrementCounter("Custom/Cache/Writes");
    }
}
