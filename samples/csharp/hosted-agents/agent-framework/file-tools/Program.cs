// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using DotNetEnv;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;

Env.TraversePath().Load();

var projectEndpoint = new Uri(Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is not set."));

var deployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME") ?? "gpt-4.1-mini";

// Bundled root: files copied into the published output via csproj <Content Include="resources\**">.
// In the container this resolves to /app/resources/.
string bundledRoot = Path.GetFullPath(
    Environment.GetEnvironmentVariable("BUNDLED_FILES_DIR")
    ?? Path.Combine(AppContext.BaseDirectory, "resources"));

// Session root: per-session $HOME volume managed by the Foundry platform.
// Files uploaded via `azd ai agent files upload <file>` land at $HOME/<file>.
string sessionRoot = Path.GetFullPath(
    Environment.GetEnvironmentVariable("HOME")
    ?? "/home/session");

AIAgent agent = new AIProjectClient(projectEndpoint, new DefaultAzureCredential())
    .AsAIAgent(
        model: deployment,
        instructions: """
            You are a friendly assistant that answers questions over two file sources:

              - Bundled files: built-in knowledge that ships with the agent image
                (e.g., reference reports the author packaged with you). Tools:
                ListBundledFiles, ReadBundledFile.

              - Session files: user-uploaded data for this session only (e.g., notes
                or a CSV the user wants you to analyse). Tools: ListSessionFiles,
                ReadSessionFile.

            Pick the tool pair by intent. If a name could match either source, list
            both first. Always read the file before answering; do not guess. Quote
            numbers and figures verbatim from the file.
            """,
        name: "file-tools",
        description: "Hosted agent that answers questions over bundled (image-baked) and session-uploaded files via two scoped tool pairs.",
        tools:
        [
            AIFunctionFactory.Create(ListBundledFiles),
            AIFunctionFactory.Create(ReadBundledFile),
            AIFunctionFactory.Create(ListSessionFiles),
            AIFunctionFactory.Create(ReadSessionFile),
        ]);

var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();

[Description("List the names of files bundled with the agent (built-in knowledge that ships with the image).")]
string ListBundledFiles() => SafeListNames(bundledRoot);

[Description("Read the full text contents of a bundled file by name.")]
string ReadBundledFile(
    [Description("Name of the bundled file (no directory components). Must be one of the names returned by ListBundledFiles.")] string fileName)
    => SafeRead(bundledRoot, fileName, "bundled files");

[Description("List the names of files the user uploaded into the current session (e.g., via 'azd ai agent files upload').")]
string ListSessionFiles() => SafeListNames(sessionRoot);

[Description("Read the full text contents of a file uploaded into the current session by name.")]
string ReadSessionFile(
    [Description("Name of the session file (no directory components). Must be one of the names returned by ListSessionFiles.")] string fileName)
    => SafeRead(sessionRoot, fileName, "session files");

// Path-safe helpers: GetFileName strip + canonicalize + StartsWith(root) check enforces the boundary
// per tool. The model cannot escape its own root, even via crafted input or indirect prompt injection.

static string SafeListNames(string root)
{
    try
    {
        if (!Directory.Exists(root))
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(root).Select(Path.GetFileName));
    }
    catch (Exception ex)
    {
        return $"Error listing files: {ex.Message}";
    }
}

static string SafeRead(string root, string fileName, string scope)
{
    try
    {
        string safeName = Path.GetFileName(fileName);
        if (string.IsNullOrEmpty(safeName))
        {
            return $"File '{fileName}' not found in {scope}.";
        }

        string fullPath = Path.GetFullPath(Path.Combine(root, safeName));

        string rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.Ordinal))
        {
            return $"File '{fileName}' not found in {scope}.";
        }

        return File.Exists(fullPath)
            ? File.ReadAllText(fullPath)
            : $"File '{fileName}' not found in {scope}.";
    }
    catch (Exception ex)
    {
        return $"Error reading '{fileName}': {ex.Message}";
    }
}
