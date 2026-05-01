# Microsoft.Agents.Extensions.Teams

## Purpose

This is a Agents SDK based implementation of the former Teams AI library.
Issues, releases, and source code available at https://github.com/microsoft/teams-agents

## Changelog
| Version | Date | Changelog |
|------|----|------------|
| 1.2.0 | 2025-08-19 | [Detailed Changelog](https://github.com/microsoft/Agents-for-net/releases/tag/v1.2.0) |
| 1.3.0 | 2025-10-22 | [Detailed Changelog](https://github.com/microsoft/Agents-for-net/blob/main/changelog.md) |

## Teams AI Agent APIs

Below is a detailed overview of the main APIs and their purposes:

### Microsoft.Agents.Extensions.Teams.AI
This is the main library defined here. We leverage existing Agent SDK libraries to handle non-Teams and non-AI tasks.

- **TeamsApplication**: Represents a Teams application that can be used to build and deploy intelligent agents within Microsoft Teams.
- **TeamsApplicationBuilder**: A builder class for simplifying the creation of an Application instance.
- **AI System**: Responsible for moderating input and output, generating plans, and executing them. It includes components like the moderator, planner, and actions.
- **ActionPlanner**: A powerful planner that uses a Large Language Model (LLM) to generate plans. It can trigger parameterized actions and send text-based responses to the user.
- **PromptManager**: Manages prompt functions, data sources, and templates, allowing for the addition and retrieval of these components.
- **GPTTokenizer**: Implements a GPT Tokenizer for encoding and decoding text using the "gpt-4" model.
- **JsonResponseValidator**: Parses any JSON returned by the model and optionally verifies it against a JSON schema.

### Microsoft.Agents.Hosting.AspNetCore

- **AddAgentAspNetAuthentication**: Configures authentication services based on provided settings in the configuration file, facilitating authentication and authorization for Microsoft Teams agents.

### Microsoft.Agents.Authentication.Msal

- **Authentication**: Provides authentication capabilities using MSAL (Microsoft Authentication Library) for secure access to Microsoft services.

### Microsoft.KernelMemory

- **Kernel Memory**: A library that allows indexing and querying any data using LLM and natural language, tracking sources, and showing citations.

### Azure and OpenAI Integration

- **Azure.AI.OpenAI**: Provides integration with Azure OpenAI services for natural language processing.
- **Azure.AI.ContentSafety**: Ensures content safety by moderating input and output using Azure's Content Safety API.

### Configuration and Deployment

- **teamsapp.yml**: Automates the creation of a Teams app registration, Azure AD app registration for a bot, and the deployment of infrastructure using ARM templates.
- **appsettings.json**: Contains configuration settings for token validation, connections, and logging.

## Getting Started
We recommend getting started with the Teams Chef sample, available at
- [Teams Chef Bot Sample](./samples/04.ai.a.teamsChefBot) - An AI-powered agent designed to assist developers in building Microsoft Teams apps.