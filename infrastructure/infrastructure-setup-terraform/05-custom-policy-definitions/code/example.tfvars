# Example configuration for custom policy definitions

# Policy definition name
policy_name = "deny-disallowed-foundry-connections"

# Allowed connection categories (customize as needed)
allowed_categories = ["BingLLMSearch", "CognitiveSearch", "AzureOpenAI"]

# Set to true to assign the policy to the subscription
assign_policy = false

# Policy assignment configuration (used if assign_policy = true)
assignment_name         = "deny-disallowed-connections-assignment"
assignment_display_name = "Deny Disallowed Foundry Connections"
