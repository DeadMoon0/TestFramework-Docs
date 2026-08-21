# Local to Docker to live

One timeline, three levels of realism. This is the framework's central practical claim, and it is worth
knowing exactly how much of it is free and where the work actually is.

## The three stages

| Stage | What is real | Cost | Catches |
|---|---|---|---|
| Local | nothing external - in-process steps and fakes | milliseconds, runs anywhere | logic, wiring, contract violations |
| Docker | the API, the database, the stubbed dependencies | seconds, needs a daemon, still hermetic | serialisation, schema, what the app *sends* |
| Live | the deployed system | slow, shared, not repeatable on demand | configuration, permissions, network reality |

They are not three test suites. They are the same suite with a different environment.

## What stays identical

The timeline. Every one of these is unchanged across all three stages:

- the steps and their order,
- the variables and how they are supplied,
- the artifacts and who owns them,
- every assertion.

That holds because a timeline names **identifiers**, never addresses. `"orders"` is a logical name; what
serves it is somebody else's decision.

## What changes

One line:

```csharp
// Local - configuration supplies the address
TimelineRun run = await timeline.SetupRun(config).RunAsync();

// Docker - an environment starts what is needed and publishes the addresses
TimelineRun run = await timeline.SetupRun(config)
    .SetEnv(DockerWebEnvironment.For<SampleSqlDefinition>())
    .RunAsync();
```

Plus, for the Docker stage, the definition classes describing the infrastructure - which is real work, but
work you do once per shape rather than once per test.

## Going up the ladder

**Local → Docker.** Add the container package, write the definition classes, add `SetEnv(...)`. Watch for
two things: your assertions may have been passing against a fake that was more forgiving than a real
database, and schema drift becomes visible for the first time.

**Docker → live.** Remove `SetEnv(...)`, point the configuration at the deployed environment. Two things
change character:

- **Teardown deletes by default, and `FindArtifact(...)` is not an exception.** Against a shared system
  that deletes real data, and no shipped finder is read-only. Before pointing a suite at live data, walk
  every artifact it declares and add `MarkReadonly()` to the ones it only reads. See
  [deleting is the default](../concepts/artifacts.md#deleting-is-the-default-and-markreadonly-is-the-opt-out).
- **Stubs are gone.** Assertions on `StubCall(...)` and `StubUnmatchedCalls(...)` have no meaning against
  a live dependency. Those checks belong to the Docker stage, and that is fine - it is where they are
  strongest.

## Which stage should a test live at?

Prefer the lowest stage that can actually fail for the reason you care about:

- Does the reasoning of my code work? **Local.**
- Does the system write, send and respond correctly together? **Docker.**
- Does the deployment work? **Live** - and keep that set small, because a slow shared suite that nobody
  trusts is worse than a small one that everybody does.

## Going deeper

- [Learn: running it against Docker](../../learn/into-docker.md)
- [Environments and providers](../concepts/environments-and-providers.md)
- [The Showroom's own notes on this progression](https://github.com/DeadMoon0/TestFramework-Showroom/blob/main/Documentation/LocalToDockerToLive.md)
