# New Relic .NET Agent Sample - Console (.NET 9)

## Overview
This sample demonstrates a .NET 9 Console app, using the `NewRelic.Agent` [NuGet package](https://www.nuget.org/packages/NewRelic.Agent). The application runs a `BackgroundService` that requests the current weather for Portland, OR (using a free api service) every 5 seconds and writes the  JSON response to the console. 

## To run this sample:
1. Edit `runSample.ps1` and replace `<your-new-relic-license-key>` with your New Relic license key.
2. Start a Powershell terminal window and navigate to the `samples\console` folder, then run:
    ```
    .\runSample.ps1
    ```
3. Log in to your New Relic account and look for the `ConsoleSample` app on the APM Summary page (it can take a few minutes).
4. You can view the logs generated by the New Relic .NET Agent by navigating to the `bin\debug\net9.0\newrelic\logs` folder.

## For more help
Refer to the [.NET Agent Documentation](https://docs.newrelic.com/install/dotnet) if you need further guidance.
