# Asserting properly

`EnsureRanToCompletion()` proves the run finished. It does not prove the run did the right thing. That
is what the assertion layer is for.

## Name what you intend to assert on

An assertion needs a handle, and a handle needs a name:

```csharp
private readonly Timeline _timeline = Timeline.Create()
    .Trigger(SimpleExt.Trigger.Message("Is anyone out there?"))
        .Name("ping")
    .Build();

// ...
run.Step("ping").Should().HaveCompleted();
```

Name the steps you will assert on and leave the rest anonymous. Names are stable across refactoring in
a way that positions are not - a test that reasons about "the third step" is a test that breaks when
someone inserts a second one.

## Three handles, three questions

```csharp
run.Step("ping").Should().HaveCompleted();                  // did this step run?
run.Variable<int>("ExitCode").Should().Exist().And().Be(0);  // what data came out?
run.FileArtifact("newFile").Utf8Text().Should().Be("...");    // what is the resource's state?
```

They chain, so one handle can answer several questions:

```csharp
run.Step("ping").Should()
    .HaveCompleted()
    .And().HaveCompleted();
```

## Asserting a loop as a batch

When a label appears inside a `ForEach`, one label covers many instances - and there is a plural handle
for exactly that:

```csharp
Timeline.Create()
    .ForEach(["Alice", "Bob", "Charlie"], "item", loop =>
    {
        loop.Trigger(SimpleExt.Trigger.Message(Var.Ref<string>("item"))).Name("greet");
    })
    .Build();

// ...
run.Steps("greet").Should().AllHaveCompleted();
```

One assertion over the whole batch beats three hand-written ones, and it keeps working when the list
grows.

## Collect the whole failure, not the first one

By default the first failed assertion throws, and you learn one fact per test run. An assertion scope
collects them and reports them together:

```csharp
using (run.AssertionScope())
{
    run.Step("check").Should().HaveCompleted();
    run.Variable<int>("ExitCode").Should().Exist().And().Be(0);
}
```

When the scope closes, every collected failure arrives as one report. For an integration test - where a
single run may be expensive and slow - that difference matters: you get the whole picture from one
execution instead of peeling the onion one failure at a time.

## Use the framework's assertions, not raw ones

`Assert.Equal` works, and the Showroom uses it where it is proving a framework-internal detail. For
ordinary test code prefer `Should()`, because those assertions are reported to the debugging surface,
they participate in `run.AssertionScope()`, and they fail with the framework's own exception types
carrying the run context.

## Where to look next

- Run it: chapters 08 and 09 in the [Examples](../examples/index.md)
- Concept: [assertions](../guide/concepts/assertions.md)
- Reference: <xref:TestFramework.Core.Timelines.Assertions>

Next: [artifacts](artifacts.md).
