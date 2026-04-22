# Basic example of hosting an agent with the `responses` API and a remote MCP

## Running the server locally

### Using `azd` (Recommended)

```bash
azd ai agent run
```

### Without `azd`

Follow the instructions in the [Environment setup](../../README.md#environment-setup-without-azd) section of the README in the parent directory to set up your environment and install dependencies.

Follow the instructions here to get a GitHub Personal Access Token (PAT): [Creating a personal access token](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/creating-a-personal-access-token)

Run the following command to start the server:

```bash
python main.py
```

### Interacting with the agent

Send a POST request to the server with a JSON body containing a "message" field to interact with the agent. For example:

```bash
azd ai agent invoke --local "List all the repositories I own on GitHub."
```

Or use `curl`:

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "List all the repositories I own on GitHub."}'
```
