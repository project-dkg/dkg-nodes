# Dkg nodes

## Starting dkg node in docker container

__Parameters__
* ```<host>``` -- external address of the system where the node will run
* ```<port>``` -- public port dedicated for the node.  Please note that there is current limitation that the port exposed from container shall be the same as public port.
* ```<name>``` -- node public name, optional, defaults to ```<host>:<port>```

__Command__
```
docker run --env=DKG_NODE_SERVER_GRPC_PORT=<port> --env=DKG_NODE_SERVER_GRPC_HOST=<host> \
           --env=DKG_SERVICE_NODE_URL=http://dkg.samsonov.net:8080 --env=DKG_NODE_SERVER_NAME=<name> \
           --env=PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin \
           --env=DOTNET_RUNNING_IN_CONTAINER=true --env=DOTNET_VERSION=8.0.4 --env=ASPNET_VERSION=8.0.4 \
           --workdir=/app -p <port>:<port> --runtime=runc -d ghcr.io/maxirmx/dkg-node:latest
```
