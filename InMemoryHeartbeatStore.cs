using System.Collections.Concurrent;

namespace ControlAgentNet.Features.Heartbeat;

public sealed class InMemoryHeartbeatStore : IHeartbeatStore
{
    private readonly ConcurrentDictionary<string, HeartbeatSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);

    public Task EnsureRegisteredAsync(
        HeartbeatDefinition definition,
        DateTimeOffset registeredAtUtc,
        CancellationToken cancellationToken = default)
    {
        _snapshots.AddOrUpdate(
            definition.HeartbeatId,
            _ => CreateRegisteredSnapshot(definition, registeredAtUtc),
            (_, previous) => previous with
            {
                DisplayName = definition.DisplayName,
                Prompt = definition.Prompt,
                CronExpression = definition.CronExpression,
                TimeZoneId = definition.TimeZoneId,
                ConversationId = definition.ConversationId,
                UserId = definition.UserId,
                TenantId = definition.TenantId,
                Metadata = new Dictionary<string, string>(definition.Metadata, StringComparer.OrdinalIgnoreCase)
            });

        return Task.CompletedTask;
    }

    public Task RecordAsync(
        string heartbeatId,
        HeartbeatResult result,
        DateTimeOffset executedAtUtc,
        CancellationToken cancellationToken = default)
    {
        _snapshots.AddOrUpdate(
            heartbeatId,
            _ => throw new InvalidOperationException($"Heartbeat '{heartbeatId}' was not registered."),
            (_, previous) => previous with
            {
                Status = result.Status,
                LastRunAtUtc = executedAtUtc,
                LastResultText = result.ResponseText,
                LastError = result.Error
            });

        return Task.CompletedTask;
    }

    public Task<HeartbeatSnapshot?> GetAsync(string heartbeatId, CancellationToken cancellationToken = default)
    {
        var snapshot = _snapshots.TryGetValue(heartbeatId, out var existing)
            ? Clone(existing)
            : null;
        return Task.FromResult(snapshot);
    }

    public Task<IReadOnlyList<HeartbeatSnapshot>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<HeartbeatSnapshot>>(
            _snapshots.Values
                .OrderBy(snapshot => snapshot.HeartbeatId, StringComparer.OrdinalIgnoreCase)
                .Select(Clone)
                .ToList());

    private static HeartbeatSnapshot CreateRegisteredSnapshot(
        HeartbeatDefinition definition,
        DateTimeOffset registeredAtUtc)
        => new()
        {
            HeartbeatId = definition.HeartbeatId,
            DisplayName = definition.DisplayName,
            Prompt = definition.Prompt,
            CronExpression = definition.CronExpression,
            TimeZoneId = definition.TimeZoneId,
            ConversationId = definition.ConversationId,
            UserId = definition.UserId,
            TenantId = definition.TenantId,
            Status = HeartbeatStatus.Registered,
            RegisteredAtUtc = registeredAtUtc,
            Metadata = new Dictionary<string, string>(definition.Metadata, StringComparer.OrdinalIgnoreCase)
        };

    private static HeartbeatSnapshot Clone(HeartbeatSnapshot snapshot)
        => snapshot with
        {
            Metadata = new Dictionary<string, string>(snapshot.Metadata, StringComparer.OrdinalIgnoreCase)
        };
}
