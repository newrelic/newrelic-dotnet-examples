// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Confluent.Kafka;

namespace KafkaCustomInstrumentation.Services;

/// <summary>
/// Produces messages to the consume-to-complete and processing-only Kafka topics,
/// injecting New Relic distributed trace headers into each message.
///
/// Both produce methods are invoked from ASP.NET Core endpoints, which the agent
/// auto-instruments as web transactions. This means a transaction is already active
/// when InsertDistributedTraceHeaders is called, so the outbound headers carry a valid
/// trace context that the consumer can use to link its transaction back to this one.
///
/// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#InsertDistributedTraceHeaders
/// </summary>
public class KafkaProducerService
{
    public const string ConsumeToCompleteTopic = "consume-to-complete-messages";
    public const string ProcessingOnlyTopic = "processing-only-messages";

    private readonly IProducer<string, string> _producer;

    public KafkaProducerService(string bootstrapServers)
    {
        var config = new ProducerConfig { BootstrapServers = bootstrapServers };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public Task ProduceConsumeToCompleteMessageAsync(string payload) =>
        ProduceWithTraceHeadersAsync(ConsumeToCompleteTopic, payload);

    public Task ProduceProcessingOnlyMessageAsync(string payload) =>
        ProduceWithTraceHeadersAsync(ProcessingOnlyTopic, payload);

    /// <summary>
    /// Injects distributed trace headers into the Kafka message before producing. The
    /// delegate receives the Kafka Headers collection as the carrier and adds each header
    /// as a UTF-8 byte array. On the consumer side, the headers are extracted and passed
    /// to AcceptDistributedTraceHeaders() — either automatically by the agent (Pattern 1)
    /// or manually in code (Pattern 2).
    ///
    /// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#InsertDistributedTraceHeaders
    /// </summary>
    private async Task ProduceWithTraceHeadersAsync(string topic, string payload)
    {
        var headers = new Headers();

        // Inject the current transaction's distributed trace context into the message
        // headers. The lambda receives the Headers collection as the carrier, plus the
        // key and value to add. Values are stored as UTF-8 bytes (Kafka header values
        // have no implied encoding, so UTF-8 is the conventional choice).
        NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction
            .InsertDistributedTraceHeaders(
                headers,
                (carrier, key, value) => carrier.Add(key, Encoding.UTF8.GetBytes(value)));

        var message = new Message<string, string>
        {
            Key = Guid.NewGuid().ToString(),
            Value = payload,
            Headers = headers
        };

        await _producer.ProduceAsync(topic, message);
    }
}
