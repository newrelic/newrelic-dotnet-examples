using Azure.Messaging.ServiceBus;
using NewRelic.Api.Agent;


Console.WriteLine("Hello from Sender");

// Get Service Bus config from environment
string connectionString = Environment.GetEnvironmentVariable("AZURE_SERVICE_BUS_CONNECTION_STRING") ?? string.Empty;
string queueName = Environment.GetEnvironmentVariable("AZURE_SERVICE_BUS_QUEUE_NAME") ?? string.Empty;

if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(queueName))
{
    Console.WriteLine("You must set the following environment variables:");
    Console.WriteLine("AZURE_SERVICE_BUS_CONNECTION_STRING");
    Console.WriteLine("AZURE_SERVICE_BUS_QUEUE_NAME");
    return;
}

// Create a ServiceBusClient that will authenticate using a connection string
await using ServiceBusClient client = new(connectionString);

var startTime = DateTime.Now;
while (DateTime.Now - startTime < TimeSpan.FromMinutes(5))
{
    await SendAMessage(client, queueName, $"Message: {Guid.NewGuid().ToString()}");
    Thread.Sleep(5000);
}

[Transaction]
static async Task SendAMessage(ServiceBusClient client, string queueName, string messageText)
{
    Console.WriteLine($"Sending message with text: '{messageText}'");

    // create the sender
    ServiceBusSender sender = client.CreateSender(queueName);

    // create a message that we can send. UTF-8 encoding is used when providing a string.
    ServiceBusMessage message = new(messageText);

    NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction.InsertDistributedTraceHeaders(message.ApplicationProperties, Setter);

    // send the message
    await sender.SendMessageAsync(message);
}

static void Setter(IDictionary<string, object> applicationProperties, string key, string value)
{
    Console.WriteLine($"Inserting tracing data: {key}, {value}");
    applicationProperties.Add(key, value);
}
