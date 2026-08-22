# TestFramework.Simple

Lightweight triggers for the cases where a full custom `Step<T>` class would be more ceremony than the
work deserves.

```bash
dotnet add package TestFramework.Simple
```

## Quickstart

```csharp
string? message = null;

Timeline timeline = Timeline.Create()
    .Trigger(SimpleExt.Trigger.Action(() => message = "Action executed"))
    .Build();

TimelineRun run = await timeline.SetupRun().RunAsync();

run.EnsureRanToCompletion();
Assert.Equal("Action executed", message);
```

## What it provides

| Entry point | For |
|---|---|
| `SimpleExt.Trigger.Action(...)` | running arbitrary code as a step |
| `SimpleExt.Trigger.Message(...)` | writing through the run logger, so the text lands in the run report |

`Message(...)` is more useful than it looks: because it writes through the run logger rather than to the
console, its output appears in the run report in position - which makes it a legitimate way to mark
progress inside a long timeline.

## Choosing an Action overload

Take the smallest overload that gives you what you need:

- `Action(Action)` - the step only needs to run code.
- `Action(Action<Dictionary<VariableIdentifier, object?>>, params VariableReferenceGeneric[])` - the step
  needs resolved variables.
- ...plus artifacts - when it needs both variables and artifact instances.
- ...plus `IServiceProvider` and `ScopedLogger` - when it also needs injected services or logging.

The richer overloads trade simplicity for reach: values arrive as dictionaries keyed by identifier, which
is less pleasant to read than a typed step but avoids writing a class for a three-line action.

```csharp
Timeline timeline = Timeline.Create()
    .SetVariable("name", Var.Const("Alex"))
    .Trigger(SimpleExt.Trigger.Action(
        vars => Console.WriteLine($"Hello {vars[new VariableIdentifier("name")]}"),
        Var.Ref<string>("name")))
    .Build();
```

Note that the variables an action uses are passed explicitly. That is not redundancy - it is the IO
declaration, and it is what lets the planner validate the plan and schedule the step.

## When to stop using it

Move to a real `Step<T>` when any of these becomes true:

- the same action appears in more than a couple of timelines,
- it needs a typed result other steps consume,
- it needs retry semantics that depend on the failure,
- or the dictionary lookups have started to outweigh the class you were avoiding.

An inline action is the right tool for glue. It is the wrong tool for a capability.

## Going deeper

- <xref:TestFramework.Simple>
- [Runs, stages and steps](../concepts/runs-stages-steps.md) - what a real step implements
- [Package guide in the repository](https://github.com/DeadMoon0/TestFramework-Core/blob/main/TestFramework.Simple/README.md)
