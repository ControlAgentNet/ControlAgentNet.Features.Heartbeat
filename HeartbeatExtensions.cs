using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ControlAgentNet.Core.Abstractions;
using ControlAgentNet.Core.Models;
using ControlAgentNet.Features.CronJobs;
using ControlAgentNet.Policies;
using ControlAgentNet.Runtime.Extensions;

namespace ControlAgentNet.Features.Heartbeat;

public static class HeartbeatExtensions
{
    public static IControlAgentNetBuilder AddHeartbeat(
        this IControlAgentNetBuilder builder,
        Action<HeartbeatRegistrationOptions> configure)
    {
        builder.AddHeartbeatInfrastructure();

        var heartbeatOptions = new HeartbeatRegistrationOptions();
        configure(heartbeatOptions);
        var runtimeOptions = BindRuntimeOptions(builder.Configuration, configure: null);
        ValidateHeartbeatOptions(heartbeatOptions, runtimeOptions);

        var definition = CreateDefinition(heartbeatOptions, runtimeOptions);
        builder.Services.AddSingleton(definition);

        return builder.AddCronJob(
            options =>
            {
                options.JobId = definition.HeartbeatId;
                options.CronExpression = definition.CronExpression;
                options.TimeZoneId = definition.TimeZoneId;
                options.RunOnStartup = definition.RunOnStartup;
                options.AllowConcurrentRuns = definition.AllowConcurrentRuns;
            },
            async (serviceProvider, context, cancellationToken) =>
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                var runtime = scope.ServiceProvider.GetRequiredService<IOptions<HeartbeatOptions>>().Value;
                var agentOptions = scope.ServiceProvider.GetRequiredService<IOptions<AgentOptions>>().Value;
                var orchestrator = scope.ServiceProvider.GetRequiredService<IAgentOrchestrator>();
                var store = scope.ServiceProvider.GetRequiredService<IHeartbeatStore>();
                var channelPolicyStore = scope.ServiceProvider.GetService<IChannelPolicyStore>();
                var tenantId = definition.TenantId ?? agentOptions.TenantId;
                var userId = string.IsNullOrWhiteSpace(definition.UserId)
                    ? runtime.DefaultUserId
                    : definition.UserId;

                if (channelPolicyStore is not null)
                {
                    var policy = await channelPolicyStore.ResolveChannelPolicyAsync(
                        runtime.ChannelId,
                        new PolicyContext(tenantId, agentOptions.Id, runtime.ChannelId, userId),
                        cancellationToken).ConfigureAwait(false);

                    if (policy is PolicyValue.Disabled or PolicyValue.ApprovalRequired)
                    {
                        var error = policy == PolicyValue.Disabled
                            ? "Heartbeat channel disabled by policy."
                            : "Heartbeat channel requires approval and cannot run automatically.";
                        await store.RecordAsync(
                            definition.HeartbeatId,
                            new HeartbeatResult
                            {
                                Status = HeartbeatStatus.BlockedByPolicy,
                                Error = error
                            },
                            context.TriggeredAtUtc,
                            cancellationToken).ConfigureAwait(false);
                        return;
                    }
                }

                try
                {
                    var response = await orchestrator.ProcessAsync(new IncomingMessage
                    {
                        ConversationId = definition.ConversationId,
                        TenantId = tenantId,
                        UserId = userId!,
                        Text = definition.Prompt,
                        ChannelId = runtime.ChannelId,
                        ChannelType = Core.Descriptors.ChannelTransportKind.Scheduled,
                        Timestamp = context.TriggeredAtUtc,
                        CorrelationId = Guid.NewGuid().ToString("N"),
                        Metadata = CreateExecutionMetadata(definition, agentOptions.Id, context)
                    }, cancellationToken).ConfigureAwait(false);

                    await store.RecordAsync(
                        definition.HeartbeatId,
                        new HeartbeatResult
                        {
                            Status = HeartbeatStatus.Succeeded,
                            ResponseText = response.Text
                        },
                        context.TriggeredAtUtc,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    await store.RecordAsync(
                        definition.HeartbeatId,
                        new HeartbeatResult
                        {
                            Status = HeartbeatStatus.Failed,
                            Error = ex.Message
                        },
                        context.TriggeredAtUtc,
                        cancellationToken).ConfigureAwait(false);
                }
            });
    }

