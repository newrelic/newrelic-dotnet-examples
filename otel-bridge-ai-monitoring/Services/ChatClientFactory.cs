// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace OtelBridgeAiMonitoring.Services;

/// <summary>
/// Creates OpenTelemetry-instrumented <see cref="IChatClient"/> instances for Azure OpenAI or
/// OpenAI. The choice is driven by the "AI:Provider" configuration value ("azure" or "openai").
///
/// Both <c>AzureOpenAIClient</c> and <c>OpenAIClient</c> expose <c>GetChatClient</c>, which
/// returns the same <see cref="OpenAI.Chat.ChatClient"/> type, so a single
/// <see cref="WrapWithOpenTelemetry"/> path instruments either provider identically. The
/// instrumentation emits GenAI semantic-convention spans that the New Relic agent's
/// OpenTelemetry bridge converts into AI Monitoring data.
/// </summary>
public class ChatClientFactory : IChatClientFactory
{
    /// <summary>
    /// ActivitySource name the Microsoft.Extensions.AI OpenTelemetry instrumentation emits to.
    /// This is a custom (application-owned) name, so the New Relic OpenTelemetry bridge listens
    /// to it by default. Do not reuse the OpenAI SDK's built-in "OpenAI.ChatClient" source — the
    /// agent excludes that one by default.
    /// </summary>
    public const string ActivitySourceName = "OtelBridgeAiMonitoring";

    private const string BogusKeySuffix = "-bogus-key-for-testing";

    private readonly IConfiguration _config;

    public ChatClientFactory(IConfiguration config)
    {
        _config = config;
    }

    public IChatClient Create(bool useBogusKey = false)
    {
        var provider = _config["AI:Provider"] ?? "azure";

        var chatClient = provider.Equals("openai", StringComparison.OrdinalIgnoreCase)
            ? CreateOpenAIChatClient(useBogusKey)
            : CreateAzureChatClient(useBogusKey);

        return WrapWithOpenTelemetry(chatClient);
    }

    private OpenAI.Chat.ChatClient CreateAzureChatClient(bool useBogusKey)
    {
        var endpoint = _config["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is required when AI:Provider is 'azure'");
        var apiKey = _config["AzureOpenAI:ApiKey"]
            ?? throw new InvalidOperationException("AzureOpenAI:ApiKey is required when AI:Provider is 'azure'");
        var deploymentName = _config["AzureOpenAI:DeploymentName"] ?? "gpt-4o-mini";

        if (useBogusKey)
            apiKey += BogusKeySuffix;

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
        return azureClient.GetChatClient(deploymentName);
    }

    private OpenAI.Chat.ChatClient CreateOpenAIChatClient(bool useBogusKey)
    {
        var apiKey = _config["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey is required when AI:Provider is 'openai'");
        var model = _config["OpenAI:Model"] ?? "gpt-4o-mini";

        if (useBogusKey)
            apiKey += BogusKeySuffix;

        var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey));
        return openAIClient.GetChatClient(model);
    }

    private static IChatClient WrapWithOpenTelemetry(OpenAI.Chat.ChatClient chatClient)
    {
        return new ChatClientBuilder(chatClient.AsIChatClient())
            .UseOpenTelemetry(sourceName: ActivitySourceName, configure: options =>
            {
                // Captures prompt and completion content on the spans. The New Relic agent only
                // records that content when aiMonitoring.recordContent is also enabled.
                options.EnableSensitiveData = true;
            })
            .Build();
    }
}
