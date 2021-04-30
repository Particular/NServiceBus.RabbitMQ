# RabbitMQ Transport for NServiceBus

The [NServiceBus.RabbitMQ NuGet package](https://www.nuget.org/packages/NServiceBus.RabbitMQ) provides support for sending messages over [RabbitMQ](http://www.rabbitmq.com/).

For more information, see the [documentation](https://docs.particular.net/nservicebus/rabbitmq/).

## Running tests locally

All tests expects a connection string to be set via the `RabbitMQTransport_ConnectionString` environment variable.

For developers using Docker containers, the following docker command will quickly setup a container configured to use the default port:

`docker run -d --hostname my-rabbit --name my-rabbit -p 5672:5672 -p 15672:15672 rabbitmq:3-management`

With this setup, the connection string to use would be `host=localhost`.

## Setting up a docker cluster

A 3-node cluster can be set up using docker for testing or development purposes by running the following script:

```
docker run -d --hostname rabbit1 --name rabbit1 -p 5672:5672 -p 15672:15672 -e RABBITMQ_ERLANG_COOKIE='asdfasdf' rabbitmq:3-management
docker run -d --hostname rabbit2 --name rabbit1 -p 5673:5672 -p 15673:15672 -e RABBITMQ_ERLANG_COOKIE='asdfasdf' rabbitmq:3-management
docker run -d --hostname rabbit3 --name rabbit1 -p 5674:5672 -p 15674:15672 -e RABBITMQ_ERLANG_COOKIE='asdfasdf' rabbitmq:3-management

docker exec rabbit2 rabbitmqctl stop_app
docker exec rabbit2 rabbitmqctl join_cluster rabbit@rabbit1
docker exec rabbit2 rabbitmqctl start_app

docker exec rabbit3 rabbitmqctl stop_app
docker exec rabbit3 rabbitmqctl join_cluster rabbit@rabbit1
docker exec rabbit3 rabbitmqctl start_app
```

After these commands have completed, a 3-node RabbitMQ cluster will be running.
