# dkg-service-node
Dkg service node

docker run --env=DKG_NODE_SERVER_GRPC_PORT=5050 --env=DKG_NODE_SERVER_GRPC_HOST=localhost --env=DKG_SERVICE_NODE_URL=http://dkg.samsonov.net:8080 --env=DKG_NODE_SERVER_NAME=TestExt --env=PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin --env=DOTNET_RUNNING_IN_CONTAINER=true --env=DOTNET_VERSION=8.0.4 --env=ASPNET_VERSION=8.0.4 --workdir=/app -p 5050:5050 --runtime=runc -d ghcr.io/maxirmx/dkg-node:latest