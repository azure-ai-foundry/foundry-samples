# Microsoft.Agents.Hosting.AspNetCore

- Required package for a hosting an Agent in AspNet.
- Provides extension methods to add an Agents using DI.

## Changelog
| Version | Date | Changelog |
|------|----|------------|
| 1.2.0 | 2025-08-19 | [Detailed Changelog](https://github.com/microsoft/Agents-for-net/releases/tag/v1.2.0) |
| 1.3.0 | 2025-10-22 | [Detailed Changelog](https://github.com/microsoft/Agents-for-net/blob/main/changelog.md) |

## How-to use

```cs
builder.AddAgent<TAgent>();
```

Or

```cs
builder.AddAgent(sp =>
{
	var options = new AgentApplicationOptions()
	{
	};

	var app = new AgentApplication(options);

	return app;
});
```
