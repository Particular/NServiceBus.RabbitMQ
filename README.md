# RabbitMQ Transport for NServiceBus

Install this to enable NServiceBus to facilitate messaging over RabbitMQ

## Installation

Before doing anything else, make sure you have RabbitMQ up and running in your environment. Also make sure it is accessible from all the machines in your setup.

1. Add NServiceBus.RabbitMQ to your project(s). The easiest way to do that is by installing the [NServiceBus.RabbitMQ nuget package](https://www.nuget.org/packages/NServiceBus.RabbitMQ).

2. In your app.config make sure to provides the necessary connection information needed to communicate to the RabbitMQ server. A typical setup would be:

````xml
<connectionStrings>
  <add name="NServiceBus/Transport" connectionString="host=localhost"/>
</connectionStrings>
````

## Samples

See https://github.com/Particular/NServiceBus.RabbitMQ.Samples
