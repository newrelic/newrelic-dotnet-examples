// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using KafkaCustomInstrumentation.Services;

var builder = WebApplication.CreateBuilder(args);

var bootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9092";

// KafkaProducerService is a singleton because IProducer<> is thread-safe and intended
// to be shared across requests for the lifetime of the application.
builder.Services.AddSingleton(_ => new KafkaProducerService(bootstrapServers));

// Both consumer services are long-running background workers. Factory lambdas are used
// here because the services take a plain string (bootstrapServers) alongside the
// DI-provided ILogger<T>, which requires manual wiring.
builder.Services.AddHostedService(sp =>
    new ConsumeToCompleteConsumerService(
        bootstrapServers,
        sp.GetRequiredService<ILogger<ConsumeToCompleteConsumerService>>()));

builder.Services.AddHostedService(sp =>
    new ProcessingOnlyConsumerService(
        bootstrapServers,
        sp.GetRequiredService<ILogger<ProcessingOnlyConsumerService>>()));

var app = builder.Build();

app.Urls.Add("http://localhost:5004");

// POST /consume-to-complete/produce
// Sends a message to the consume-to-complete topic. The ConsumeToCompleteConsumerService
// receives these messages inside a [Transaction]-decorated method, which enables the
// agent's Kafka auto-instrumentation of consumer.Consume() — creating a MessageBroker
// segment and automatically accepting distributed trace headers. The resulting transaction
// duration spans from consume to end of processing.
app.MapPost("/consume-to-complete/produce", async (HttpRequest request, KafkaProducerService producer) =>
{
    var message = await new StreamReader(request.Body).ReadToEndAsync();
    await producer.ProduceConsumeToCompleteMessageAsync(message);
    return Results.Ok(new { topic = KafkaProducerService.ConsumeToCompleteTopic, message });
});

// POST /processing-only/produce
// Sends a message to the processing-only topic. The ProcessingOnlyConsumerService calls
// Consume() outside any transaction so the transaction duration reflects only the
// processing work. Distributed trace headers must be accepted manually inside ProcessMessage().
app.MapPost("/processing-only/produce", async (HttpRequest request, KafkaProducerService producer) =>
{
    var message = await new StreamReader(request.Body).ReadToEndAsync();
    await producer.ProduceProcessingOnlyMessageAsync(message);
    return Results.Ok(new { topic = KafkaProducerService.ProcessingOnlyTopic, message });
});

app.Run();
