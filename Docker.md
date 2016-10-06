# Docker
## Getting ready
1. Install [boot2docker](http://boot2docker.io/) for your operating system or install docker directly with your method of choice
1. Open a shell which has docker in the path (under windows this is boot2docker start)
		initializing...
		Virtual machine boot2docker-vm already exists
		
		starting...
		Waiting for VM and Docker daemon to start...
		................oooooooo
		Started.
		...
		
		To connect the Docker client to the Docker daemon, please set:
		    export ...
		
		Or run: `eval "$(boot2docker shellinit)"`
		
		IP address of docker VM:
		192.168.59.103
note the IP address (in this example `192.168.59.103`) of the host.
1. Change directory to the root folder of this repository and issue the following command
		docker build -t particular_rabbit
1. When everything is successful start the rabbit like this
		docker run -d --hostname rabbit --name rabbit -p 5672:5672 -p 15672:15672 particular_rabbit
1. You can then access rabbit by using the host IP address. If you want to access the management interface then open up a web browser on your machine and type in
		http://host-ip:15672

## Start rabbit again
	docker start rabbit

## Look at rabbit logs
	docker logs rabbit

## Rebuild from scratch or update
	docker stop rabbit
	docker rm rabbit
	repeat Step 3 and 4 from Getting ready