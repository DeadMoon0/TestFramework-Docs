# The wider ecosystem

Two repositories in the TestFramework ecosystem are not NuGet packages. They matter anyway.

## Showroom - the runnable examples

[TestFramework-Showroom](https://github.com/DeadMoon0/TestFramework-Showroom) is the teaching surface: one
concept per chapter, each an executable test. The [Examples](../examples/index.md) section of this site is
generated from it, so what you read there is what runs.

It is organised in three lanes, in increasing order of what they need from your machine:

| Lane | Needs | Covers |
|---|---|---|
| Basic | nothing | the timeline model, variables, events, artifacts, assertions, retry, failure paths |
| Web | a Docker daemon, except the schema chapter | REST APIs, SQL Server, stubs, and all four together |
| Azure | a Docker daemon | Storage, Table, Cosmos, Service Bus, SQL, Function Apps |

Clone it and run a lane directly:

```bash
git clone https://github.com/DeadMoon0/TestFramework-Showroom.git
cd TestFramework-Showroom
dotnet test TestFramework.Showroom.Basic/TestFramework.Showroom.Basic.csproj -c Release
```

The Showroom deliberately restores only published package versions, so a fresh clone must work against the
public feed alone. That is a rule with teeth: it is how a lane once shipped references to versions that
existed only on one machine.

## DebugUI - inspecting a run as a tree

[TestFramework-DebugUI](https://github.com/DeadMoon0/TestFramework-DebugUI) is a desktop inspection surface
for timeline runs. Reach for it when a text report stops being enough:

- inspect a run as `Run → Stage → Layer → Step → Attempt`,
- review variables, artifacts, logs and assertions per node instead of parsing output,
- pause at breakpoints and continue deliberately,
- keep completed runs open while later runs execute.

**It is not a NuGet package**, and it is not published yet. It is a Windows desktop application, and its
distribution story is still open - so today it means building from the repository. If you only need to read
a run occasionally, the text report covers it; see [debugging a run](how-to/debugging-a-run.md).

## Common - the shared assets

[TestFramework-Common](https://github.com/DeadMoon0/TestFramework-Common) carries the package icon and the
licence, consumed as a git submodule by every package repository. Nothing a consumer interacts with, but it
explains why a clone of any framework repository needs `--recurse-submodules` before it can pack.

## How the repositories relate

Each package repository is independent, with its own CI and its own release tags - there is no umbrella
repository. That is why:

- a package can ship without dragging its repo-mates along,
- the dependency chain has a publish order that matters (see
  [compatibility](reference/compatibility.md)),
- and this documentation site is its own repository too, building a site out of what the others publish.
