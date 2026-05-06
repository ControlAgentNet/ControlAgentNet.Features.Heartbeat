using ControlAgentNet.Core.Descriptors;

namespace ControlAgentNet.Features.Heartbeat;

internal static class HeartbeatChannelDescriptor
{
    public const string Id = "heartbeat";

    public static ChannelDescriptor Create(HeartbeatOptions options)
        => new(
            Id: options.ChannelId,
            Name: options.ChannelName,
            Description: "Synthetic channel for scheduled ControlAgentNet heartbeat prompts.",
            DefaultEnabled: true,
            Transport: ChannelTransportKind.Scheduled,
            Version: "1.0.0",
            SourceAssembly: typeof(HeartbeatExtensions).Assembly.GetName().Name ?? nameof(ControlAgentNet.Features.Heartbeat),
            Category: "automation");
}
