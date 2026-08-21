# Variables

Variables are the data channel: between the build phase and the run, and between steps within a run. A
frozen plan plus per-run variables is what lets one timeline serve many cases.

## The three ways a value arrives

| Source | Written as | Known when |
|---|---|---|
| A literal in the plan | `Var.Const(value)` | authoring |
| An input supplied per run | `Var.Ref<T>("name")` + `AddVariable("name", value)` | run setup |
| Something a step produced | `GetExitCode("name")` and friends | during the run |

```csharp
Timeline timeline = Timeline.Create()
    .SetVariable("greeting", Var.Const("Good morning"))
    .Trigger(LocalIOExt.Trigger.Cmd(Var.Ref<string>("cmdCommand")))
    .GetExitCode("ExitCode")
    .Build();

TimelineRun run = await timeline.SetupRun(outputHelper)
    .AddVariable("cmdCommand", "echo hello")
    .RunAsync();
```

## Reference or immutable reference

`Var.Ref<T>(...)` is a reference resolved when the step executes. `Var.RefImmutable<T>(...)` is a
reference that must already have a value before execution begins.

Control flow uses the immutable form, and not arbitrarily: a branch has to be decided before the run
starts moving through it, so a value that could still change is not admissible.

```csharp
.Conditional(Var.RefImmutable<bool>("doPathA"), thenBranch => { /* ... */ })
.ForEach(Var.RefImmutable<string[]>("messages"), "item", loop => { /* ... */ })
```

Anything passed to `AddVariable(...)` at run setup satisfies that requirement by construction.

## Transform at the point of use

A reference can be reshaped where it is consumed, which keeps the source value simple and avoids a
variable per format:

```csharp
.Trigger(SimpleExt.Trigger.Message(
    Var.Ref<string>("cmdCommand").Transform(x => x + ". And it is even Transformed!")))
```

For a transformation worth naming - one other steps also need - use the builder's `Transform(...)` verb
instead, which produces a named variable rather than an inline reshape.

## Loop variables

`ForEach(..., "item", ...)` binds `"item"` inside the loop body, so the body reads it like any other
reference:

```csharp
.ForEach(["Alice", "Bob", "Charlie"], "item", loop =>
{
    loop.Trigger(SimpleExt.Trigger.Message(Var.Ref<string>("item"))).Name("greet");
})
```

Each iteration is a real step instance. One label therefore covers many instances, which is what
`run.Steps("greet")` is for - see [assertions](assertions.md).

## Reading variables afterwards

```csharp
run.Variable<int>("ExitCode").Should().Exist().And().Be(0);
```

Existence and value are separate assertions on purpose. A variable no step ever produced is a different
defect from one that came out wrong, and collapsing the two costs you the distinction exactly when you
need it.

## Declared, therefore validated

Steps declare the variables they consume and produce, so the run rejects an incomplete plan before
executing anything. A missing `AddVariable(...)` surfaces as an `IOContractViolationException` from run
setup, naming the variable - not as a confusing failure inside a shell command.

## See also

- [Learn: passing data between steps](../../learn/passing-data.md)
- <xref:TestFramework.Core.Variables>
