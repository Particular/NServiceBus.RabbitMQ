## Setup NServiceBus.RabbitMQ

- Install virtualization software like VirtualBox
- Create Win10 VM
 - Name VM something sane
- Install Erlang
- Install RabbitMQ

Determine whether the RabbitMQ install worked
 - Verify that it's available to anything other than localhost
 - If it didn't work, we need the workaround.. TODO
	
Open the rabbit cmd prompt as elevated
 - Manually navigate to "C:\programfiles\rabbitMQ Server\rabbitmq_server-3.6.1\sbin"
	
Turn on RabbitMQ management site at http://localhost:15672
 - rabbitmq-plugins enable rabbitmq_management

Create the rabbitmq.config file in user appdata ROAMING/RabbitMQ/rabbitmq.config (This will allow you to use the loopback credentials in a connection string)
 - with this text: "[{rabbit, [{loopback_users, []}]}]."

To force rabbit to use the config file, run the following commands
 - rabbitmq-service stop
 - rabbitmq-service remove
 - rabbitmq-service install
 - rabbitmq-service start
	
Make sure you can talk to your VM through the network
 - Use bridged adapter
 - Make sure IP scheme matches your network, probably private range
   - If not, use `ipconfig /release` and then `ipconfig /renew` after you selected bridged mode

Match up URL to tests with environment variable
 - variable name: `RabbitMQTransport.ConnectionString`
 - value: `host={nameOfVirtualMachine}`