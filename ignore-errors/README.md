# New Relic .NET Agent Samples - Ignore Errors

This folder contains a Dockerfile and a simple ASP.NET Core web api application that can be used to test the agent's ability to ignore error codes by status.

Steps for testing:

1. Edit the Dockerfile to set your New Relic license key and application name. 
2. `docker build . -t errortest`
3. `docker run -p 8080:8080 errortest`
4. In a browser or with curl, make several requests to http://localhost:8080/weatherforecast and then one or two requests to http://localhost:8080/throwerror
5. Observe in New Relic APM that there should be some 422 errors in your errors inbox for the test app
6. Now run the container again with the ignore error codes env var set: `docker run -p 8080:8080 -e 'NEW_RELIC_ERROR_COLLECTOR_IGNORE_ERROR_CODES=422' errortest`
7. Repeat step 4
8. There should be no additional 422 errors in your errors inbox