    private static IControlAgentNetBuilder AddHeartbeatInfrastructure(this IControlAgentNetBuilder builder)
    {
        builder.AddCronJobs();
        builder.Services.AddOptions<HeartbeatOptions>()
            .Bind(builder.Configuration.GetSection(HeartbeatOptions.SectionName));
        var runtimeOptions = BindRuntimeOptions(builder.Configuration, configure: null);

        if (runtimeOptions.RegisterInMemoryStore)
        {
            builder.Services.TryAddSingleton<IHeartbeatStore, InMemoryHeartbeatStore>();
        }

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, HeartbeatDefinitionRegistrar>());
        builder.AddChannelDescriptor(HeartbeatChannelDescriptor.Create(runtimeOptions));

        return builder;
    }

    private static HeartbeatOptions BindRuntimeOptions(IConfiguration configuration, Action<HeartbeatOptions>? configure)
    {
        var options = new HeartbeatOptions();
        configuration.GetSection(HeartbeatOptions.SectionName).Bind(options);
        configure?.Invoke(options);
        return options;
    }

    private static void ValidateHeartbeatOptions(HeartbeatRegistrationOptions options, HeartbeatOptions runtimeOptions)
    {
        if (string.IsNullOrWhiteSpace(options.HeartbeatId))
        {
            throw new ArgumentException("HeartbeatId is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.Prompt))
        {
            throw new ArgumentException("Prompt is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.CronExpression))
        {
            throw new ArgumentException("CronExpression is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.UserId)
            && string.IsNullOrWhiteSpace(runtimeOptions.DefaultUserId))
        {
            throw new ArgumentException("A default user id is required for heartbeat execution.", nameof(runtimeOptions));
        }
    }

    private static HeartbeatDefinition CreateDefinition(
        HeartbeatRegistrationOptions options,
        HeartbeatOptions runtimeOptions)
        => new(
            HeartbeatId: options.HeartbeatId.Trim(),
            DisplayName: ResolveDisplayName(options),
            Prompt: options.Prompt.Trim(),
            CronExpression: options.CronExpression.Trim(),
            TimeZoneId: string.IsNullOrWhiteSpace(options.TimeZoneId) ? "UTC" : options.TimeZoneId.Trim(),
            RunOnStartup: options.RunOnStartup,
            AllowConcurrentRuns: options.AllowConcurrentRuns,
            UserId: string.IsNullOrWhiteSpace(options.UserId) ? runtimeOptions.DefaultUserId : options.UserId.Trim(),
            TenantId: string.IsNullOrWhiteSpace(options.TenantId) ? null : options.TenantId.Trim(),
            ConversationId: ResolveConversationId(options),
            Metadata: new Dictionary<string, string>(options.Metadata, StringComparer.OrdinalIgnoreCase));

    private static IReadOnlyDictionary<string, string> CreateExecutionMetadata(
        HeartbeatDefinition definition,
        string agentId,
        CronJobExecutionContext context)
    {
        var metadata = new Dictionary<string, string>(definition.Metadata, StringComparer.OrdinalIgnoreCase)
        {
            ["heartbeat_id"] = definition.HeartbeatId,
            ["heartbeat_display_name"] = definition.DisplayName,
            ["controlagentnet_agent_id"] = agentId,
            ["scheduled_at_utc"] = context.ScheduledAtUtc.ToString("O"),
            ["triggered_at_utc"] = context.TriggeredAtUtc.ToString("O")
        };

        return metadata;
    }

    private static string ResolveConversationId(HeartbeatRegistrationOptions options)
        => string.IsNullOrWhiteSpace(options.ConversationId)
            ? $"heartbeat:{options.HeartbeatId.Trim()}"
            : options.ConversationId.Trim();

    private static string ResolveDisplayName(HeartbeatRegistrationOptions options)
        => string.IsNullOrWhiteSpace(options.DisplayName)
            ? options.HeartbeatId.Trim()
            : options.DisplayName.Trim();
}
