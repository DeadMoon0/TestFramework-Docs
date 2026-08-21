# Runs, stages and steps

A timeline is the plan. A **run** is one execution of it. A run is organised into **stages**, and a
stage holds **steps**.

## The run

`SetupRun(...)` creates a run builder - the place where everything run-specific goes: the service
provider, the environment, variables, artifact data, and the output helper. `RunAsync()` executes it.

```csharp
TimelineRun run = await timeline.SetupRun(serviceProvider, outputHelper)
    .AddVariable("orderId", "42")
    .RunAsync();
```

Runs are isolated from each other. Two runs of the same timeline share the plan and nothing else, which
is what makes a `static readonly` timeline per test class safe.

A finished run is an immutable record. It is what the assertion handles read, what the debug view
renders, and what `EnsureRanToCompletion()` inspects.

## Stages

Stages separate work that must not be interleaved. The two you will always meet are the **main stage**,
which holds the steps you authored, and the **cleanup stage**, which runs afterwards.

The distinction earns its keep on the failure path: cleanup runs even when the main stage failed. That
is why artifact teardown is reliable rather than best-effort - it is not attached to the success of your
steps.

## Steps

A step is one executable unit. The builder verbs (`Trigger`, `WaitForEvent`, `SetVariable`, `Transform`,
`AssertVariable`, `RegisterArtifact`, ...) emit steps, and each verb returns a modifier chain so the step
can be configured inline:

```csharp
.Trigger(SimpleExt.Trigger.Message("hello"))
    .Name("greet")
    .WithRetry(3, CalcDelays.Fixed(TimeSpan.Zero))
    .WithTimeOut(TimeSpan.FromSeconds(5))
```

Four modifiers cover nearly everything: `Name(...)` for assertion handles, `WithRetry(...)` for
transient failure, `WithTimeOut(...)` for bounded waiting, and `DoNotParallelize()` for exclusivity.

## Phases: why authored order survives

Each step declares a **phase**, and phases order themselves. In the built-in planner:

| Phase | Holds | Mergeable |
|---|---|---|
| `Prepare` | setting up data and variables | yes |
| `Act` | causing something to happen | no |
| `Observe` | waiting for and reading the effect | no |
| `Materialize` | registering results and artifacts | yes |

So the common `Trigger → WaitForEvent → RegisterArtifact` shape executes in the order you wrote it
without any explicit dependency declaration - the trigger acts, the event observes, the registration
materialises.

This is the mechanism behind a property worth relying on: **authored order and executed order agree
unless something deliberately says otherwise.**

## IO contracts

A step also declares what it needs and what it produces. The run validates that graph before executing
anything, so a missing input is a planning failure rather than a half-finished side effect:

```csharp
// Cmd declares that it needs cmdCommand. Supply it and the plan is valid.
.Trigger(LocalIOExt.Trigger.Cmd(Var.Ref<string>("cmdCommand")))
.GetExitCode("ExitCode")
```

The same declarations tell the planner which mergeable steps may share a layer - see
[parallel execution](parallel-execution.md).

## Writing your own step

A custom step implements `Step<T>` with a result context type, and provides `Execute(...)`, `Clone()`,
`GetInstance()` and `DeclareIO(...)`. `Clone()` and `DeclareIO(...)` are the two that matter: cloning is
what lets one frozen plan produce independent instances per run, and the IO declaration is what lets the
planner reason about the step at all.

At that point you are an extension author rather than a consumer. <xref:TestFramework.Core.Steps> has
the surface.

## See also

- [The timeline](timeline.md) - why the plan is frozen
- [Parallel execution](parallel-execution.md) - how layers are chosen
- <xref:TestFramework.Core.Timelines> - the reference
