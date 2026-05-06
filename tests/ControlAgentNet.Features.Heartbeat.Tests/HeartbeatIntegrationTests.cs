using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ControlAgentNet.Core.Abstractions;
using ControlAgentNet.Core.Descriptors;
using ControlAgentNet.Core.Models;
using ControlAgentNet.Features.CronJobs;
using ControlAgentNet.Policies;
using ControlAgentNet.Runtime.Extensions;
using Xunit;

namespace ControlAgentNet.Features.Heartbeat.Tests;

public class HeartbeatIntegrationTests
{
    [Fact]
    public async Task AddHeartbeat_runs_prompt_and_records_snapshot()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAgentOrchestrator, FakeOrchestrator>();
        services.AddControlAgentNet(CreateConfiguration(), new TestHostEnvironment(), includeAgentOrchestrator: false, configureAgent: options =>
            {
                options.Id = "agent-1";
                options.TenantId = "tenant-1";
            })
            .AddHeartbeat(options =>
            {
                options.HeartbeatId = "web";
                options.Prompt = "Revisa la web.";
                options.CronExpression = "0 20 * * *";
                options.RunOnStartup = true;
            });

        using var provider = services.BuildServiceProvider();
        var registrar = provider.GetServices<IHostedService>()
            .Single(service => string.Equals(service.GetType().Name, "HeartbeatDefinitionRegistrar", StringComparison.Ordinal));
        await registrar.StartAsync(default);
        var stateStore = provider.GetRequiredService<ICronJobStateStore>();
        var heartbeatStore = provider.GetRequiredService<IHeartbeatStore>();
        var worker = new CronJobWorker(
            provider,
            provider.GetServices<CronJobDefinition>(),
            stateStore,
            new FakeClock(DateTimeOffset.Parse("2026-05-03T20:00:00Z")),
            Options.Create(new CronJobsOptions { PollInterval = TimeSpan.FromMilliseconds(1) }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CronJobWorker>.Instance);

        await worker.RunOnceAsync();
        var snapshot = await heartbeatStore.GetAsync("web");

        Assert.NotNull(snapshot);
        Assert.Equal(HeartbeatStatus.Succeeded, snapshot!.Status);
        Assert.Equal("agent:Revisa la web.", snapshot.LastResultText);
        Assert.Equal("heartbeat:web", snapshot.ConversationId);
    }

    [Fact]
    public async Task AddHeartbeat_blocks_execution_when_channel_policy_requires_approval()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAgentOrchestrator, FakeOrchestrator>();
        services.AddSingleton<IChannelPolicyStore>(new FakeChannelPolicyStore(PolicyValue.ApprovalRequired));
        services.AddControlAgentNet(CreateConfiguration(), new TestHostEnvironment(), includeAgentOrchestrator: false, configureAgent: options => options.Id = "agent-1")
            .AddHeartbeat(options =>
            {
                options.HeartbeatId = "web";
                options.Prompt = "Revisa la web.";
                options.CronExpression = "0 20 * * *";
                options.RunOnStartup = true;
            });

        using var provider = services.BuildServiceProvider();
        var registrar = provider.GetServices<IHostedService>()
            .Single(service => string.Equals(service.GetType().Name, "HeartbeatDefinitionRegistrar", StringComparison.Ordinal));
        await registrar.StartAsync(default);
        var worker = new CronJobWorker(
            provider,
            provider.GetServices<CronJobDefinition>(),
            provider.GetRequiredService<ICronJobStateStore>(),
            new FakeClock(DateTimeOffset.Parse("2026-05-03T20:00:00Z")),
            Options.Create(new CronJobsOptions { PollInterval = TimeSpan.FromMilliseconds(1) }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CronJobWorker>.Instance);

        await worker.RunOnceAsync();
        var snapshot = await provider.GetRequiredService<IHeartbeatStore>().GetAsync("web");

        Assert.NotNull(snapshot);
        Assert.Equal(HeartbeatStatus.BlockedByPolicy, snapshot!.Status);
        Assert.Equal("Heartbeat channel requires approval and cannot run automatically.", snapshot.LastError);
    }

    private static IConfiguration CreateConfiguration()
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

    private sealed class FakeClock(DateTimeOffset nowUtc) : ICronClock
    {
        public DateTimeOffset UtcNow => nowUtc;
    }

    private sealed class FakeOrchestrator : IAgentOrchestrator
    {
        public Task<OutgoingMessage> ProcessAsync(IncomingMessage message, CancellationToken cancellationToken = default)
            => Task.FromResult(new OutgoingMessage
            {
                ConversationId = message.ConversationId,
                Text = $"agent:{message.Text}",
                ChannelId = message.ChannelId,
                ChannelType = ChannelTransportKind.Scheduled
            });

        public Task<OutgoingMessage> ProcessStreamAsync(IncomingMessage message, CancellationToken cancellationToken = default)
            => ProcessAsync(message, cancellationToken);
    }

    private sealed class FakeChannelPolicyStore(PolicyValue resolvedPolicy) : IChannelPolicyStore
    {
        public PolicyValue? GetChannelPolicy(string channelId, PolicyContext? context = null) => null;

        public Task SetChannelPolicyAsync(string channelId, PolicyValue value, PolicyContext? context = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public IReadOnlyList<ConfiguredPolicyRecord> ListChannelPolicies(string? channelId = null, PolicyContext? context = null) => [];

        public IReadOnlyList<ConfiguredPolicyRecord> ListChannelPolicyHistory(string channelId, PolicyContext? context = null, int take = 50) => [];

        public Task RestoreChannelPolicyAsync(string channelId, PolicyContext? context = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<PolicyValue> ResolveChannelPolicyAsync(string channelId, PolicyContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(resolvedPolicy);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
