package com.azure.ai.foundry.samples.utils;

import io.github.cdimascio.dotenv.Dotenv;
import com.azure.identity.DefaultAzureCredential;
import com.azure.identity.DefaultAzureCredentialBuilder;

/**
 * Utility class for loading configuration from .env file and providing authentication credentials.
 * This class handles environment variable retrieval and authentication using Azure's DefaultAzureCredential,
 * which simplifies the authentication process by trying multiple authentication methods in sequence.
 * 
 * When using this class for authentication:
 * - For local development, ensure you're logged in with the Azure CLI using 'az login'
 * - For production environments, consider using managed identities or service principals
 * 
 * The DefaultAzureCredential tries the following methods in order:
 * 1. Environment variables (AZURE_CLIENT_ID, AZURE_CLIENT_SECRET, AZURE_TENANT_ID)
 * 2. Managed Identity credentials
 * 3. Visual Studio Code credentials
 * 4. Azure CLI credentials
 * 5. IntelliJ/Azure Toolkit credentials
 * 6. Azure PowerShell credentials
 */
public class ConfigLoader {
    private static final Dotenv dotenv = Dotenv.configure().ignoreIfMissing().load();
    private static DefaultAzureCredential credential;

    /**
     * Get an environment variable
     * 
     * @param key The environment variable key
     * @return The environment variable value
     */
    public static String getVariable(String key) {
        return dotenv.get(key);
    }

    /**
     * Get an environment variable with a default value
     * 
     * @param key The environment variable key
     * @param defaultValue The default value if the key is not found
     * @return The environment variable value or the default value
     */
    public static String getVariable(String key, String defaultValue) {
        String value = dotenv.get(key);
        return value != null ? value : defaultValue;
    }

    /**
     * Get the Azure Tenant ID
     * 
     * @return The Azure Tenant ID
     */
    public static String getAzureTenantId() {
        return getVariable("AZURE_TENANT_ID");
    }
    
    /**
     * Get the Azure Client ID
     * 
     * @return The Azure Client ID
     */
    public static String getAzureClientId() {
        return getVariable("AZURE_CLIENT_ID");
    }
    
    /**
     * Get the Azure Client Secret
     * 
     * @return The Azure Client Secret
     */
    public static String getAzureClientSecret() {
        return getVariable("AZURE_CLIENT_SECRET");
    }

    /**
     * Get the Azure Endpoint
     * 
     * @return The Azure Endpoint
     */
    public static String getAzureEndpoint() {
        return getVariable("AZURE_ENDPOINT");
    }

    /**
     * Get the Azure Deployment Name
     * 
     * @return The Azure Deployment Name
     */
    public static String getAzureDeployment() {
        return getVariable("AZURE_DEPLOYMENT");
    }

    /**
     * Get the Project ID (optional)
     * 
     * @return The Project ID
     */
    public static String getProjectId() {
        return getVariable("PROJECT_ID", null);
    }
    
    /**
     * Get an Azure DefaultAzureCredential instance for secure authentication with Azure services.
     * 
     * This method provides a centralized authentication mechanism using DefaultAzureCredential,
     * which is the recommended approach for Azure authentication. It implements a singleton pattern
     * to reuse the credential instance across multiple service client calls.
     * 
     * DefaultAzureCredential automatically tries multiple authentication methods in sequence:
     * - Environment variables (for service principals or automated environments)
     * - Managed Identity (for Azure-hosted applications)
     * - Visual Studio Code credentials (for local development)
     * - Azure CLI credentials (for local development with CLI login)
     * - IntelliJ/Azure Toolkit credentials (for Java IDE development)
     * - Azure PowerShell credentials (for PowerShell environments)
     * 
     * For local development, ensure you have logged in using 'az login' before running the application.
     * 
     * @return A configured DefaultAzureCredential instance ready for use with Azure service clients
     */
    public static DefaultAzureCredential getDefaultCredential() {
        if (credential == null) {
            credential = new DefaultAzureCredentialBuilder().build();
        }
        return credential;
    }
}
