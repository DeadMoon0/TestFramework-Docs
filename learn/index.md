# Learn

TestFramework describes an integration test as a **timeline**: a plan you build once and then execute
as many times as you like, each run isolated from every other.

That split is the whole idea, and it is why the API looks the way it does:

| Phase | What happens | What you write |
|---|---|---|
| Build | The plan is composed and frozen | `Timeline.Create()` ... `Build()` |
| Run | The plan executes with run-specific inputs | `SetupRun(...)`, `RunAsync()` |
| Result | The finished run is inspected | `EnsureRanToCompletion()`, `Variable<T>(...)` |

## The track

One goal per page, in order. Nothing here is optional and nothing is a matrix - when you need the full
surface, the [API reference](../api/index.md) has it.

1. [Install](install.md) - a test project that can run a timeline
2. [Your first timeline](first-timeline.md) - build one, run it, assert on it
3. [Reading a run](reading-a-run.md) - what a finished run tells you, and how to ask
4. [Passing data between steps](passing-data.md) - constants, run inputs, step outputs
5. [Waiting for events](waiting-for-events.md) - declared waits instead of polling loops
6. [Asserting properly](asserting.md) - named handles, batches, and assertion scopes
7. [Artifacts](artifacts.md) - the four lifecycle paths, and what decides whether teardown deletes
8. [When things fail](when-things-fail.md) - invalid plans, failed steps, retries, timeouts
9. [Your first real system](first-real-system.md) - a REST API and the database behind it
10. [Running it against Docker](into-docker.md) - the same timeline, container-backed
11. [Where to go from here](next-steps.md)

Each page ends with the [example chapter](../examples/index.md) that runs the same code, so you can
execute what you just read.

## Before you start

You need a .NET 8 or .NET 10 SDK and a test project. The track uses xunit because the framework's own
examples do, but nothing in the timeline model depends on it.
