# TestFramework.Core

The timeline engine: steps, variables, artifacts, assertions, and the run that executes them. Every
other package in the ecosystem builds on this one, and a test that needs nothing external needs only
this one.

```bash
dotnet add package TestFramework.Core
```

## Quickstart

```csharp
public class SampleIntegrationTest
{
    private static readonly Timeline _timeline = Timeline.Create()
        .SetVariable("name", Var.Const("Alex"))
        .Transform("greeting", Var.Ref<string>("name"), name => $"Hello {name}")
        .AssertVariable(Var.Ref<string>("greeting"), greeting => greeting == "Hello Alex")
        .Build();

    [Fact]
    public async Task CanRunTimeline()
    {
        TimelineRun run = await _timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();

        using (run.AssertionScope())
        {
            run.Variable<string>("greeting").Should().Exist().And().Be("Hello Alex");
        }
    }
}
```

## The mental model

Four ideas, in the order you meet them:

- **[Timeline](../concepts/timeline.md)** - the plan, built once and frozen by `Build()`.
- **Run** - one execution of that plan, created by `SetupRun(...)` and started by `RunAsync()`.
  Runs are isolated from each other.
- **Variables** - the data channel between steps and between the build and run phases. `Var.Const(...)`
  for a value known while authoring, `Var.Ref<T>(...)` for one resolved during the run.
- **Artifacts** - external resources the run creates, tracks and cleans up, so setup and teardown are
  deterministic rather than hopeful.

## The three contract layers

The public surface is larger than what most test authors need, and that is deliberate rather than
accidental. Read it in this order:

1. **Consumer-first API** - `Timeline.Create()`, the fluent verbs (`SetVariable`, `Transform`,
   `Trigger`, `WaitForEvent`, `AssertVariable`, `Conditional`, `ForEach`), `Build()`, `SetupRun(...)`,
   `RunAsync()`, and `TimelineRun` with its assertion handles. If you are writing tests, this is the
   whole job.
2. **Extension API** - artifact describers and references, environment providers and requirements,
   event base types. This is for package authors, including the other TestFramework packages.
3. **Visible scaffolding** - the action interfaces the fluent builder is composed from. They are public
   because the chain is built out of them, not because they are a good place to start.

The API reference labels the most-visited namespaces with the layer they belong to, so a search result
tells you whether you have landed somewhere you should be. Coverage is not complete; an unlabelled
namespace means nobody has written the note yet, not that it is unimportant.

## The artifact lifecycle

Most confusion about artifacts comes from mixing four distinct paths. Decide which one your resource
is taking before reaching for the API:

| Path | Use when | Call |
|---|---|---|
| Declare | the run sets the resource up from external data before the main steps | `SetupArtifact("name")` |
| Register | a step creates the resource during the run and tracking starts afterwards | `RegisterArtifact("name", reference)` |
| Discover | the resource must be searched for once earlier work has finished | `FindArtifact(...)`, `FindArtifacts(...)` |
| Assert | the resource already exists and you are checking it | `TimelineRun` artifact handles |

Which path you took does not decide whether teardown deletes the resource: it deletes all of them by
default. Chain `MarkReadonly()` onto the declaring call for a resource the run only reads. That matters
most on the discover path, where it is easy to assume looking is read-only: see
[deleting is the default](../concepts/artifacts.md#deleting-is-the-default-and-markreadonly-is-the-opt-out).

## Troubleshooting

**The run passed but nothing was verified.** `RunAsync()` returns a finished run, failed or not.
`EnsureRanToCompletion()` is the call that fails the test.

**A frozen-timeline exception.** Something tried to author after `Build()`. Timelines are immutable by
design - build a second timeline instead.

**Nothing appears in the test output.** Pass the xunit `ITestOutputHelper` into `SetupRun(...)`;
without it the run executes silently.

## Going deeper

- [Example chapters](../../examples/index.md) - runnable, with their real output
- <xref:TestFramework.Core.Timelines> - the API reference
- [Core architecture](https://github.com/DeadMoon0/TestFramework-Core/blob/main/Documentation/CoreArchitecture.md) -
  the in-repo architecture notes, for readers who are already in the code
