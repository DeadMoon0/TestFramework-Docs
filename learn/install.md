# Install

TestFramework runs inside your existing test framework. It does not replace xunit; it gives xunit
something better to run than a pile of setup code.

## A project that can run a timeline

```bash
dotnet new xunit -n MyIntegrationTests
cd MyIntegrationTests
dotnet add package TestFramework.Core
```

That is the whole requirement. `TestFramework.Core` targets `net8.0` and `net10.0`, and it brings the
timeline engine, variables, artifacts and assertions with it.

## Add capability, not ceremony

Everything else in the ecosystem is additive. Install a package when a test needs the thing it does,
not before:

| You want to | Install |
|---|---|
| Run shell commands, watch files | `TestFramework.LocalIO` |
| Call REST APIs and the SQL behind them | `TestFramework.Web` |
| Talk to Azure components | `TestFramework.Azure` |
| Configure services and settings properly | `TestFramework.Config` |
| Trigger small inline actions | `TestFramework.Simple` |
| Run any of the above against Docker | `TestFramework.Container.Web`, `TestFramework.Container.Azure` |

None of them changes how a timeline is written - they add verbs to the same builder. They are not all
independent, though: `TestFramework.Azure` and `TestFramework.Web` bring `TestFramework.Config` with
them, and the two `Container.*` packages bring their transport plus the shared container plumbing. The
[compatibility page](../guide/reference/compatibility.md) has the real dependency graph.

## Check that it works

Before writing anything real, prove the plumbing with a timeline that does nothing:

```csharp
using TestFramework.Core.Timelines;
using Xunit;
using Xunit.Abstractions;

public class SmokeTest(ITestOutputHelper output)
{
    [Fact]
    public async Task FrameworkIsWiredUp()
    {
        Timeline timeline = Timeline.Create().Build();

        TimelineRun run = await timeline.SetupRun(output).RunAsync();

        run.EnsureRanToCompletion();
    }
}
```

```bash
dotnet test
```

Green, and the test output now contains a run report with an empty main stage. That report is the
thing you will read for the rest of your time with this framework, so it is worth having seen it once
while nothing can possibly have gone wrong.

Next: [your first timeline](first-timeline.md).
