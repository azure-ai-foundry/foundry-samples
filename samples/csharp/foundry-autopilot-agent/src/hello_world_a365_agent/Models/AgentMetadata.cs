namespace HelloWorldA365.Models;

public class AgentMetadata 
{
    public Guid UserId { get; set; }
    public Guid AgentId { get; set; }
    public Guid AgentApplicationId { get; set; }
    public Guid TenantId { get; set; }
    public string EmailId { get; set; } = string.Empty;
    public bool IsMessagingEnabled { get; set; } = false;
}