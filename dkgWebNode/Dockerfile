#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# Step 1: Build and publish the Blazor WebAssembly application
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER app
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-stage
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["dkgWebNode/dkgWebNode.csproj", "dkgWebNode/"]
RUN dotnet restore "./dkgWebNode/dkgWebNode.csproj"
COPY . .
WORKDIR "/src/dkgWebNode"
RUN dotnet publish "./dkgWebNode.csproj" -c Release -o /app/publish

# Step 2: Setup Nginx to serve the Blazor app
FROM nginx:alpine AS final
COPY --from=build-stage /app/publish/wwwroot /var/www/dkg
# Copy custom nginx config
COPY dkgWebNode/config/nginx.docker.config  /etc/nginx/conf.d/default.conf

# Expose port 80 for HTTP
EXPOSE 80
#EXPOSE 443
# Start Nginx and keep it running in the foreground
CMD ["nginx", "-g", "daemon off;"]
