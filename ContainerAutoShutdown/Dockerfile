FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS base
WORKDIR /app


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ContainerAutoShutdown/ContainerAutoShutdown.csproj", "ContainerAutoShutdown/"]
RUN dotnet restore "./ContainerAutoShutdown/ContainerAutoShutdown.csproj"
COPY . .
WORKDIR "/src/ContainerAutoShutdown"
RUN dotnet build "./ContainerAutoShutdown.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ContainerAutoShutdown.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Run as root to access the Docker socket mounted from the host.
USER root

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD dotnet ContainerAutoShutdown.dll -- healthcheck || exit 1

ENTRYPOINT ["dotnet", "ContainerAutoShutdown.dll"]