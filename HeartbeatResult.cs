namespace ControlAgentNet.Features.Heartbeat;

public sealed record HeartbeatResult
{
    public required HeartbeatStatus Status { get; init; }

    public string? ResponseText { get; init; }

    public string? Error { get; init; }
}
