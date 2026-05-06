# ControlAgentNet.Features.Heartbeat

Prompt-driven heartbeats built on top of `ControlAgentNet.Features.CronJobs`.

## What It Does

- Runs periodic agent instructions using cron schedules
- Sends each heartbeat to the agent through a synthetic `heartbeat` channel
- Stores the latest execution result, error, and policy-blocked state
- Integrates with channel policies for the synthetic heartbeat channel
- Supports many heartbeats in the same host

## Usage

### Prompt-driven heartbeat

```csharp
using ControlAgentNet.Features.Heartbeat;

builder.Services.AddControlAgentAgent(builder.Configuration, builder.Environment, configureAgent: options =>
{
    options.Id = "my-agent";
    options.Name = "My Agent";
    options.Instructions = "You are a helpful assistant.";
})
    .AddHeartbeat(options =>
    {
        options.HeartbeatId = "website-review";
        options.DisplayName = "Website Review";
        options.Prompt = """
        Revisa https://miweb.com.
        Usa las tools disponibles para verificar si esta caida.
        Si detectas error, resume la causa probable y el impacto.
        """;
        options.CronExpression = "0 20 * * *";
        options.TimeZoneId = "America/Caracas";
    });
```

### Heartbeat invoking a tool every minute

This is the simplest “OpenClaw-style” integration example: the heartbeat is a prompt, and the agent resolves it by calling a registered tool.

```csharp
using ControlAgentNet.Features.Heartbeat;
using ControlAgentNet.Tools.Greeting;

builder.Services.AddControlAgentAgent(builder.Configuration, builder.Environment, configureAgent: options =>
{
    options.Id = "my-agent";
    options.Name = "My Agent";
    options.Instructions = "Use the Greeting tool when a heartbeat asks for a greeting.";
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
```

In the sample host, the orchestrator resolves the registered `Greeting` tool from `ToolRegistry` and invokes it. See [samples/HeartbeatDemo/Program.cs](samples/HeartbeatDemo/Program.cs).

### Multiple heartbeats

```csharp
builder.Services.AddControlAgentAgent(builder.Configuration, builder.Environment, configureAgent: options =>
{
    options.Id = "my-agent";
    options.Name = "My Agent";
    options.Instructions = "You are a helpful assistant.";
})
    .AddHeartbeat(options =>
    {
        options.HeartbeatId = "website-review";
        options.Prompt = "Revisa https://miweb.com y resume si esta operativa.";
        options.CronExpression = "0 20 * * *";
        options.TimeZoneId = "America/Caracas";
    })
    .AddHeartbeat(options =>
    {
        options.HeartbeatId = "incident-summary";
        options.Prompt = "Revisa los incidentes abiertos y resume las alertas criticas.";
        options.CronExpression = "0 */2 * * *";
        options.TimeZoneId = "America/Caracas";
    });
```

## Model

- `AddHeartbeat(...)` registers a host-defined prompt to run on a cron schedule
- Each run enters the runtime as an `IncomingMessage` on channel `heartbeat`
- Tool guards, policy guards, and human approval still apply when the agent uses tools
- `IChannelPolicyStore` can disable or require approval for the synthetic heartbeat channel
- Execution state is recorded in `IHeartbeatStore`
