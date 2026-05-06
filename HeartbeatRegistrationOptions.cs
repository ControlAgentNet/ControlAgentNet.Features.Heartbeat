namespace ControlAgentNet.Features.Heartbeat;

public class HeartbeatRegistrationOptions
{
    public string HeartbeatId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;

    public string CronExpression { get; set; } = string.Empty;

    public string? TimeZoneId { get; set; }

    public bool RunOnStartup { get; set; } = true;

    public bool AllowConcurrentRuns { get; set; }

    public string? UserId { get; set; }

    public string? TenantId { get; set; }

    public string? ConversationId { get; set; }

    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
