FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble AS base
WORKDIR /app
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80

FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS build

WORKDIR /src
COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM base AS final

# Download and install the latest version of the New Relic .NET Agent via the apt package manager.
#
# To use a specific version instead, modify the apt-get install command to reference a specific version
#
# For example, to use v10.21.1 of the .NET agent, you would use the following apt-get install command:
#  apt-get install -y newrelic-dotnet-agent=10.21.1
#
# the agent is installed at /usr/local/new-relic-dotnet-agent
RUN apt-get update && apt-get install -y wget ca-certificates gnupg \
&& echo 'deb [signed-by=/usr/share/keyrings/newrelic-apt.gpg] http://apt.newrelic.com/debian/ newrelic non-free' | tee /etc/apt/sources.list.d/newrelic.list \
&& wget -O- https://download.newrelic.com/NEWRELIC_APT_2DAD550E.public | gpg --import --batch --no-default-keyring --keyring /usr/share/keyrings/newrelic-apt.gpg \
&& apt-get update \
&& apt-get install -y newrelic-dotnet-agent \
&& rm -rf /var/lib/apt/lists/*

# Enable the agent
ENV CORECLR_ENABLE_PROFILING=1 \
CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} \
CORECLR_NEWRELIC_HOME=/usr/local/newrelic-dotnet-agent \
CORECLR_PROFILER_PATH=/usr/local/newrelic-dotnet-agent/libNewRelicProfiler.so \
NEW_RELIC_LICENSE_KEY=<your-new-relic-license-key> \
NEW_RELIC_APP_NAME=weather-forecast-ubuntu \
NEWRELIC_LOG_DIRECTORY=/app/logs

WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "WeatherForecast.dll"]
