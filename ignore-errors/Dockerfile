# Learn about building .NET container images:
# https://github.com/dotnet/dotnet-docker/blob/main/samples/README.md
FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build
WORKDIR /source

# copy csproj and restore as distinct layers
COPY webapi/*.csproj .
RUN dotnet restore

# copy and publish app and libraries
COPY webapi/* .
RUN dotnet publish --no-restore -o /app

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy

# install New Relic .NET agent
RUN apt update && apt install -y wget
RUN wget https://download.newrelic.com/dot_net_agent/previous_releases/10.27.0/newrelic-dotnet-agent_10.27.0_amd64.deb -O agent.deb
RUN dpkg -i agent.deb

# configure agent env vars
ENV CORECLR_ENABLE_PROFILING=1
ENV CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A}
ENV CORECLR_PROFILER_PATH="/usr/local/newrelic-dotnet-agent/libNewRelicProfiler.so"
ENV CORECLR_NEWRELIC_HOME="/usr/local/newrelic-dotnet-agent"
ENV NEW_RELIC_APP_NAME="YourAppName"
ENV NEW_RELIC_LICENSE_KEY="YourLicenseKey"
ENV NEWRELIC_LOG_LEVEL="debug"

# run app
EXPOSE 8080
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["./webapi"]