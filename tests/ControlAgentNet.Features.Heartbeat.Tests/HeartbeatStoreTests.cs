using Xunit;

namespace ControlAgentNet.Features.Heartbeat.Tests;

public class HeartbeatStoreTests
{
    [Fact]
    public async Task RecordAsync_updates_registered_snapshot()
    {
        var store = new InMemoryHeartbeatStore();
        await store.EnsureRegisteredAsync(
            new HeartbeatDefinition(
                HeartbeatId: "web",
                DisplayName: "Web",
                Prompt: "Revisa la web.",
                CronExpression: "0 20 * * *",
                TimeZoneId: "UTC",
                RunOnStartup: true,
                AllowConcurrentRuns: false,
                UserId: "heartbeat",
                TenantId: null,
                ConversationId: "heartbeat:web",
                Metadata: new Dictionary<string, string>()),
            DateTimeOffset.Parse("2026-05-03T19:00:00Z"));

        await store.RecordAsync("web", new HeartbeatResult
        {
            Status = HeartbeatStatus.Failed,
            Error = "timeout"
        }, DateTimeOffset.Parse("2026-05-03T21:00:00Z"));

        var snapshot = await store.GetAsync("web");

        Assert.NotNull(snapshot);
        Assert.Equal(HeartbeatStatus.Failed, snapshot!.Status);
        Assert.Equal("timeout", snapshot.LastError);
        Assert.Equal("Revisa la web.", snapshot.Prompt);
    }
}
