namespace ControlAgentNet.Features.Heartbeat;

public interface IHeartbeatStore
{
    Task EnsureRegisteredAsync(
        HeartbeatDefinition definition,
        DateTimeOffset registeredAtUtc,
        CancellationToken cancellationToken = default);

    Task RecordAsync(
        string heartbeatId,
        HeartbeatResult result,
        DateTimeOffset executedAtUtc,
        CancellationToken cancellationToken = default);

    Task<HeartbeatSnapshot?> GetAsync(string heartbeatId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HeartbeatSnapshot>> ListAsync(CancellationToken cancellationToken = default);
}
