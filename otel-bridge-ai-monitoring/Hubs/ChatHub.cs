// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using NewRelic.Api.Agent;
using OtelBridgeAiMonitoring.Services;

namespace OtelBridgeAiMonitoring.Hubs;

/// <summary>
/// SignalR hub that relays browser chat messages to a language model through
/// <see cref="IChatClient"/>. The underlying provider (Azure OpenAI or OpenAI) is chosen by
/// configuration via <see cref="IChatClientFactory"/>.
///
/// Hub methods are decorated with <c>[Transaction]</c> so the New Relic agent starts a
/// transaction for each WebSocket invocation (SignalR invocations are not HTTP requests, so
/// they are not auto-instrumented). The GenAI spans produced by the OpenTelemetry-instrumented
/// chat client nest under that transaction and are ingested by the agent's OpenTelemetry bridge
/// as AI Monitoring data. The <c>[Transaction]</c> attribute is required because of SignalR's
/// hosting model — not because of AI Monitoring; a controller action or minimal-API endpoint
/// would be auto-instrumented without it.
/// </summary>
public class ChatHub : Hub
{
    private readonly IChatClient _chatClient;
    private readonly IChatClientFactory _chatClientFactory;

    public ChatHub(IChatClient chatClient, IChatClientFactory chatClientFactory)
    {
        _chatClient = chatClient;
        _chatClientFactory = chatClientFactory;
    }

    /// <summary>
    /// Standard (non-streaming) completion. Sends the full response back to the caller once the
    /// model finishes.
    /// </summary>
    [Transaction]
    public async Task SendMessage(string prompt)
    {
        try
        {
            var result = await _chatClient.GetResponseAsync(prompt);
            await Clients.Caller.SendAsync("ReceiveMessage", result.Text);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("ReceiveError", ex.Message);
        }
    }

    /// <summary>
    /// Streaming completion. Forwards each chunk to the caller as the model produces it.
    /// </summary>
    [Transaction]
    public async Task SendMessageStreaming(string prompt)
    {
        try
        {
            await foreach (var update in _chatClient.GetStreamingResponseAsync(prompt))
            {
                if (update.Text != null)
                    await Clients.Caller.SendAsync("ReceiveChunk", update.Text);
            }
            await Clients.Caller.SendAsync("StreamComplete");
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("ReceiveError", ex.Message);
        }
    }

    /// <summary>
    /// Deliberately calls the model with an invalid API key to demonstrate how a failed GenAI
    /// call is captured as an errored span. Useful for verifying error data appears in New Relic.
    /// </summary>
    [Transaction]
    public async Task SendMessageFailure(string prompt)
    {
        try
        {
            using var errorClient = _chatClientFactory.Create(useBogusKey: true);
            var result = await errorClient.GetResponseAsync(prompt);
            await Clients.Caller.SendAsync("ReceiveMessage", result.Text);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("ReceiveError", $"Expected error: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
