# Reading a run

A finished run is not a boolean. It is a structured record of what executed, in what order, with what
data - and the framework will print the whole thing for you if you let it.

## Let the run talk

Pass the xunit output helper into `SetupRun(...)` and the run reports itself:

```csharp
public class DebugOutput(ITestOutputHelper outputHelper)
{
    private readonly Timeline _timeline = Timeline.Create()
        .Trigger(SimpleExt.Trigger.Message("Hello from Test"))
        .Build();

    [Fact]
    public async Task Run()
    {
        var run = await this._timeline.SetupRun(outputHelper).RunAsync();
        run.EnsureRanToCompletion();
    }
}
```

Leave the helper out and everything still works - silently. That silence is the single most expensive
default in integration testing, which is why every example in this documentation passes the helper.

## What the report contains

The run prints a debug view rather than a log line per step. Its sections answer the questions you
actually ask when something is wrong:

| Section | Answers |
|---|---|
| Variables | what data existed, and what it resolved to |
| Artifacts | which external resources were tracked, and in what state |
| Stage Plan | which stages exist and how many steps each holds |
| Dependency graph | why the planner ordered things the way it did |
| Run log | what happened, step by step, with attempt numbers and timings |

You can see a real one on any [example chapter](../examples/index.md) - open the **Output** panel under
the code. Those panels are captured from actual runs, not written by hand.

## Asking the run direct questions

Reading is one thing; asserting is another. A completed run exposes its steps and variables by name,
so a test can interrogate specifics:

```csharp
run.Step("hello").Should().HaveCompleted();
run.Variable<int>("ExitCode").Should().Exist().And().Be(0);
```

Both of those require the thing to have a name, which is what `.Name("hello")` on a step is for. Name
the steps you intend to assert on; leave the rest anonymous.

## The one call that decides the test

```csharp
run.EnsureRanToCompletion();
```

`RunAsync()` returns a finished run whether it succeeded or failed, because a failure is often the
thing under test. `EnsureRanToCompletion()` is what converts a failed run into a failed test. A test
that never calls it can only fail on an assertion you wrote yourself - which means an exploded step
goes unnoticed.

Next: [passing data between steps](passing-data.md).
