# Learn about building .NET container images:
# https://github.com/dotnet/dotnet-docker/blob/main/samples/README.md
FROM mcr.microsoft.com/dotnet/aspnet:11.0-resolute AS base
WORKDIR /app
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80

FROM mcr.microsoft.com/dotnet/sdk:11.0-resolute AS build

WORKDIR /src
COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM base AS final

# Install New Relic .NET agent
#
# The agent version and the SHA-256 hash of its deb file are pinned below. The build fails if the downloaded file does not
# match the hash. To use another version, change both values: the hash of newrelic-dotnet-agent_<version>_amd64.deb is listed in
# https://github.com/newrelic/newrelic-dotnet-agent/blob/main/release-data/download-site-hashes.json
ARG NEW_RELIC_AGENT_VERSION=10.58.0
ARG NEW_RELIC_AGENT_SHA256=cfbe4b6015fdc70dfd1fdb54053f4712b9e284b00552c6a620170aa41b2ffc34
RUN apt update && apt install -y wget
RUN wget https://download.newrelic.com/dot_net_agent/previous_releases/${NEW_RELIC_AGENT_VERSION}/newrelic-dotnet-agent_${NEW_RELIC_AGENT_VERSION}_amd64.deb -O agent.deb
RUN echo "${NEW_RELIC_AGENT_SHA256}  agent.deb" | sha256sum -c -
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
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ignore-errors.dll"]