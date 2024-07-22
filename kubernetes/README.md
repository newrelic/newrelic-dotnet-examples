# Deploy the .NET agent with the New Relic Kubernetes Agent Operator

This example demonstrates building and deploying a .NET application in Kubernetes, using the [New Relic Kubernetes Agent Operator](https://docs.newrelic.com/docs/kubernetes-pixie/kubernetes-integration/installation/k8s-agent-operator/) to automatically install the New Relic .NET agent. Note that there is no need to add commands to `Dockerfile` or use the `NewRelic.Agent` Nuget package to install the .NET agent as shown in other examples in this repo -- the agent operator takes care of that for you automatically.

## Custom Instrumentation Resource Definition
The Kubernetes Agent Operator is added to each namespace that you want to monitor by way of a custom resource definition. You can either add the following `Instrumentation.yml` file to your Helm chart (as shown in this example under `chart/templates`), or you can apply the custom resource definition to each namespace with the following command (after following steps 1 through 3 below): 
```
kubectl apply -f Instrumentation.yml -n <your namespace>
```

##### Instrumentation.yml:
```
apiVersion: newrelic.com/v1alpha1
kind: Instrumentation
metadata:
    labels:
        app.kubernetes.io/name: instrumentation
        app.kubernetes.io/created-by: k8s-agents-operator
    name: newrelic-instrumentation
spec:
    dotnet:
        image: newrelic/newrelic-dotnet-init:latest
```
This example uses the latest .NET init container (containing the latest .NET agent release) - for an actual production app, you should use a specific version tag. See the [.NET Init Container Docker Hub Repo](https://hub.docker.com/repository/docker/newrelic/newrelic-dotnet-init/general) for the full list of available versions.

## Installation
This example uses MiniKube, but the same process can be used for other Kubernetes environments. 

1. Add the `jetstack` and `k8s-agents-operator` Helm chart repositories:
```
helm repo add jetstack https://charts.jetstack.io
helm repo add k8s-agents-operator https://newrelic.github.io/k8s-agents-operator
```

2. Install the `cert-manager` Helm chart:
```
helm install cert-manager jetstack/cert-manager \
  --namespace cert-manager \
  --create-namespace \
  --set crds.enabled=true
```

3. Install the `k8s-agents-operator` Helm chart in its own namespace and specify your New Relic license key:
```
helm upgrade --install k8s-agents-operator k8s-agents-operator/k8s-agents-operator \
  --namespace k8s-agents-operator \
  --create-namespace \
  --set=licenseKey=<NEW RELIC INGEST LICENSE KEY>
```

Note that this example creates a standalone installation of the Kubernetes agent operator. For a production deployment, we recommend installing the agent operator in addition to the `nri-bundle` - refer to [these instructions](https://docs.newrelic.com/docs/kubernetes-pixie/kubernetes-integration/installation/k8s-agent-operator/#bundle-installation) for more details.

4. Create a secret in each namespace where you want the agent to be installed, containing a New Relic ingest license key (we use the `default` namespace in this exmple):
```
kubectl create secret generic newrelic-key-secret \
  --namespace default \
  -- create-namespace \
  --from-literal=new_relic_license_key=<NEW RELIC INGEST LICENSE KEY>
```  

5. Build the .NET test app container:
```
minikube image build -t weatherforecast:latest .
```

6. Deploy the .NET test app Helm chart:
```
helm install test-app-dotnet ./chart/ -n default
```

7. Start the .NET test app service:
```
minikube service test-app-dotnet-service
```
This will show the URL where the service is listening. From a browser, navigate to `<url>/WeatherForecast` and within a few minutes, you should see a `k8s-test-app-dotnet` APM entity in the New Relic UI.
