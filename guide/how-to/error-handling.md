# Error handling

The framework distinguishes several kinds of wrong, and reports them differently on purpose. Reading the
category correctly is most of the diagnosis.

## The categories

| Category | When | Surfaces as |
|---|---|---|
| Framework misuse | before execution - the plan itself is invalid | throws from run setup, e.g. `IOContractViolationException` |
| Step failure | during the run | recorded on the run; raised by `EnsureRanToCompletion()` as `TimelineRunFailedException` |
| Domain result | during the run - the system answered, just not with success | a step **result** you assert on |
| Transport failure | during the run - the system could not be reached | an exception from that package, e.g. `ApiRequestFailedException` |

The third row is the one that surprises people, and it is deliberate: a 404 is an answer.

## Framework errors explain themselves

A misuse exception formats itself with a `[FRAMEWORK ERROR]` header, a `Recovery:` section, and the
specific name involved:

```csharp
IOContractViolationException exception = await Assert.ThrowsAsync<IOContractViolationException>(() =>
    timeline.SetupRun(outputHelper).RunAsync());

string formatted = exception.ToString();
// contains "[FRAMEWORK ERROR]", "Recovery:", and "cmdCommand"
```

When you see that header, the message is meant to be read rather than searched for online. It names what
was missing and what to do about it.

## Reading a failed run

```csharp
TimelineRunFailedException exception =
    Assert.Throws<TimelineRunFailedException>(() => run.EnsureRanToCompletion());

Assert.Single(exception.FailedSteps);
Assert.Contains("File Exists Event", exception.FailedSteps[0].StepName, StringComparison.OrdinalIgnoreCase);
Assert.IsType<TimeoutException>(exception.FailedSteps[0].StepException);
```

`FailedSteps` gives you the step name and the underlying exception per failure - not one aggregate string.

**Assert on exception types, not on wording.** When a step's timeout modifier and an event's own timeout
race each other, both phrase the failure differently while both being a `TimeoutException`. A substring
match on one of the two sentences is a coin flip; the type is stable.

## Discovery failures are their own thing

`FindArtifactsAs([...], finder)` names its expected results, so a count mismatch is a failure with a
specific meaning: the run shape was fine, but the environment held a different number of things than the
scenario allows. The message names `FindArtifactsAs` and the expected count.

Treat it as an environment assertion rather than a bug in the finder - usually a previous run left
something behind, or a seed did less than you thought.

## Making failure paths testable

A test that asserts on a failure is a first-class scenario, not a hack. The pattern:

1. build the timeline that will fail,
2. run it and capture the run,
3. assert that `EnsureRanToCompletion()` throws the expected type,
4. assert on `FailedSteps` for the specifics.

This is how you prove that your system fails *correctly*, which for integration tests is at least as
valuable as proving it succeeds.

## Habits that prevent most of it

- **Bound every wait** with `WithTimeOut(...)`. The 10-minute default keeps a suite from hanging, but
  it is a backstop; a wait that will never succeed should say so in seconds.
- **Use `IsLive(...)` in front of a call against something that may still be starting**, so the startup
  error lands on the step designed to absorb it.
- **Retry only genuinely transient work**, with `WithRetry(...)`. Retrying a deterministic failure just
  makes the log longer.
- **Always call `EnsureRanToCompletion()`.** Without it, a failed step is invisible.

## Going deeper

- [Learn: when things fail](../../learn/when-things-fail.md)
- <xref:TestFramework.Core.Exceptions>, <xref:TestFramework.Core.Steps.Options>
- [Web error handling notes](https://github.com/DeadMoon0/TestFramework-Web/blob/main/Documentation/ERROR-HANDLING-WEB.md)
