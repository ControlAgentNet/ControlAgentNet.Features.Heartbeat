using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ControlAgentNet.Core.Abstractions;
using ControlAgentNet.Core.Models;
using ControlAgentNet.Features.CronJobs;
using ControlAgentNet.Features.Heartbeat;
using ControlAgentNet.Runtime.Extensions;
using ControlAgentNet.Runtime.Tools;
using ControlAgentNet.Tools.Greeting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddControlAgentNet(builder.Configuration, builder.Environment, includeAgentOrchestrator: false, configureAgent: options =>
{
    options.Id = "heartbeat-demo-agent";
    options.Name = "Heartbeat Demo Agent";
    options.Description = "Demo host for prompt-driven heartbeats invoking registered tools.";
    options.Instructions = "Use the Greeting tool when the heartbeat asks for a greeting.";
})
    .AddGreetingTools()
    .AddHeartbeat(options =>
    {
        options.HeartbeatId = "greeting-every-minute";
        options.DisplayName = "Greeting Every Minute";
        options.Prompt = """
        Invoke the Greeting tool with the name "Heartbeat Demo".
        Return only the tool result.
        """;
        options.CronExpression = "*/1 * * * *";
        options.TimeZoneId = "America/Caracas";
    });

builder.Services.AddSingleton<IAgentOrchestrator, GreetingHeartbeatOrchestrator>();

using var host = builder.Build();

var hostedServices = host.Services.GetServices<IHostedService>().ToList();
var registrar = hostedServices
    .Single(service => string.Equals(service.GetType().Name, "HeartbeatDefinitionRegistrar", StringComparison.Ordinal));
var cronWorker = host.Services.GetServices<IHostedService>()
    .OfType<CronJobWorker>()
    .Single();

await registrar.StartAsync(CancellationToken.None);
await cronWorker.RunOnceAsync();

var snapshots = await host.Services.GetRequiredService<IHeartbeatStore>().ListAsync();
Console.WriteLine($"Registered heartbeats: {snapshots.Count}");
foreach (var snapshot in snapshots)
{
    Console.WriteLine($"- {snapshot.HeartbeatId}: {snapshot.DisplayName} [{snapshot.Status}]");
    if (!string.IsNullOrWhiteSpace(snapshot.LastResultText))
    {
        Console.WriteLine($"  Result: {snapshot.LastResultText}");
    }

    if (!string.IsNullOrWhiteSpace(snapshot.LastError))
    {
        Console.WriteLine($"  Error: {snapshot.LastError}");
    }
}

internal sealed partial class GreetingHeartbeatOrchestrator : IAgentOrchestrator
{
    private readonly ToolRegistry _toolRegistry;

    public GreetingHeartbeatOrchestrator(ToolRegistry toolRegistry)
    {
        _toolRegistry = toolRegistry;
    }

    public async Task<OutgoingMessage> ProcessAsync(IncomingMessage message, CancellationToken cancellationToken = default)
    {
        var greetingTool = _toolRegistry.GetEnabledTools()
            .OfType<AIFunction>()
            .Single(tool => string.Equals(tool.Name, "Greeting", StringComparison.Ordinal));

        var name = ExtractGreetingName(message.Text) ?? "friend";
        var result = await greetingTool.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?> { ["argument"] = name }),
            cancellationToken).ConfigureAwait(false);

        return new OutgoingMessage
        {
            ConversationId = message.ConversationId,
            Text = result?.ToString() ?? string.Empty,
            ChannelId = message.ChannelId,
            ChannelType = message.ChannelType
        };
    }

    public Task<OutgoingMessage> ProcessStreamAsync(IncomingMessage message, CancellationToken cancellationToken = default)
        => ProcessAsync(message, cancellationToken);

    private static string? ExtractGreetingName(string prompt)
    {
        var match = GreetingNameRegex().Match(prompt);
        return match.Success ? match.Groups["name"].Value : null;
    }

    [GeneratedRegex("\"(?<name>[^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex GreetingNameRegex();
}
