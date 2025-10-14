FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build 
WORKDIR /src

COPY . ./Sender
RUN dotnet restore "Sender/Sender.csproj"

WORKDIR "/src/Sender"
RUN dotnet build "Sender.csproj" -c Release -o /app/build --os linux

FROM build AS publish
RUN dotnet publish "Sender.csproj" -c Release -o /app/publish

FROM base AS final

ARG NEW_RELIC_LICENSE_KEY
ARG NEW_RELIC_APP_NAME
ARG AZURE_SERVICE_BUS_CONNECTION_STRING
ARG AZURE_SERVICE_BUS_QUEUE_NAME

# Enable the agent
ENV CORECLR_ENABLE_PROFILING=1 \
CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} \
CORECLR_NEW_RELIC_HOME=/app/newrelic \
CORECLR_PROFILER_PATH=/app/newrelic/libNewRelicProfiler.so \
NEW_RELIC_LICENSE_KEY=${NEW_RELIC_LICENSE_KEY} \
NEW_RELIC_APP_NAME=${NEW_RELIC_APP_NAME} \
NEW_RELIC_LOG_DIRECTORY=/app/logs \
AZURE_SERVICE_BUS_CONNECTION_STRING=${AZURE_SERVICE_BUS_CONNECTION_STRING} \
AZURE_SERVICE_BUS_QUEUE_NAME=${AZURE_SERVICE_BUS_QUEUE_NAME}
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Sender.dll"]
