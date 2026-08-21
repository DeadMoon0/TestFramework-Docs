# Assertions

The framework has its own fluent assertion layer, and using it rather than raw `Assert.*` buys three
things: the assertion is reported to the debugging surface, it participates in assertion scopes, and it
fails with framework exception types that carry the run context.

## Two different checks

```csharp
run.EnsureRanToCompletion();          // did the run finish?
run.Step("ping").Should().HaveCompleted();  // did this specific thing happen?
```

`EnsureRanToCompletion()` is not optional politeness. `RunAsync()` returns failed runs too, so a test
without this call can only fail on assertions you wrote - an exploded step passes unnoticed.

## Handles

Every assertion starts from a handle, and a handle is looked up by name:

| Handle | Asks about |
|---|---|
| `run.Step("name")` | one step's execution |
| `run.Steps("name")` | every instance of a label, e.g. inside a loop |
| `run.Variable<T>("name")` | a variable's existence and value |
| `run.FileArtifact("name")` | a file artifact's content |
| `run.ApiStatus("name")`, `run.ApiJson<T>("name")`, `run.ApiProbe("name")` | an HTTP step's result |
| `run.SqlScalar<T>("name")`, `run.SqlRow<T>("name")` | a SQL step's result |
| `run.StubCall("name")`, `run.StubUnmatchedCalls("name")` | what a stub received |

Names come from `.Name("...")` on the step, or from the artifact or variable identifier. Name the things
you will assert on; leave the rest anonymous.

## Chaining and projection

`And()` continues a chain. `Select(...)` projects a result before asserting, so you assert on the field
you mean rather than on a whole object:

```csharp
run.Step("ping").Should().HaveCompleted().And().HaveCompleted();

run.SqlRow<ShowroomOrder>("written").Select(order => order.Quantity).Should().Be(6);
run.ApiProbe("live").Select(probe => probe.Success).Should().Be(true);
```

## Existence is its own question

```csharp
run.Variable<int>("ExitCode").Should().Exist().And().Be(0);
```

A variable that was never produced and a variable with the wrong value are different defects. The API
keeps them separate so the failure message does too.

## Batch assertions

```csharp
run.Steps("greet").Should().AllHaveCompleted();
```

One assertion over every iteration of a loop label. It keeps working when the input list grows, which a
hand-written triple does not.

## Assertion scopes

By default the first failure throws and you learn one fact per run. Inside a scope, failures are
collected and reported together when the scope closes:

```csharp
using (run.AssertionScope())
{
    run.ApiStatus("create").Should().Be(HttpStatusCode.Created);
    run.SqlRow<ShowroomOrder>("written").Select(order => order.Quantity).Should().Be(6);
    run.StubUnmatchedCalls("audit").Should().HaveCount(0);
}
```

For integration tests this is a significant difference. A run may take thirty seconds and start a
database; getting three facts out of it instead of one is worth the two extra lines.

## In the timeline, not only after it

Assertions can also be steps, which puts the check at the point in the run where it is meaningful rather
than at the end:

```csharp
.AssertVariable(Var.Ref<string>("greeting"), greeting => greeting == "Hello Alex")
```

A failing in-timeline assertion fails its step, which means it appears in the run log in position. It
also stops the stage: once a layer contains a failed step, no later layer runs, so the rest of the
timeline does not execute against a state you already know is wrong. Steps sharing the failing step's
layer still run - they had already been started - and the cleanup stage runs either way.

## See also

- [Learn: asserting properly](../../learn/asserting.md)
- <xref:TestFramework.Core.Timelines.Assertions>
