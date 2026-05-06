namespace ControlAgentNet.Features.Heartbeat;

public sealed record HeartbeatSnapshot
{
    public required string HeartbeatId { get; init; }

    public required string DisplayName { get; init; }

    public required string Prompt { get; init; }

    public required string CronExpression { get; init; }

    public required string TimeZoneId { get; init; }

    public required string ConversationId { get; init; }

    public string? UserId { get; init; }

    public string? TenantId { get; init; }

    public required HeartbeatStatus Status { get; set; }

    public DateTimeOffset RegisteredAtUtc { get; set; }

    public DateTimeOffset? LastRunAtUtc { get; set; }

    public string? LastResultText { get; set; }

    public string? LastError { get; set; }

    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
