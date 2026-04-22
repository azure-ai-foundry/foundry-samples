# Basic example of hosting an agent with the `responses` API and a workflow

## Running the server locally

### Using `azd` (Recommended)

```bash
azd ai agent run
```

### Without `azd`

Follow the instructions in the [Environment setup](../../README.md#environment-setup-without-azd) section of the README in the parent directory to set up your environment and install dependencies.

Run the following command to start the server:

```bash
python main.py
```

### Interacting with the agent

Send a POST request to the server with a JSON body containing a "message" field to interact with the agent. For example:

```bash
azd ai agent invoke --local "Create a slogan for a new electric SUV that is affordable and fun to drive."
```

Or use `curl`:

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "Create a slogan for a new electric SUV that is affordable and fun to drive."}'
```
