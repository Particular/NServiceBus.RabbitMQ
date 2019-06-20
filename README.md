# RabbitMQ Transport for NServiceBus

The [NServiceBus.RabbitMQ NuGet package](https://www.nuget.org/packages/NServiceBus.RabbitMQ) provides support for sending messages over [RabbitMQ](http://www.rabbitmq.com/).

For more information, see the [documentation](https://docs.particular.net/nservicebus/rabbitmq/).


## Running Tests Locally

By default both the AcceptanceTests and Tests projects will connect to a RabbitMQ broker on localhost listening on port `5672`

For developers using Docker containers the following docker command will quickly setup a container configured to use the default port:

`docker run -d --hostname my-rabbit --name my-rabbit -p 5672:5672 -p 15672:15672  rabbitmq:3-management`
