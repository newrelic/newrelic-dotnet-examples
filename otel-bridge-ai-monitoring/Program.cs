// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.AI;
using OtelBridgeAiMonitoring.Hubs;
using OtelBridgeAiMonitoring.Services;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddSignalR();

// The factory builds an IChatClient backed by either Azure OpenAI or OpenAI, selected by the
// "AI:Provider" configuration value. Each client is wrapped with the Microsoft.Extensions.AI
// OpenTelemetry instrumentation, which emits GenAI spans (model, token counts, and—when
// EnableSensitiveData is true—prompt and completion content). The New Relic agent's
// OpenTelemetry bridge ingests those spans and surfaces them as AI Monitoring data; no OTLP
// exporter to New Relic is required.
builder.Services.AddSingleton<IChatClientFactory, ChatClientFactory>();
builder.Services.AddSingleton<IChatClient>(sp => sp.GetRequiredService<IChatClientFactory>().Create());

// Register the GenAI ActivitySource with OpenTelemetry. The console exporter prints spans to
// stdout for local visibility; the New Relic agent reads the spans independently via its bridge.
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(ChatClientFactory.ActivitySourceName)
        .AddConsoleExporter());

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapHub<ChatHub>("/chathub");

app.Run();
