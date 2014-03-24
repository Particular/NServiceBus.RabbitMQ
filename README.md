# RabbitMQ Transport for NServiceBus

Install this to enable NServiceBus to facilitate messaging over RabbitMQ

## Installation

Follow the instructions here: http://docs.particular.net/servicecontrol/multi-transport-support

In your app.config make sure to provides the necessary connection information needed to communicate to the RabbitMQ server. A typical setup would be:

````xml
<connectionStrings>
  <add name="NServiceBus/Transport" connectionString="host=localhost"/>
</connectionStrings>
````

## Samples

See https://github.com/Particular/NServiceBus.RabbitMQ.Samples
