# TestFramework.LocalIO

Local-machine capability: running commands, watching files, and tracking files as artifacts. Public entry
points are `LocalIOExt.Trigger`, `LocalIOExt.Events` and `LocalIOExt.Artifacts`.

```bash
dotnet add package TestFramework.LocalIO
```

## Quickstart

```csharp
Timeline timeline = Timeline.Create()
    .UseRunDirectory()
    .Trigger(LocalIOExt.Trigger.Cmd(Var.Const("echo hello > out.txt")))
    .WaitForEvent(LocalIOExt.Events.FileExists(Var.Const("out.txt")))
        .WithTimeOut(TimeSpan.FromSeconds(10))
    .RegisterArtifact("outFile", LocalIOExt.Artifacts.FileRef(Var.Const("out.txt")))
    .Build();

TimelineRun run = await timeline.SetupRun().RunAsync();

run.EnsureRanToCompletion();
string content = run.ArtifactStore.GetFileArtifact("outFile").Last.DataAsUtf8String;
```

## Start with UseRunDirectory()

`UseRunDirectory()` creates `tf-localio-<guid>` under the system temp directory, publishes it as the run
directory, and removes it during cleanup. Every relative LocalIO path then resolves inside it - command
working directory, `FileExists(...)` target, artifact reference - so concurrent runs cannot read,
overwrite or delete each other's files.

Pass a root when the directory must live somewhere specific: `UseRunDirectory(Var.Const(myRoot))`.

Without it, relative paths resolve against the process-wide current directory at run time. That is a
documented legacy fallback, not a recommendation - it is also the single most common reason a LocalIO test
passes alone and fails in a parallel suite.

For one command that needs a different directory than the rest of the run, use the two-argument overload:
`LocalIOExt.Trigger.Cmd(command, workingDirectory)`.

## Scheduling comes for free

Local steps declare phases that match what they do: command triggers act in `Act`, file polling observes
in `Observe`, artifact registration materialises in `Materialize`. So the canonical
`Trigger → WaitForEvent → RegisterArtifact` sequence executes in authored order with no explicit
dependencies. Keep `DoNotParallelize()` for the rarer case where a step must be an explicit barrier
within its phase.

## Reading and seeding files

```csharp
TimelineRun run = await timeline.SetupRun()
    .AddFileArtifact("inputFile", inputPath, "hello world")
    .RunAsync();

string content = run.ArtifactStore.GetFileArtifact("inputFile").Last.DataAsUtf8String;
```

A seeded artifact is owned by the test, so cleanup removes it. See [artifacts](../concepts/artifacts.md)
for the ownership rules.

## Writing portable commands

Shell commands are the least portable thing in a test suite, and the Showroom keeps the platform
differences in one helper per chapter for exactly that reason. Three specifics worth stealing:

- **Join with `&&`, not `&`.** In `cmd` a single `&` is a separator; to a Unix shell it means "run this in
  the background". `&&` means "then" in both.
- **Do not use Windows `timeout` for delays.** It refuses to run when the console is redirected - which is
  every test host. `ping -n {seconds + 1} 127.0.0.1 >nul` survives redirection.
- **Watch the space before `>>`.** `echo text>> file` writes exactly `text`; `echo text >> file` writes the
  trailing space too, and then an exact-match assertion fails on one platform and passes on the other.

## Troubleshooting

**A `FileExists` wait timed out.** The event reports the resolved path it watched, not the template. Read
that path: it is almost always a working-directory mismatch, which `UseRunDirectory()` prevents.

**An assertion on file content fails by one character.** Line endings. A file artifact's text is exactly
what was written, `\r\n` included.

## Going deeper

- Chapters 05, 06 and 14 in the [Examples](../../examples/index.md)
- <xref:TestFramework.LocalIO>
- [Package guide](https://github.com/DeadMoon0/TestFramework-LocalIO/blob/main/TestFramework.LocalIO/README.md)
  and [arc42 notes](https://github.com/DeadMoon0/TestFramework-LocalIO/blob/main/Documentation/Arc42.md)
