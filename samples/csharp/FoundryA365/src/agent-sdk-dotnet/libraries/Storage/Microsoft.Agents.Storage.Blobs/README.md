# Microsoft.Agents.Storage.Blobs

## About

* An Agent is inherently stateless. Once your Agent is deployed, it may not run in the same process or on the same machine from one turn to the next. However, your Agent may need to track the context of a conversation so that it can manage its behavior and remember answers to previous questions. The state and storage features of the Agents SDK allow you to add state to your Agent.
* Microsoft.Agents.State use Storage to persist state to memory, Azure Blobs, or CosmosDb.

## Changelog
| Version | Date | Changelog |
|------|----|------------|
| 1.2.0 | 2025-08-19 | [Detailed Changelog](https://github.com/microsoft/Agents-for-net/releases/tag/v1.2.0) |
| 1.3.0 | 2025-10-22 | [Detailed Changelog](https://github.com/microsoft/Agents-for-net/blob/main/changelog.md) |

## Main Types

- BlobsStorage

## Registering BlobsStorage

### appsettings.json
```json
{
  "BlobsStorageOptions": {
    "ConnectionString": "{blobs_connection_string}",
    "ContainerName": "{blobs_container_name}"
  }
}
```

### Program.cs
```
builder.Services.AddSingleton<IStorage>((sp) => new BlobsStorage(
    builder.Configuration["BlobsStorageOptions:ConnectionString"],
    builder.Configuration["BlobsStorageOptions:ContainerName"]));
```
> See the other BlobsStorage constructors for additional options.

