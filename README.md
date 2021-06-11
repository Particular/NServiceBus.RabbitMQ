# RabbitMQ Transport for NServiceBus

The [NServiceBus.RabbitMQ NuGet package](https://www.nuget.org/packages/NServiceBus.RabbitMQ) provides support for sending messages over [RabbitMQ](http://www.rabbitmq.com/).

For more information, see the [documentation](https://docs.particular.net/nservicebus/rabbitmq/).

## Running tests locally

All tests expects a connection string to be set via the `RabbitMQTransport_ConnectionString` environment variable.

For developers using Docker containers, the following docker command will quickly setup a container configured to use the default port:

`docker run -d --hostname my-rabbit --name my-rabbit -p 5672:5672 -p 15672:15672 rabbitmq:3-management`

With this setup, the connection string to use would be `host=localhost`.

## Setting up a docker cluster

A 3-node RabbitMQ cluster with a HAProxy load balancer in front and by default mirroring all queues accross all 3 nodes can be set up using docker for testing or development purposes by running the following script:

Setup cluster network:
```cmd
docker network create --driver bridge rabbitnet
```

Setup cluster:
```cmd
docker run -d --network rabbitnet --hostname rabbit1 --name rabbit1 -v rabbitmq-data:/var/lib/rabbitmq rabbitmq:3-management
docker run -d --network rabbitnet --hostname rabbit2 --name rabbit2 -v rabbitmq-data:/var/lib/rabbitmq rabbitmq:3-management
docker run -d --network rabbitnet --hostname rabbit3 --name rabbit3 -v rabbitmq-data:/var/lib/rabbitmq rabbitmq:3-management

docker exec rabbit2 rabbitmqctl stop_app
docker exec rabbit2 rabbitmqctl join_cluster rabbit@rabbit1
docker exec rabbit2 rabbitmqctl start_app

docker exec rabbit3 rabbitmqctl stop_app
docker exec rabbit3 rabbitmqctl join_cluster rabbit@rabbit1
docker exec rabbit3 rabbitmqctl start_app
```

Setup classic queue mirroring:

Note that [mirroring of classic queues](https://www.rabbitmq.com/ha.html) will be removed in a future version of RabbitMQ. Consider using [quorum queues](https://www.rabbitmq.com/quorum-queues.html) instead.

```cmd
docker exec rabbit1 rabbitmqctl set_policy ha-all "\." '{"ha-mode":"exactly","ha-params":2,"ha-sync-mode":"automatic"}'
```


Create `haproxy.cfg` file for configurating HAProxy:
```txt
global
        log 127.0.0.1   local1
        maxconn 4096
 
defaults
        log     global
        mode    tcp
        option  tcplog
        retries 3
        option redispatch
        maxconn 2000
        timeout connect 5000
        timeout client 50000
        timeout server 50000
 
listen  stats
        bind *:1936
        mode http
        stats enable
        stats hide-version
        stats realm Haproxy\ Statistics
        stats uri /
 
listen rabbitmq
        bind *:5672
        mode            tcp
        balance         roundrobin
        timeout client  3h
        timeout server  3h
        option          clitcpka
        server          rabbit1 rabbit1:5672  check inter 5s rise 2 fall 3
        server          rabbit2 rabbit2:5672  check inter 5s rise 2 fall 3
        server          rabbit3 rabbit3:5672  check inter 5s rise 2 fall 3

listen mgmt
        bind *:15672
        mode            tcp
        balance         roundrobin
        timeout client  3h
        timeout server  3h
        option          clitcpka
        server          rabbit1 rabbit1:15672  check inter 5s rise 2 fall 3
        server          rabbit2 rabbit2:15672  check inter 5s rise 2 fall 3
        server          rabbit3 rabbit3:15672  check inter 5s rise 2 fall 3
```

Setup HAProxy container, note correct the path where `haproxy.cfg` is saved.
```cmd
docker run -d --network rabbitnet --hostname rabbitha --name rabbitha -p 15672:15672 -p 5672:5672 -v ./haproxy.cfg:/usr/local/etc/haproxy/haproxy.cfg:ro haproxy:1.7
```

Setup quorem queues:

[quorum queues](https://www.rabbitmq.com/quorum-queues.html)

After all these commands have run, a 3-node RabbitMQ cluster will be running that should be accessible via the load balancer.
