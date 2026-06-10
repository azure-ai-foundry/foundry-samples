namespace WorkstreamManager.Models;

public class AgentMetadata 
{
    public Guid UserId { get; set; }
    public Guid AgentId { get; set; }
    public Guid AgentApplicationId { get; set; }
    public Guid TenantId { get; set; }
    public bool IsMessagingEnabled { get; set; } = false;
}
