# Waiting for events

Integration tests spend most of their time waiting. The difference between a good one and a flaky one
is whether that waiting is declared or improvised.

## Declare the condition, not the polling

`WaitForEvent(...)` names the condition the world must satisfy. The framework does the waiting:

```csharp
private readonly Timeline _timeline = Timeline.Create()
    .Trigger(LocalIOExt.Trigger.Cmd(Var.Ref<string>("cmdCreate"), Var.Ref<string>("cwd")))
    .WaitForEvent(LocalIOExt.Events.FileExists(Var.Ref<string>("artifactPath")))
    .RegisterArtifact("newFile", LocalIOExt.Artifacts.FileRef(Var.Ref<string>("artifactPath")))
    .Build();
```

Three steps, in authored order: cause something, wait for its effect, then start tracking the result.
No `Task.Delay`, no retry loop, no "it works on my machine because my machine is slow enough".

## Give a wait a deadline shorter than the default

Every step already has one: `TimeOutOptions` defaults to 10 minutes. So an unbounded wait fails
eventually rather than hanging forever - but ten minutes of a test doing nothing is a long time to wait
for that news. State the deadline the wait actually deserves:

```csharp
.WaitForEvent(LocalIOExt.Events.FileExists(Var.Const(outputPath)))
    .WithTimeOut(TimeSpan.FromSeconds(10))
```

On timeout, the event reports what it was watching - the resolved path, not the template - so a
mismatched working directory is visible rather than mysterious.

## Ordering comes from phases, not from luck

You did not have to tell the planner that the wait must follow the trigger. Steps declare a phase, and
the phases order themselves: local command triggers act in `Act`, file polling observes in `Observe`,
artifact registration materialises in `Materialize`. The common
`Trigger → WaitForEvent → RegisterArtifact` shape therefore executes in the order you wrote it without
a single explicit dependency.

That is worth internalising, because it is why timelines stay readable as they grow: authored order and
executed order agree unless something deliberately says otherwise.

## Waiting on other systems

The same verb covers every package. Only the event changes:

| Waiting for | Event |
|---|---|
| A file to appear | `LocalIOExt.Events.FileExists(...)` |
| A stub to be called | `WebExt.Stub.Called("pricing", HttpMethod.Post, "/api/quotes")` |
| A Service Bus message | `AzureExt.Event.ServiceBus.MessageReceived(...)` |

## Where to look next

- Run it: chapter 06 in the [Examples](../examples/index.md)
- Concept: [runs, stages and steps](../guide/concepts/runs-stages-steps.md)

Next: [asserting properly](asserting.md).
