# Microsoft.Agents.Authentication

Defines core Authentication and token retrieval functionality.

## Changelog
| Version | Date | Changelog |
|------|----|------------|
| 1.2.0 | 2025-08-19 | [Detailed Changelog](https://github.com/microsoft/Agents-for-net/releases/tag/v1.2.0) |
| 1.3.0 | 2025-10-22 | [Detailed Changelog](https://github.com/microsoft/Agents-for-net/blob/main/changelog.md) |

## Main Types

- IAccessTokenProvider
- IConnections: Returns an IAccessTokenProvider based on name or matching criteria.
- ConfigurationConnections: Loads IConnections using configuration.

## ConfigurationConnections

- Configuration-based token connections
- Map of named connections

```
  "Connections": {
    "ServiceConnection": {
      "Assembly": "{{provider-assembly-name}}",
      "Type": "{{provider-type-name}}",
      "Settings": {
          {{provider-specific-settings}}
      }
    }
  }
```

> Note that provider type loading only works if the provider package has been added as a dependency in your project.