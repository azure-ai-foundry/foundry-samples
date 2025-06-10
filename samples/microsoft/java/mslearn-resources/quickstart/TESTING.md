# Testing Azure AI Foundry Java Samples

This guide provides instructions on how to test the Java samples in this repository to ensure they work correctly with the Azure AI Foundry SDK.

## Authentication Setup

These samples use `DefaultAzureCredential` for authentication. Before testing, ensure you are logged in with the Azure CLI:

```bash
az login
```

## Automated Testing Scripts

For your convenience, this repository includes testing scripts that automate the execution of all the samples. You can run the appropriate script for your environment to test all samples in sequence.

### On Linux/macOS/WSL: 

To use the testing script on Linux, macOS, or Windows Subsystem for Linux (WSL):

1. Make sure you've set up your `.env` file with the required configuration
2. Ensure you are logged in with the Azure CLI (run `az login`)
3. Open a terminal in the Java samples directory
4. Run the script:

```bash
# Make the script executable
chmod +x testing.sh

# Run all tests
./testing.sh

# Run a specific test
./testing.sh ChatCompletionSample
```

### On Windows:

To use the testing script on Windows Command Prompt or PowerShell:

1. Make sure you've set up your `.env` file with valid credentials
2. Open Command Prompt or PowerShell in the Java samples directory
3. Run the script:

```cmd
# Run all tests
testing.bat

# Run a specific test
testing.bat ChatCompletionSample
```

The scripts will execute each sample and display the results. They include error handling and track the success or failure of each sample.

## Prerequisites

Before testing, make sure you have:

1. Java 21 or later installed
2. Maven installed
3. An Azure account with access to Azure AI Services
4. Microsoft Entra ID application (service principal) with appropriate permissions
5. Azure CLI installed and logged in
6. Configured your `.env` file with valid credentials

## Setting Up Your Environment for Testing

1. Clone the repository (if you haven't already)
2. Navigate to the Java samples directory:
   ```bash
   cd foundry-samples/samples/microsoft/java/mslearn-resources/quickstart
   ```
3. Copy the `.env.template` file to `.env`:
   ```bash
   cp .env.template .env
   ```
4. Edit the `.env` file with your actual Azure credentials:
   ```properties
   AZURE_TENANT_ID=your_actual_tenant_id
   AZURE_CLIENT_ID=your_actual_client_id
   AZURE_CLIENT_SECRET=your_actual_client_secret
   AZURE_ENDPOINT=your_actual_endpoint
   AZURE_DEPLOYMENT=your_deployment_name
   ```

5. Install the Maven dependencies:
   ```bash
   mvn clean install
   ```

## Running and Testing Individual Samples

### 1. Create Project Sample

This sample tests the ability to create a new project in Azure AI Foundry.

```bash
mvn compile exec:java -Dexec.mainClass="com.azure.ai.foundry.samples.CreateProject"
```

**Expected output:**
- Successful project creation message
- Project details including ID, name, and creation time
- Instructions to add the project ID to your `.env` file

**What to verify:**
- The project was created successfully
- You received a valid project ID
- Save the project ID to your `.env` file as `PROJECT_ID=your_project_id` for use in subsequent tests

### 2. Chat Completion Sample

This sample tests basic chat completion capabilities.

```bash
mvn compile exec:java -Dexec.mainClass="com.azure.ai.foundry.samples.ChatCompletionSample"
```

**Expected output:**
- A completion response from the AI model
- Usage statistics showing token counts

**What to verify:**
- The model responded with relevant information about Azure AI Foundry
- No errors occurred during the API call
- The response contains readable text content

### 3. Streaming Chat Completion Sample

This sample tests streaming chat completions, allowing for real-time responses.

```bash
mvn compile exec:java -Dexec.mainClass="com.azure.ai.foundry.samples.ChatCompletionStreamingSample"
```

**Expected output:**
- A streaming response that appears token by token
- A complete poem about cloud computing when done

**What to verify:**
- Tokens appear incrementally rather than all at once
- The complete response forms coherent, readable content
- The streaming completes successfully without errors

### 4. Agent Sample

This sample tests creating and running an agent that can perform tasks.

```bash
mvn compile exec:java -Dexec.mainClass="com.azure.ai.foundry.samples.AgentSample"
```

**Expected output:**
- Agent creation confirmation
- Thread creation confirmation
- Status updates as the agent processes the request
- Final agent response about cloud computing benefits

**What to verify:**
- The agent was created successfully
- The agent run completed (not stuck in QUEUED or IN_PROGRESS state)
- The agent provided relevant information about cloud computing benefits
- The responses are well-formatted and coherent

### 5. File Search Agent Sample

This sample tests an agent's ability to search through document content.

```bash
mvn compile exec:java -Dexec.mainClass="com.azure.ai.foundry.samples.FileSearchAgentSample"
```

**Expected output:**
- Temporary file creation with cloud computing content
- Successful file upload confirmation
- Agent creation with file search capability
- Agent response listing benefits of cloud computing from the document

**What to verify:**
- The file was successfully uploaded
- The agent could access and search the file content
- The agent extracted relevant information about cloud computing benefits
- The agent response correctly references content from the document

### 6. Evaluate Agent Run Sample

This sample tests evaluating an agent's performance using metrics.

```bash
mvn compile exec:java -Dexec.mainClass="com.azure.ai.foundry.samples.EvaluateAgentSample"
```

**Expected output:**
- Agent creation and run for evaluation
- Evaluation metrics including helpfulness and accuracy scores
- Feedback on the agent's performance

**What to verify:**
- The evaluation completed successfully
- Scores are provided for the specified metrics
- The feedback provides insights on the agent's performance

## Troubleshooting Common Issues

### Authentication Errors

If you encounter authentication errors:
- Double-check your Azure API key in the `.env` file
- Ensure your Azure subscription has access to Azure AI services
- Verify that your deployment name is correct

### Resource Not Found Errors

If resources (like deployments or projects) can't be found:
- Verify that the names and IDs in your `.env` file are correct
- Check if the resources exist in your Azure portal
- Ensure your account has permission to access these resources

### Connection Issues

If you experience connection timeout or network errors:
- Check your internet connection
- Verify that the Azure endpoint URL is correct
- Ensure that your firewall or network allows connections to Azure services

## Advanced Testing

### Memory Usage and Performance

To monitor Java memory usage during sample execution:
```bash
mvn compile exec:java -Dexec.mainClass="com.azure.ai.foundry.samples.ChatCompletionSample" -Xmx512m -XX:+PrintGCDetails
```

### Running All Tests in Sequence

To run all samples in sequence to test the complete workflow:
```bash
for sample in CreateProject ChatCompletionSample ChatCompletionStreamingSample AgentSample FileSearchAgentSample EvaluateAgentSample; do
  echo "Running $sample..."
  mvn compile exec:java -Dexec.mainClass="com.azure.ai.foundry.samples.$sample"
  echo "Completed $sample"
  echo "--------------------------------------"
  sleep 5
done
```
