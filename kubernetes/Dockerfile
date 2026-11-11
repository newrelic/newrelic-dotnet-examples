FROM mcr.microsoft.com/dotnet/aspnet:11.0-resolute AS base
WORKDIR /app
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80

FROM mcr.microsoft.com/dotnet/sdk:11.0-resolute AS build
WORKDIR /src
RUN dotnet new webapi --name WeatherForecast --output ./
RUN dotnet publish -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# test by browsing to http://<hostname>:<port>/WeatherForecast
ENTRYPOINT ["dotnet", "WeatherForecast.dll"]
