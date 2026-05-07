# Image Generation with Black Forest Labs' FLUX models

This sample demonstrates how to use **FLUX** image-generation models from _Black Forest Labs_ in Azure AI Foundry. The provided .NET 10 single-file application showcases how to create high-quality images from textual prompts.

This demo utilises `HttpClient` for direct API interaction and the `Azure.Identity` library for secure Azure Entra ID authentication.

## 📑 Table of Contents:
- [Part 1: Configuring Solution Environment](#part-1-configuring-solution-environment)
- [Part 2: Generating Images with FLUX Models](#part-2-generating-images-with-flux-models)
- [Part 3: Model Comparison - V1.1 Pro vs. V2 Pro](#part-3-model-comparison---v11-pro-vs-v2-pro)

## Part 1: Configuring Solution Environment
To use the application, set up your Azure AI Foundry environment and install the .NET 10 SDK.

### 1.1 Azure AI Foundry Setup
Ensure you have an Azure AI Foundry project with the FLUX-1.1-pro or FLUX-2-pro models deployed.

### 1.2 Authentication
This demo uses **Azure Entra ID** authentication via `AzureCliCredential` from `Azure.Identity`. This credential type uses the Azure CLI login context.

To make it work, ensure you are logged in via `az login` (Azure CLI).

> [!NOTE]
> More detailed information about supported credential types can be found [here](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential).

### 1.3 Configuration
Update `appsettings.json` with your Azure AI Foundry endpoint and model deployment names:

``` json
{
  "AzureFoundry": {
    "Endpoint": "https://<YOUR_RESOURCE>.services.ai.azure.com/",
    "Flux11ProDeployment": "flux-1.1-pro",
    "Flux2ProDeployment": "flux.2-pro"
  }
}
```

| Setting                          | Description                                                                                |
| -------------------------------- | ------------------------------------------------------------------------------------------ |
| AzureFoundry:Endpoint            | Your Azure AI Foundry endpoint URL (e.g., https://<YOUR_RESOURCE>.services.ai.azure.com/). |
| AzureFoundry:Flux11ProDeployment | The name of your FLUX 1.1 Pro model deployment.                                           |
| AzureFoundry:Flux2ProDeployment  | The name of your FLUX 2 Pro model deployment.                                             |

### 1.4 Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

Run the application:

``` PowerShell
dotnet .\ImageGeneration.cs
```

## Part 2: Generating Images with FLUX Models
The `ImageGeneration.cs` single-file application groups the image generation logic into the following core blocks:

### 2.1 Model Configuration:
The endpoint and deployment names are loaded from `appsettings.json`, while the BFL provider paths and API versions are defined in the application code.

``` csharp
var fluxModels = new Dictionary<string, (string Path, string ApiVersion, string ModelName)>
{
    ["flux-1.1-pro"] = (
        Path: "providers/blackforestlabs/v1/flux-pro-1.1",
        ApiVersion: "preview",
        ModelName: configuration["AzureFoundry:Flux11ProDeployment"] ?? "flux-1.1-pro"
    ),
    ["flux-2-pro"] = (
        Path: "providers/blackforestlabs/v1/flux-2-pro",
        ApiVersion: "preview",
        ModelName: configuration["AzureFoundry:Flux2ProDeployment"] ?? "flux.2-pro"
    ),
};
```

> [!WARNING]
> Please, ensure that the name of your model deployment is passed in lower case.

### 2.2 Secure Authentication:
Obtains an Entra ID access token for the `cognitiveservices` scope.

``` csharp
var credential = new AzureCliCredential();
var token = await credential.GetTokenAsync(
    new Azure.Core.TokenRequestContext(["https://cognitiveservices.azure.com/.default"]));
```

### 2.3 API Request:
Sends a JSON POST request to the model's endpoint, incl. the `prompt` describing the desired image and parameters such as `width`, `height` and `output_format`.

``` csharp
var url = $"{azureFoundryEndpoint.TrimEnd('/')}/{config.Path}?api-version={config.ApiVersion}";
var body = new
{
    prompt,
    n = 1,
    width = 1024,
    height = 1024,
    output_format = "png",
    model = config.ModelName
};
```

### 2.4 Response Processing:
Decodes the b64_json image data from the response and saves it to disk.

``` csharp
var json = await response.Content.ReadAsStringAsync();
using var doc = JsonDocument.Parse(json);
var b64 = doc.RootElement.GetProperty("data")[0].GetProperty("b64_json").GetString()!;
var imageBytes = Convert.FromBase64String(b64);
await File.WriteAllBytesAsync(outputPath, imageBytes);
```

## Part 3: Model Comparison - V1.1 Pro vs. V2 Pro

### 3.1 Features Comparison
While both models are available via Azure AI Foundry, they differ in resolution capabilities and API compatibility:

|                       | FLUX 1.1 Pro                              | FLUX 2 Pro                              |
| --------------------- | ----------------------------------------- | --------------------------------------- |
| BFL API Path          | providers/blackforestlabs/v1/flux-pro-1.1 | providers/blackforestlabs/v1/flux-2-pro |
| Max Resolution        | Up to 1.6 MP                              | Up to 4 MP                              |
| OpenAI-Compatible API | Supported                                 | Not supported                           |
| BFL Native API        | Supported                                 | Supported                               |

### 3.2 Image Generation by FLUX-1.1-Pro

``` JSON
Portrait of a red panda in renaissance clothing in Rembrandt style, detailed, intricate, digital art
```

<img src="images/image1.png" alt="IMAGE_REMBRANDT_FLUX1.1" width="400"/>

### 3.3 Image Generation by FLUX-2-Pro

``` JSON
Portrait of a red panda in renaissance clothing in Vermeer style, detailed, intricate, digital art
```

<img src="images/image2.png" alt="IMAGE_VERMEER_FLUX2" width="400"/>
