# Passing data between steps

A timeline is frozen, but the data flowing through it is not. Variables are how one immutable plan
runs a hundred different ways.

## A value known while authoring

`Var.Const(...)` puts a literal into the plan:

```csharp
Timeline timeline = Timeline.Create()
    .SetVariable("greeting", Var.Const("Good morning"))
    .Build();
```

## A value supplied per run

`Var.Ref<T>(...)` says "this value arrives later, under this name". The run supplies it:

```csharp
private readonly Timeline _timeline = Timeline.Create()
    .Trigger(SimpleExt.Trigger.Message(Var.Ref<string>("cmdCommand")))
    .Build();

[Fact]
public async Task Run()
{
    var run = await this._timeline.SetupRun(outputHelper)
        .AddVariable("cmdCommand", "Hello from Test via Var")
        .RunAsync();

    run.EnsureRanToCompletion();
}
```

The timeline is a field, shared across every test in the class. Only the `AddVariable(...)` calls
differ. That is the payoff: one plan, many inputs, no duplicated builders.

## A value a step produced

Steps also write variables. `GetExitCode(...)` takes what a command step produced and gives it a name
the rest of the run can use:

```csharp
private readonly Timeline _timeline = Timeline.Create()
    .Trigger(LocalIOExt.Trigger.Cmd(Var.Ref<string>("cmdCommand")))
    .GetExitCode("CmdExitCode")
    .Build();

// ...
run.Variable<int>("CmdExitCode").Should().Exist().And().Be(0);
```

Note `Should().Exist().And().Be(0)`. Existence and value are separate questions, and a variable that
was never produced is a different bug from one that came out wrong.

## Reshaping a value where it is used

A variable can be transformed at the point of use, so the source stays simple and the consumer still
gets the shape it needs:

```csharp
.Trigger(SimpleExt.Trigger.Message(
    Var.Ref<string>("cmdCommand").Transform(x => x + ". And it is even Transformed!")))
```

## Immutable references, for decisions

Control flow needs its inputs before execution starts moving, so branches read
`Var.RefImmutable<T>(...)` rather than `Var.Ref<T>(...)`:

```csharp
.Conditional(Var.RefImmutable<bool>("doPathA"), thenBranch =>
{
    thenBranch.Trigger(SimpleExt.Trigger.Message("Hello from Path A"));
})
```

Anything supplied at `SetupRun(...)` time qualifies as immutable by definition - it cannot change once
the run has begun.

## Where to look next

- Run it: chapter 04 in the [Examples](../examples/index.md)
- Reference: <xref:TestFramework.Core.Variables>

Next: [waiting for events](waiting-for-events.md).
