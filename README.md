# AzureServiceBusTopologyComparison

## Running locally

### Requirements

1. [Install minikube](https://minikube.sigs.k8s.io/docs/start/)
2. [Enable the registry plugin](https://minikube.sigs.k8s.io/docs/handbook/registry/)
3. [Setup kubectl](https://minikube.sigs.k8s.io/docs/handbook/kubectl/)
4. In a command line tab execute `kubectl port-forward --namespace kube-system service/registry 5000:80` to forward `localhost:5000` to the container registry
5. In another command line tab execute `minikube dashboard`
6. Create an Azure Service Bus namespace

### Secrets

```bash
echo -n 'CONNECTION_STRING' | base64
```

add the base64 encoded value to the `secret.yaml`

```bash
kubectl apply -f secret.yaml
```

### Build containers

For the publisher, subscriber and topologycleaner execute

```bash
dotnet publish -c Release /t:PublishContainer -p ContainerRegistry=localhost:5000
```

By default minikube doesn't cache the images. Restarts of the cluster means the previous images are lost. In case you want to cache images refer to the [caching guideline](https://minikube.sigs.k8s.io/docs/commands/cache/).

### Prepare values

## Running on AKS

1. Setup AKS cluster
1. Connect according to the quick start guide in the local shell

### Build containers

```bash
dotnet publish /t:PublishContainer /p:ContainerRegistry=XYZ.azurecr.io --os linux --arch x64
```

### Deploy

```bash
helm install message-processor ./topology -f ./topology/values-acr.yaml
```