FROM mcr.microsoft.com/dotnet/aspnet:11.0-alpine AS base
WORKDIR /app
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80

FROM mcr.microsoft.com/dotnet/sdk:11.0-alpine AS build

WORKDIR /src
COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM base AS final

# Download, verify and install the New Relic .NET Agent from the release tarball.
#
# The agent version and the SHA-256 hash of its tarball are pinned below. The build fails if the downloaded file does not
# match the hash, so a file that was corrupted or changed after it was published is never installed.
#
# To use another version, change both values. The hash of newrelic-dotnet-agent_<version>_amd64.tar.gz is listed in
# https://github.com/newrelic/newrelic-dotnet-agent/blob/main/release-data/download-site-hashes.json
# For agent version 10.58.0 and higher you can also check the file's GitHub attestation:
#   gh attestation verify newrelic-dotnet-agent_<version>_amd64.tar.gz --repo newrelic/newrelic-dotnet-agent
# For more details, see https://docs.newrelic.com/docs/apm/agents/net-agent/other-installation/how-verify-checksum-net-agent-downloads/
#
# The agent is installed at /usr/local/newrelic-dotnet-agent
ARG NEW_RELIC_AGENT_VERSION=10.58.0
ARG NEW_RELIC_AGENT_SHA256=1a55362d8d430728215854a85e7483dce02c7aa43a968a5b8d741cbea55ade8d
RUN apk update && apk add --no-cache wget tar \
    && wget -q https://download.newrelic.com/dot_net_agent/previous_releases/${NEW_RELIC_AGENT_VERSION}/newrelic-dotnet-agent_${NEW_RELIC_AGENT_VERSION}_amd64.tar.gz -O /tmp/newrelic-dotnet-agent.tar.gz \
    && echo "${NEW_RELIC_AGENT_SHA256}  /tmp/newrelic-dotnet-agent.tar.gz" | sha256sum -c - \
    && tar -xzf /tmp/newrelic-dotnet-agent.tar.gz -C /usr/local \
    && rm /tmp/newrelic-dotnet-agent.tar.gz

# Enable the agent
ENV CORECLR_ENABLE_PROFILING=1 \
CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} \
CORECLR_NEWRELIC_HOME=/usr/local/newrelic-dotnet-agent \
CORECLR_PROFILER_PATH=/usr/local/newrelic-dotnet-agent/libNewRelicProfiler.so \
NEW_RELIC_LICENSE_KEY=<your-new-relic-license-key> \
NEW_RELIC_APP_NAME=weather-forecast-alpine \
NEWRELIC_LOG_DIRECTORY=/app/logs

WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "WeatherForecast.dll"]
