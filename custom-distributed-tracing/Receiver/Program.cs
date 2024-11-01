using Azure.Messaging.ServiceBus;
using NewRelic.Api.Agent;

Console.WriteLine("Hello from Reciever");

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
    await ReceiveAMessage(client, queueName);
    Thread.Sleep(5000);
}

[Transaction]
static async Task ReceiveAMessage(ServiceBusClient client, string queueName)
{
    // create the sender
    ServiceBusReceiver receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete});

    // the received message is a different type as it contains some service set properties
    Console.WriteLine("Attempting to receive message...");
    ServiceBusReceivedMessage receivedMessage = await receiver.ReceiveMessageAsync();

    // Accept the incoming tracing data
    NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction.AcceptDistributedTraceHeaders(receivedMessage.ApplicationProperties, Getter, TransportType.Queue);

    // get the message body as a string
    string body = receivedMessage.Body.ToString();
    Console.WriteLine($"Received message body: {body}");
}

static IEnumerable<string> Getter(IReadOnlyDictionary<string, object> applicationProperties, string key)
{
    var data = new List<string>();
    if (applicationProperties == null)
    {
        return data;
    }
    object value;
    if (applicationProperties.TryGetValue(key, out value))
    {
        if (value != null)
        {
            Console.WriteLine($"key=value: {key} = {value}");
            data.Add(value.ToString());
        }
    }
    return data;
}
