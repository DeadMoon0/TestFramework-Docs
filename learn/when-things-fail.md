# When things fail

Integration tests fail for three quite different reasons, and the framework deliberately reports them
differently. Telling them apart is most of the diagnostic work.

## Before anything runs: the plan is invalid

Steps declare what they need and what they produce. The run validates that plan **before** touching the
outside world, so a missing input fails without side effects:

```csharp
Timeline timeline = Timeline.Create()
    .Trigger(LocalIOExt.Trigger.Cmd(Var.Ref<string>("cmdCommand")))
    .Build();

// No AddVariable("cmdCommand", ...) - this throws from SetupRun/RunAsync, not from the command.
await Assert.ThrowsAsync<IOContractViolationException>(() =>
    timeline.SetupRun(outputHelper).RunAsync());
```

These are the cheapest failures you can have: nothing was started, so nothing needs cleaning up. The
exception formats itself with a `[FRAMEWORK ERROR]` header, a `Recovery:` section and the name of the
variable that was missing - so the message tells you what to do, not just what went wrong.

## During the run: a step failed

A failed step does not throw out of `RunAsync()`. The run finishes and records the failure, and
`EnsureRanToCompletion()` is what raises it:

```csharp
TimelineRunFailedException exception =
    Assert.Throws<TimelineRunFailedException>(() => run.EnsureRanToCompletion());

Assert.Single(exception.FailedSteps);
Assert.IsType<TimeoutException>(exception.FailedSteps[0].StepException);
```

`FailedSteps` carries the step name and the underlying exception, which is what you assert against.
Assert on the **type**, not the wording: when a step timeout and an event timeout race each other, both
phrase the message differently while both being a `TimeoutException`.

## Transient failure: let the step retry

Retry is a property of the step contract rather than a favour granted by one transport, so it applies
the same way everywhere:

```csharp
Timeline.Create()
    .Trigger(new EventuallySuccessfulStep(probe))
        .Name("transient")
        .WithRetry(3, CalcDelays.Fixed(TimeSpan.Zero))
    .Build();
```

The run log records each attempt separately, so a test that passed on attempt 2 does not look like a
test that passed cleanly. `CalcDelays` also offers backoff shapes for the cases where hammering
immediately is the wrong move.

## Bound a wait to something shorter than ten minutes

Every step already has a deadline. `TimeOutOptions` defaults to **10 minutes**, and the runner enforces
it by cancelling the step's own cancellation token - so a wait for something that never arrives fails
rather than hanging the suite forever.

That default is a backstop, not a setting to rely on. Ten minutes of a test doing nothing is ten
minutes you spend before learning anything, so give a wait the deadline it actually deserves:

```csharp
.WaitForEvent(LocalIOExt.Events.FileExists(Var.Ref<string>("missingPath")))
    .WithTimeOut(TimeSpan.FromMilliseconds(150))
    .Name("wait-for-never")
```

Two things follow from the deadline being enforced by cancellation:

- **A step is told to stop, not abandoned.** The token it received is cancelled, so a well-behaved step
  can stop its own work rather than continuing invisibly behind the run.
- **The step cannot narrate its own timeout.** The runner stops awaiting at the deadline, so a step
  that wants to explain a timeout in its own words has to finish just inside its own deadline rather
  than waiting to be cut off.

## Status codes are results, not failures

For HTTP work this catches people out, so it is worth stating flatly: a non-2xx response is returned to
the timeline and asserted on. Only a transport problem - connection refused, DNS failure, timeout -
raises.

```csharp
run.ApiStatus("missing").Should().Be(HttpStatusCode.NotFound);
```

A 404 is an answer. An unopened socket is not.

## Where to look next

- Run it: chapters 11 and 15 in the [Examples](../examples/index.md)
- How-to: [error handling](../guide/how-to/error-handling.md)
- Reference: <xref:TestFramework.Core.Exceptions>, <xref:TestFramework.Core.Steps.Options>

Next: [your first real system](first-real-system.md).
