# New Relic .NET Agent Samples - Custom Distributed Tracing

This folder contains a pair of sample applications to demonstrate how to use the .NET Agent's [distributed tracing APIs](https://docs.newrelic.com/docs/apm/agents/net-agent/configuration/distributed-tracing-net-agent/#manual-instrumentation) to link two instrumented services that communicate across a channel not automatically instrumented by the agent.
The agent is added to each application via the `NewRelic.Agent` NuGet package.

The channel used for demonstration purposes is [Azure Service Bus](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/messaging.servicebus-readme?view=azure-dotnet).
The custom distributed tracing APIs need access to a "carrier" for the tracing data being added to outgoing messages and recieved from incoming messages.  For Azure Service Bus messages, the [`ApplicationProperties`](https://learn.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebusmessage.applicationproperties?view=azure-dotnet) key/value store is used. Refer to the example code for details.

In order to use this example, you will need the following:

1. A New Relic APM license key
2. An Azure Service Bus instance with at least one queue configured.
3. The [connection string](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-dotnet-how-to-use-topics-subscriptions?tabs=connection-string#get-the-connection-string) for your Azure Service Bus instance
4. The name of the queue you want to use.

Steps for testing:

1. Create a `docker.env` file to set your specific values for your New Relic license key, sending and receiving application names, and Azure Service Bus connection string and queue name.
2. `docker-compose --env=docker.env build`
3. `docker-compose --env-docker.env up`

The Sender and Receiver applications will each run for five minutes. You should see these applications linked in the Distributed Tracing UI in New Relic APM after a few minutes.
