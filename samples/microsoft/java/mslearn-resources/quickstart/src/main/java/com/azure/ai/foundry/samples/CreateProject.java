package com.azure.ai.foundry.samples;

import com.azure.ai.foundry.samples.utils.ConfigLoader;
import com.azure.ai.projects.AIProjectClient;
import com.azure.ai.projects.AIProjectClientBuilder;
import com.azure.ai.projects.models.Project;
import com.azure.ai.projects.models.Deployment;
import com.azure.ai.projects.models.DeploymentOptions;
import com.azure.identity.DefaultAzureCredential;
import com.azure.identity.DefaultAzureCredentialBuilder;

/**
 * Demonstrates project creation and model deployment using the beta SDK.
 */
public class CreateProject {
    public static void main(String[] args) {
        var endpoint = ConfigLoader.getAzureEndpoint();
        var credential = new DefaultAzureCredentialBuilder().build();

        var client = new AIProjectClientBuilder()
            .endpoint(endpoint)
            .credential(credential)
            .buildClient();

        System.out.println("Creating project...");
        var project = client.createProject("My Sample Project", "A project created using the Java SDK");
        System.out.println("Project created: " + project.getId());

        var options = new DeploymentOptions()
            .setName("my-deployment")
            .setModel("gpt-4")
            .setDescription("Sample model deployment");

        System.out.println("Deploying model...");
        var deployment = client.createDeployment(project.getId(), options);
        System.out.println("Deployment created: " + deployment.getId());
    }
}
