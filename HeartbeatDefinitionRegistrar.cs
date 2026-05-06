using Microsoft.Extensions.Hosting;

namespace ControlAgentNet.Features.Heartbeat;

internal sealed class HeartbeatDefinitionRegistrar : IHostedService
{
    private readonly IEnumerable<HeartbeatDefinition> _definitions;
    private readonly IHeartbeatStore _store;

    public HeartbeatDefinitionRegistrar(
        IEnumerable<HeartbeatDefinition> definitions,
        IHeartbeatStore store)
    {
        _definitions = definitions;
        _store = store;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTimeOffset.UtcNow;

        foreach (var definition in _definitions)
        {
            await _store.EnsureRegisteredAsync(definition, nowUtc, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
