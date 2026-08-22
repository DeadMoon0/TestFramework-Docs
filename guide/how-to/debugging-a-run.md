# Debugging a run

A run reports itself in full. Most debugging is therefore reading, not instrumenting - provided you gave
the run somewhere to report to.

## First: attach the output helper

```csharp
TimelineRun run = await timeline.SetupRun(outputHelper).RunAsync();
```

Without it the run executes in silence, and every question below becomes guesswork. This is the single
highest-value habit in this documentation.

## Read the report in this order

The debug view is structured, and its own order is the right reading order - the plan before the
execution. Read it structurally rather than scanning for red:

1. **Variables** - what did the values actually resolve to? Most "the system is wrong" turns out to be
   "the input was not what I meant".
2. **Stage plan** - does the run contain the steps you think it does, in the phases you expected? A step
   you expected and cannot find was probably never emitted, which is an authoring bug, not a runtime one.
3. **Dependency graph** - why the planner ordered things this way.
4. **Then one block per stage**: a header carrying `steps | layers | peak parallel`, a flow trace, and one
   box per step with its phase, its layer, its declared inputs and observed outputs, its log per attempt,
   and its final state. This answers *what happened*; everything above answers *what was supposed to*.

Artifacts have no section of their own - they appear where they are used. A step's outputs list each
artifact with its state (`Setup`, `Cleaned`), and the cleanup stage logs what it did with every one of
them, including the ones it deliberately left alone and why.

You can see a real report on any [example chapter](../../examples/index.md) - open the **Output** panel.

## Common readings

**A step ran twice.** Look at the attempt numbers: a retry modifier did its job, and the first attempt's
failure is recorded above.

**Log lines appear in a surprising order.** Check the layer. Steps sharing a mergeable layer run
concurrently, so their output interleaves - see [parallel execution](../concepts/parallel-execution.md).

**A wait timed out on a path you did not expect.** Events report the *resolved* target, not the template.
For LocalIO that usually means the working directory - `UseRunDirectory()` removes the whole class of
problem.

**Everything passed but nothing was verified.** Search your test for `EnsureRanToCompletion()`. If it is
not there, the run's own failures were never raised.

## Narrow the run, do not add print statements

Two techniques beat instrumenting:

- **`Name(...)` the step and assert on it.** `run.Step("create").Should().HaveCompleted()` turns a vague
  failure into a specific one.
- **`SimpleExt.Trigger.Message(...)`** writes through the run logger, so a marker appears in the report *in
  position* rather than in console output with no context.

## When text is not enough

For large runs, the [DebugUI](../ecosystem.md) inspects a run as a tree -
`Run → Stage → Layer → Step → Attempt` - with variables, artifacts, logs and assertions per node, and
breakpoints you can pause at and continue from deliberately. It is the same data the text report shows,
navigable instead of scrolled.

It is a desktop companion tool rather than a package, and it is not published yet - see
[the ecosystem page](../ecosystem.md) for what that means today.

## Going deeper

- [Learn: reading a run](../../learn/reading-a-run.md)
- [Error handling](error-handling.md) - which category of wrong you are looking at
- [The run debugger flow](https://github.com/DeadMoon0/TestFramework-Core/blob/main/Documentation/RunDebuggerFlow.md)
