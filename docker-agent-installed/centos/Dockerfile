FROM quay.io/centos/centos:stream9 AS base
WORKDIR /app
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80

# build using the Ubuntu-based .NET SDK image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src
COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish 

FROM base AS final

# install asp.netcore
RUN dnf install aspnetcore-runtime-10.0 -y

# install wget
RUN yum -y update && yum -y install wget

# Download, verify and install the New Relic .NET Agent from the rpm package.
#
# The agent version and the SHA-256 hash of its rpm file are pinned below. The build fails if the downloaded file does not
# match the hash, so a file that was corrupted or changed after it was published is never installed.
#
# To use another version, change both values. The hash of newrelic-dotnet-agent-<version>-1.x86_64.rpm is listed in
# https://github.com/newrelic/newrelic-dotnet-agent/blob/main/release-data/download-site-hashes.json
# For agent version 10.58.0 and higher you can also check the file's GitHub attestation:
#   gh attestation verify newrelic-dotnet-agent-<version>-1.x86_64.rpm --repo newrelic/newrelic-dotnet-agent
# For more details, see https://docs.newrelic.com/docs/apm/agents/net-agent/other-installation/how-verify-checksum-net-agent-downloads/
#
# The agent is installed at /usr/local/newrelic-dotnet-agent
ARG NEW_RELIC_AGENT_VERSION=10.58.0
ARG NEW_RELIC_AGENT_SHA256=e7690ac9d4ad88493be69dd0bf7b5383b24c85c16ff4d69d5aa288850f0f2131
RUN wget -q https://download.newrelic.com/dot_net_agent/previous_releases/${NEW_RELIC_AGENT_VERSION}/newrelic-dotnet-agent-${NEW_RELIC_AGENT_VERSION}-1.x86_64.rpm -O /tmp/newrelic-dotnet-agent.rpm \
    && echo "${NEW_RELIC_AGENT_SHA256}  /tmp/newrelic-dotnet-agent.rpm" | sha256sum -c - \
    && rpm -i /tmp/newrelic-dotnet-agent.rpm \
    && rm /tmp/newrelic-dotnet-agent.rpm

# Enable the agent
ENV CORECLR_ENABLE_PROFILING=1 \
CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} \
CORECLR_NEWRELIC_HOME=/usr/local/newrelic-dotnet-agent \
CORECLR_PROFILER_PATH=/usr/local/newrelic-dotnet-agent/libNewRelicProfiler.so \
NEW_RELIC_LICENSE_KEY=<your-new-relic-license-key> \
NEW_RELIC_APP_NAME=weather-forecast-centos \
NEWRELIC_LOG_DIRECTORY=/app/logs

WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "WeatherForecast.dll"]
