FROM quay.io/centos/centos:stream9 AS base
WORKDIR /app
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80

# build using the Debian-based .NET SDK image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src
COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish 

FROM base AS final

# install asp.netcore
RUN dnf install aspnetcore-runtime-8.0 -y

# Enable the agent - note that the NuGet package installs to /app/newrelic
ENV CORECLR_ENABLE_PROFILING=1 \
CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} \
CORECLR_NEWRELIC_HOME=/app/newrelic \
CORECLR_PROFILER_PATH=/app/newrelic/libNewRelicProfiler.so \
NEW_RELIC_LICENSE_KEY=<your-new-relic-license-key> \
NEW_RELIC_APP_NAME=weather-forecast-centos \
NEWRELIC_LOG_DIRECTORY=/app/logs

WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "WeatherForecast.dll"]
