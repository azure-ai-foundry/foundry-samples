# Agent Framework Samples

This directory contains samples that demonstrate how to use the Agent Framework to host agents with different capabilities and configurations. Each sample includes a README with instructions on how to set up, run, and interact with the agent.

## Running with `azd` (Recommended)

The easiest way to run any agent-framework sample is with the Azure Developer CLI:

```bash
azd ai agent run
```

This handles virtual environment creation, dependency installation, environment variable injection, and starts the agent on `http://localhost:8088`. See each sample's README for invocation examples.

## Environment setup (without `azd`)

1. Navigate to the sample directory you want to run. For example:

   ```bash
   python -m venv .venv

   # Windows
   .venv\Scripts\Activate

   # macOS/Linux
   source .venv/bin/activate
   ```

2. Install dependencies:

   ```bash
   pip install -r requirements.txt
   ```

3. Create a `.env` file with your Foundry configuration following the `env.example` file in the sample.

4. Make sure you are logged in with the Azure CLI:

   ```bash
   az login
   ```
