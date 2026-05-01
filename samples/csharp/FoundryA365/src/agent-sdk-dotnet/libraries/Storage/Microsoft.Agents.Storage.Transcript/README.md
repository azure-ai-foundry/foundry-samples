# Microsoft.Agents.Storage.Transcript

The [Transcript](https://github.com/microsoft/Agents/blob/main/specs/transcript/transcript.md) log of conversational actions made by humans and automated software. Transcript extends the Activity Protocol to include tracing generated during the processing of those activities.

## Changelog
| Version | Date | Changelog |
|------|----|------------|
| 1.2.0 | 2025-08-19 | [Detailed Changelog](https://github.com/microsoft/Agents-for-net/releases/tag/v1.2.0) |
| 1.3.0 | 2025-10-22 | [Detailed Changelog](https://github.com/microsoft/Agents-for-net/blob/main/changelog.md) |

## Main Types

- ITranscriptLogger
- MemoryTranscriptStore: For testing purposes
- BlobsTranscriptStore: For production purposes
- TranscriptLoggerMiddleware: For automatic logging of incoming and outgoing Activities.