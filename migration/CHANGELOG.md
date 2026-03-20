# Changelog

All notable changes to the migration solution are documented in this file.

## Released March 20th, 2026

### Changed

- Reworked migration output to target the current Foundry Agent Service model instead of legacy assistant-style write APIs.
- Standardized agent creation on `AIProjectClient.agents.create_version(...)` with explicit `PromptAgentDefinition` payloads.
- Moved runtime validation to `project.get_openai_client()` using conversations plus responses for post-migration smoke testing.
- Updated source ingestion so migrations can still read from the legacy Assistants API, project endpoints, project connection strings, or Cosmos DB while always writing to the Foundry Agent Service target.
- Refreshed Docker packaging and dependency guidance around `azure-ai-projects>=2.0.0`, with optional prerelease support for connection-string helper scenarios.
- Aligned `.env.example` with the rewritten production-first environment model and current variable names.
- Removed stale Docker wrapper passthrough for unused legacy variables such as `ASSISTANT_API_BASE`, `PROJECT_ENDPOINT_URL`, and `V2_API_BASE`.

### Added

- Offline pytest coverage for connection string parsing, tool translation, prompt definition building, orchestration flow, and conversations/responses validation behavior.
- Wrapper regression coverage for PowerShell `--help`, Bash syntax validation, and Bash `--help` execution in the migration test suite.
- Explicit unsupported-tool warnings and recommendations for `connected_agent`, `event_binding`, and `output_binding`.
- Optional post-migration test tool injection for function, MCP, computer use, image generation, and Azure Function scenarios.

### Fixed

- Modernized the repository pytest collection hook to avoid the pytest 9 deprecation warning.
- Updated Docker context ignores to exclude local virtual environments, test caches, and coverage artifacts.
- Added a true help-only path to the PowerShell Docker wrapper so usage validation does not require Docker or production arguments.
- Reworked the Bash Docker wrapper argument parsing and normalized shell compatibility so `bash -n` and help-mode validation succeed on Windows-hosted workspaces.
- Confirmed local migration validation with compile checks, wrapper smoke checks, and an expanded pytest suite passing with 11 tests.