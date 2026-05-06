namespace ControlAgentNet.Features.Heartbeat;

public sealed record HeartbeatDefinition(
    string HeartbeatId,
    string DisplayName,
    string Prompt,
    string CronExpression,
    string TimeZoneId,
    bool RunOnStartup,
    bool AllowConcurrentRuns,
    string? UserId,
    string? TenantId,
    string ConversationId,
    IReadOnlyDictionary<string, string> Metadata);
