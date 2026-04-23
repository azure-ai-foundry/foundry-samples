# Basic example of hosting an agent with the `responses` API

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
azd ai agent invoke --local "Hi"
```

Or use `curl`:

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "Hi"}'
```

The server will respond with a JSON object containing the response text and a response ID. You can use this response ID to continue the conversation in subsequent requests.

### Multi-turn conversation

To have a multi-turn conversation with the agent, include the previous response id in the request body. For example:

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "How are you?", "previous_response_id": "REPLACE_WITH_PREVIOUS_RESPONSE_ID"}'
```
