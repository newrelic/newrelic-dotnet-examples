// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Text;
using Confluent.Kafka;
using NewRelic.Api.Agent;

namespace KafkaCustomInstrumentation.Services;

/// <summary>
/// Demonstrates Pattern 2: a consumer where the transaction covers only the processing
/// work, not the time spent waiting for a message to arrive.
///
/// Use this pattern when the consumer acts as a trigger for work and you want APM to
/// reflect the cost of that work — for example, when a message kicks off a report
/// generation job or an ETL pipeline and you want to measure how long the job takes,
/// independent of how long the consumer polled before the message arrived.
///
/// In this pattern, consumer.Consume() is called OUTSIDE any transaction. As a result:
///   - The agent does NOT auto-instrument Consume(): no MessageBroker segment is created.
///   - Incoming distributed trace headers are NOT automatically accepted.
///
/// A [Transaction] is opened only when a message actually arrives, in ProcessMessage().
/// Because the agent never intercepted the Consume() call, AcceptDistributedTraceHeaders()
/// must be called manually to link this transaction back to the upstream producer trace.
///
/// See: https://docs.newrelic.com/docs/apm/agents/net-agent/custom-instrumentation/custom-instrumentation-attributes-net/
/// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#AcceptDistributedTraceHeaders
/// </summary>
public class ProcessingOnlyConsumerService : BackgroundService
{
    private const string TopicName = KafkaProducerService.ProcessingOnlyTopic;
    private const string ConsumerGroup = "processing-only-consumer-group";

    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger<ProcessingOnlyConsumerService> _logger;

    public ProcessingOnlyConsumerService(string bootstrapServers, ILogger<ProcessingOnlyConsumerService> logger)
    {
        _logger = logger;

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest
        };
        _consumer = new ConsumerBuilder<string, string>(config).Build();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() =>
        {
            _consumer.Subscribe(TopicName);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Consume() is called here, OUTSIDE any transaction. This is intentional:
                    // the transaction duration should reflect the cost of processing work, not
                    // the time spent waiting for a message. The call blocks until a message
                    // arrives or the cancellation token is signalled.
                    //
                    // Because no transaction is active, the agent will NOT create a
                    // MessageBroker segment and will NOT accept distributed trace headers.
                    // Both must be handled manually inside ProcessMessage().
                    var result = _consumer.Consume(stoppingToken);
                    ProcessMessage(result);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ConsumeException ex)
                {
                    // Transient broker errors (e.g., topic not yet created) should not
                    // crash the service. Log and retry after a brief delay.
                    _logger.LogWarning("[ProcessingOnly] Consume error: {Reason}. Retrying in 2s.", ex.Error.Reason);
                    Thread.Sleep(2000);
                }
            }
        }, stoppingToken);
    }

    /// <summary>
    /// Creates a transaction scoped to the processing of a single message. Because
    /// Consume() ran outside a transaction, two things that would otherwise be automatic
    /// must be done manually:
    ///
    ///   1. AcceptDistributedTraceHeaders() — links this transaction back to the producer's
    ///      trace in New Relic's distributed tracing view. Without this call the consumer
    ///      transaction appears as an isolated, unconnected transaction.
    ///
    ///   2. SetTransactionName() — gives the transaction a meaningful name. In Pattern 1
    ///      the MessageBroker segment makes the transaction identifiable; here there is no
    ///      such segment, so an explicit name is required.
    ///
    /// [Transaction] + [MethodImpl(MethodImplOptions.NoInlining)] ensure the agent creates
    /// a new transaction for each message and that the method boundary is preserved.
    ///
    /// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#AcceptDistributedTraceHeaders
    /// </summary>
    [Transaction]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ProcessMessage(ConsumeResult<string, string> result)
    {
        var transaction = NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction;

        // Manually accept the distributed trace headers from the Kafka message. The getter
        // delegate receives the Headers collection and the header key to look up, returning
        // the matching value as a string array (or null if the key is absent).
        //
        // This is the step the agent performs automatically in Pattern 1 when Consume() is
        // called inside a transaction. Without this call, no distributed tracing link exists
        // between the producer HTTP transaction and this consumer transaction.
        //
        // TransportType.Kafka identifies the transport in the recorded span.
        //
        // See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#AcceptDistributedTraceHeaders
        transaction.AcceptDistributedTraceHeaders(
            result.Message.Headers,
            (headers, key) =>
            {
                var header = headers?.FirstOrDefault(h => h.Key == key);
                return header is null
                    ? null!
                    : new[] { Encoding.UTF8.GetString(header.GetValueBytes()) };
            },
            TransportType.Kafka);

        // Name the transaction after the topic. Without the MessageBroker segment that
        // Pattern 1 provides, an explicit name is the primary way to identify these
        // transactions in the APM UI and NRQL queries.
        // See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#SetTransactionName
        NewRelic.Api.Agent.NewRelic.SetTransactionName("Kafka", $"ProcessingOnly/{TopicName}");

        // Add Kafka delivery metadata as custom attributes so they are queryable in NRQL.
        // See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#ITransaction.AddCustomAttribute
        transaction
            .AddCustomAttribute("kafka.topic", result.Topic)
            .AddCustomAttribute("kafka.partition", result.Partition.Value)
            .AddCustomAttribute("kafka.offset", result.Offset.Value)
            .AddCustomAttribute("kafka.message.key", result.Message.Key ?? "(null)");

        // Simulate processing work.
        Thread.Sleep(Random.Shared.Next(250, 1000));

        _logger.LogInformation(
            "[ProcessingOnly] Processed message on {Topic} [{Partition}@{Offset}]: {Value}",
            result.Topic, result.Partition.Value, result.Offset.Value, result.Message.Value);
    }

    public override void Dispose()
    {
        _consumer.Close();
        _consumer.Dispose();
        base.Dispose();
    }
}
