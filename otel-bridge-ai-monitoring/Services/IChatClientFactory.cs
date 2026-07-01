// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.AI;

namespace OtelBridgeAiMonitoring.Services;

/// <summary>
/// Builds OpenTelemetry-instrumented <see cref="IChatClient"/> instances backed by either
/// Azure OpenAI or OpenAI, selected by configuration.
/// </summary>
public interface IChatClientFactory
{
    /// <summary>
    /// Creates an instrumented chat client for the configured provider.
    /// </summary>
    /// <param name="useBogusKey">
    /// When true, an intentionally invalid API key is used so the call fails — useful for
    /// demonstrating how errored GenAI calls are reported.
    /// </param>
    IChatClient Create(bool useBogusKey = false);
}
