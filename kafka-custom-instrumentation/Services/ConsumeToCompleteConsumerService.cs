// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using Confluent.Kafka;
using NewRelic.Api.Agent;

namespace KafkaCustomInstrumentation.Services;

/// <summary>
/// Demonstrates Pattern 1: a consumer where the transaction spans the full message
/// lifecycle from the Consume() call to the end of processing.
///
/// Use this pattern when consume lag is itself a KPI — for example, when your SLA
/// requires that messages are fully processed within N seconds of being produced, or
/// when you want APM to show both how long a message waited in the queue and how long
/// processing took, as two segments in the same transaction.
///
/// The key requirement for the .NET agent to auto-instrument consumer.Consume() is that
/// it must be called from inside an active transaction. In this pattern, ConsumeAndProcess()
/// is decorated with [Transaction], so a transaction is already open when Consume() executes.
/// The agent intercepts that call to:
///   - Create a MessageBroker segment (visible in the APM trace timeline)
///   - Automatically accept any distributed trace headers present in the message
///
/// If Consume() were called outside a transaction (as in the processing-only pattern),
/// neither of those things would happen.
///
/// See: https://docs.newrelic.com/docs/apm/agents/net-agent/custom-instrumentation/custom-instrumentation-attributes-net/
/// </summary>
public class ConsumeToCompleteConsumerService : BackgroundService
{
    private const string TopicName = KafkaProducerService.ConsumeToCompleteTopic;
    private const string ConsumerGroup = "consume-to-complete-consumer-group";

    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger<ConsumeToCompleteConsumerService> _logger;

    public ConsumeToCompleteConsumerService(string bootstrapServers, ILogger<ConsumeToCompleteConsumerService> logger)
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
                    // ConsumeAndProcess() opens a [Transaction] before calling Consume().
                    // The transaction being active is what enables the agent's Kafka
                    // auto-instrumentation of the Consume() call inside that method.
                    ConsumeAndProcess();
                }
                catch (ConsumeException ex)
                {
                    // Transient broker errors (e.g., topic not yet created) should not
                    // crash the service. Log and retry after a brief delay.
                    _logger.LogWarning("[ConsumeToComplete] Consume error: {Reason}. Retrying in 2s.", ex.Error.Reason);
                    Thread.Sleep(2000);
                }
            }
        }, stoppingToken);
    }

    /// <summary>
    /// Opens a transaction, calls Consume(), and — when a message is available —
    /// processes it. The [Transaction] attribute ensures the agent has an active
    /// transaction when Consume() executes, which is the prerequisite for Kafka
    /// auto-instrumentation to fire.
    ///
    /// When Consume() returns null (no message within the timeout), IgnoreTransaction()
    /// discards the transaction so that empty polling cycles do not appear in New Relic.
    ///
    /// [MethodImpl(MethodImplOptions.NoInlining)] prevents the JIT from inlining this
    /// method into the call site, which would remove the method boundary the agent
    /// relies on to apply the [Transaction] attribute.
    ///
    /// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#IgnoreTransaction
    /// </summary>
    [Transaction]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ConsumeAndProcess()
    {
        // Consume() is called here, inside an active [Transaction]. The agent intercepts
        // this call to create a MessageBroker segment and to accept any distributed trace
        // headers present in the message — the same result as calling
        // AcceptDistributedTraceHeaders() manually in Pattern 2, but automatic.
        var result = _consumer.Consume(TimeSpan.FromMilliseconds(200));

        if (result == null)
        {
            // No message arrived within the timeout. Discard this transaction so that
            // empty polling cycles do not appear in New Relic APM or affect metrics.
            // See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#IgnoreTransaction
            NewRelic.Api.Agent.NewRelic.IgnoreTransaction();
            return;
        }

        // Name the transaction so it is easy to identify in the APM UI and NRQL queries.
        // See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#SetTransactionName
        NewRelic.Api.Agent.NewRelic.SetTransactionName("Kafka", $"ConsumeToComplete/{TopicName}");

        ProcessMessage(result);
    }

    /// <summary>
    /// Processes an individual message as a child span of the ConsumeAndProcess()
    /// transaction. [Trace] creates a separate segment in the APM trace timeline,
    /// making it easy to see how processing time compares to consume time.
    /// </summary>
    [Trace]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ProcessMessage(ConsumeResult<string, string> result)
    {
        var transaction = NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction;

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
            "[ConsumeToComplete] Processed message on {Topic} [{Partition}@{Offset}]: {Value}",
            result.Topic, result.Partition.Value, result.Offset.Value, result.Message.Value);
    }

    public override void Dispose()
    {
        _consumer.Close();
        _consumer.Dispose();
        base.Dispose();
    }
}
