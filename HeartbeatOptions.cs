namespace ControlAgentNet.Features.Heartbeat;

public sealed class HeartbeatOptions
{
    public const string SectionName = "ControlAgentNet:Features:Heartbeat";

    public string ChannelId { get; set; } = HeartbeatChannelDescriptor.Id;

    public string ChannelName { get; set; } = "Heartbeat";

    public string DefaultUserId { get; set; } = "heartbeat";

    public bool RegisterInMemoryStore { get; set; } = true;
}
