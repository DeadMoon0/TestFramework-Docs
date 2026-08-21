# The timeline

A timeline is the plan. You compose it once, freeze it, and then execute it as often as you like.

```csharp
Timeline timeline = Timeline.Create()
    .SetVariable("name", Var.Const("Alex"))
    .Build();
```

`Build()` is the moment the plan stops being editable. Everything before it is authoring; everything
after it is execution.

## Why it is frozen

Because a plan that can change while it runs cannot be trusted to describe what ran. Freezing buys
three things that integration tests need badly:

- **A run cannot corrupt the plan.** Two runs of the same timeline cannot interfere, so they can share
  one instance. A `static readonly` field per test class and a plain `readonly` instance field are both
  common; the Showroom uses roughly equal numbers of each.
- **The plan can be inspected before anything executes.** The stage planner decides parallelism from
  the frozen graph, which is only sound if the graph is final.
- **A failure has a fixed reference.** When a run fails you compare it against a plan that has not
  moved.

Attempting to mutate anything frozen throws `FrameworkStateException` - "This instance has been frozen
and is read-only" - rather than quietly succeeding. Freezing applies to the step options too, not only
the timeline. See <xref:TestFramework.Core.Timelines.Timeline.Freeze> and the `IsFrozen` property.

## Stages and steps

A run is organised into **stages**, and a stage contains **steps**. The builder's verbs emit steps
into the main stage; cleanup runs in its own stage afterwards, which is why a cleanup still happens
when the main stage failed.

The planner may execute authored steps in parallel when all four of these hold:

1. they share a phase, and that phase is mergeable,
2. their declared IO contracts do not conflict,
3. neither has been marked sequential,
4. they do not share a serialised artifact-setup resource.

In the built-in planner `Prepare` and `Materialize` are mergeable, while `Act` and `Observe` stay
sequential - so side-effect ordering remains readable. `DoNotParallelize()` opts a single step out.

## One timeline, many runs

`SetupRun(...)` does not execute anything. It returns a builder for one specific run, which is where
run-specific inputs, services and output go. `RunAsync()` executes it and returns the finished run -
including a failed one, because a failure is a result you may want to assert against.

```csharp
TimelineRun run = await timeline.SetupRun(serviceProvider).RunAsync();
run.EnsureRanToCompletion();
```

`EnsureRanToCompletion()` is what turns "this run failed" into "this test failed". Without it, a
failed run is just an object you have not looked at yet.

## Where to next

- [Your first timeline](../../learn/first-timeline.md) walks this in the order you need it.
- [Example 01](../../examples/basic/01-minimal-timeline.md) is the smallest runnable version.
- <xref:TestFramework.Core.Timelines> has the full surface.
