#:package Azure.Identity@1.21.0
#:package Microsoft.Extensions.Configuration@10.0.0-preview.4.25258.110
#:package Microsoft.Extensions.Configuration.Json@10.0.0-preview.4.25258.110
#:package Microsoft.Extensions.Configuration.Binder@10.0.0-preview.4.25258.110
#:property JsonSerializerIsReflectionEnabledByDefault=true
#:property EnableAotAnalyzer=false
#:property EnableTrimAnalyzer=false

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .Build();

string azureFoundryEndpoint = configuration["AzureFoundry:Endpoint"]
    ?? throw new InvalidOperationException("AzureFoundry:Endpoint is not configured in appsettings.json");

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

var credential = new AzureCliCredential();
var token = await credential.GetTokenAsync(
    new Azure.Core.TokenRequestContext(["https://cognitiveservices.azure.com/.default"]));

using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
httpClient.Timeout = TimeSpan.FromMinutes(5);

await GenerateImageAsync("flux-1.1-pro",
    "Portrait of a red panda in renaissance clothing in Rembrandt style, detailed, intricate, digital art",
    "image1.png");

await GenerateImageAsync("flux-2-pro",
    "Portrait of a red panda in renaissance clothing in Vermeer style, detailed, intricate, digital art",
    "image2.png");

async Task GenerateImageAsync(string modelKey, string prompt, string outputFileName)
{
    var config = fluxModels[modelKey];
    var url = $"{azureFoundryEndpoint.TrimEnd('/')}/{config.Path}?api-version={config.ApiVersion}";

    Console.WriteLine($"Generating image with model '{config.ModelName}'...");
    Console.WriteLine($"Prompt: {prompt}");
    Console.WriteLine($"URL: {url}");

    var body = new
    {
        prompt,
        n = 1,
        width = 1024,
        height = 1024,
        output_format = "png",
        model = config.ModelName
    };

    var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
    var response = await httpClient.PostAsync(url, content);

    Console.WriteLine($"Status: {response.StatusCode}");

    if (!response.IsSuccessStatusCode)
    {
        var errorText = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Error: {errorText[..Math.Min(500, errorText.Length)]}");
        return;
    }

    var json = await response.Content.ReadAsStringAsync();
    using var doc = JsonDocument.Parse(json);
    var b64 = doc.RootElement.GetProperty("data")[0].GetProperty("b64_json").GetString()!;
    var imageBytes = Convert.FromBase64String(b64);

    string outputPath = Path.Combine(".", "images", outputFileName);
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    await File.WriteAllBytesAsync(outputPath, imageBytes);
    Console.WriteLine($"Image saved to {outputPath}");
}


