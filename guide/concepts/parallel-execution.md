# Parallel execution

Parallelism here is a scheduling decision the planner makes from the frozen plan - not something you
opt into, and not something you have to defend against.

## The four conditions

Two authored steps may share a layer only when all of these hold:

1. **They are in the same phase, and that phase is mergeable.** Different phases always order.
   `Prepare` and `Materialize` are mergeable; `Act` and `Observe` are not.
2. **Their IO contracts do not conflict.** Steps declare what they read and write, so the planner can
   see a conflict rather than guess at one.
3. **Neither is marked sequential** with `DoNotParallelize()`.
4. **They do not share a serialised setup resource.** An artifact type can declare that its setup runs
   one at a time, keyed by the resource it touches. Both SQL row describers do exactly this, keyed by
   database - so seeding two rows into one database is serialised, while seeding into two different
   databases still runs concurrently.

Fail any one and the steps run in order.

## Why Act and Observe stay sequential

Because test intent lives in that ordering. If two `Act` steps could be reordered or overlapped, the
scenario "create the order, then cancel it" would stop meaning anything specific. Keeping side-effect
order readable is worth more than the wall-clock saving.

`Prepare` and `Materialize` are different: assigning variables and registering results are
order-independent by nature, and that is where the free parallelism is.

## What it looks like

```csharp
private readonly Timeline _timeline = Timeline.Create()
    .SetVariable("greeting", Var.Const("Good morning"))
        .Name("set greeting")
    .SetVariable("subject", Var.Const("test subject"))
        .Name("set subject")
    .Build();
```

Both are `Prepare` work with no conflicting IO, so the planner places them in one layer. Run this with
the output helper attached and the debug view shows both steps inside the same `Prepare` layer - the run
report tells you what the planner decided, so you never have to infer it.

## Opting one step out

```csharp
.SetVariable("exclusive", Var.Const("..."))
    .Name("exclusive bulletin")
    .DoNotParallelize()
```

`DoNotParallelize()` makes a step a barrier inside its own phase: it gets the layer to itself. Reach for
it when a step touches something the IO contract cannot express - a process-wide setting, a shared
external file, an environment variable.

Note that this is an execution-policy decision, deliberately separate from IO declaration. Steps declare
what they use; the builder decides whether a step must be alone. Two jobs, two mechanisms.

## Layers in the run report

The debug view renders `Run → Stage → Layer → Step → Attempt`. The layer level is exactly this
concept made visible: everything in one layer ran together, and layers ran in sequence.

## What a failure does to a layer

The planner's unit of failure is the layer, not the step. A layer runs to completion - every step in it
was already started concurrently - and only then is it checked: if any step did not complete, the stage
stops and no later layer runs.

So a failure does not cancel its layer-mates, and it does not skip the cleanup stage, which runs
regardless. What it does stop is everything the planner had scheduled after it.

## Non-determinism is real, and honest

A parallel layer's log lines can appear in different orders across runs. That is not a defect in the
report - it is what actually happened. If you need a deterministic order, you need a sequential step, and
saying so with `DoNotParallelize()` is better than hoping.

## See also

- [Runs, stages and steps](runs-stages-steps.md) - phases in full
- Chapters 10 and 13 in the [Examples](../../examples/index.md)
