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

The debug view is structured, so read it structurally rather than scanning for red:

1. **Stage plan** - does the run contain the steps you think it does? A step you expected and cannot find
   was probably never emitted, which is an authoring bug, not a runtime one.
2. **Variables** - what did the values actually resolve to? Most "the system is wrong" turns out to be
   "the input was not what I meant".
3. **Artifacts** - which resources were tracked, in what state, and under whose ownership.
4. **Dependency graph** - why the planner ordered things this way, and what shared a layer.
5. **Run log** - step by step, with attempt numbers. Read this last: it answers *what happened*, and the
   sections above answer *what was supposed to*.

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
