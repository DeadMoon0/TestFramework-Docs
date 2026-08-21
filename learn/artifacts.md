# Artifacts

An artifact is a real thing outside the test: a file, a blob, a database row. The framework tracks it
so that setup and teardown are deterministic instead of hopeful, and so that its state can be asserted
rather than assumed.

Most confusion about artifacts comes from mixing up four different paths. Decide which one you are on
before reaching for the API.

## 1. Declare, then populate

The resource does not exist yet, and the run should create it from data you supply. Declare the slot in
the timeline, fill it at run setup:

```csharp
private readonly Timeline _timeline = Timeline.Create()
    .SetupArtifact("declaredFile")
    .Build();

// ...
var run = await this._timeline.SetupRun(outputHelper)
    .AddFileArtifact("declaredFile", artifactPath, "declared then populated")
    .RunAsync();

run.EnsureRanToCompletion();
run.FileArtifact("declaredFile").Utf8Text().Should().Be("declared then populated");
```

This is the path that owns the resource, which means teardown removes it. That is the point.

## 2. Register, then assert

A step creates the resource during the run, and tracking starts once it exists. No prophecy required:

```csharp
Timeline.Create()
    .Trigger(LocalIOExt.Trigger.Cmd(Var.Ref<string>("cmdCreate"), Var.Ref<string>("cwd")))
    .RegisterArtifact("createdFile", LocalIOExt.Artifacts.FileRef(Var.Ref<string>("artifactPath")))
    .Build();
```

A reference is the resource's address. Without one you do not have tracking, you have a side effect and
an opinion about where it went.

## 3. Discover, then observe

The resource was created by something else entirely, and the run has to find it:

```csharp
Timeline.Create()
    .FindArtifacts("foundFile", new FileArtifactFolderFinder(Var.Ref<string>("folder")))
    .Build();

// ...
run.FileArtifact("foundFile_0").Should().Exist();
run.FileArtifact("foundFile_1").Should().Exist();
```

Discovered artifacts are numbered in the order found, which is why the names carry a suffix.

## 4. Capture a version

Artifacts change while the run proceeds, and "the file afterwards" is often not the interesting state.
Name the moment you care about:

```csharp
Timeline.Create()
    .Trigger(LocalIOExt.Trigger.Cmd(Var.Ref<string>("cmdAppend"), Var.Ref<string>("cwd")))
    .RegisterArtifact("newFile", LocalIOExt.Artifacts.FileRef(Var.Ref<string>("artifactPath")))
    .Trigger(LocalIOExt.Trigger.Cmd(Var.Ref<string>("cmdAppend"), Var.Ref<string>("cwd")))
    .CaptureArtifactVersion("newFile", "laterVersion")
    .Build();
```

The store then holds both, and each is reachable by name:

```csharp
run.ArtifactStore.GetFileArtifact("newFile").First.DataAsUtf8String;
run.ArtifactStore.GetFileArtifact("newFile")["laterVersion"].DataAsUtf8String;
```

`First` and `Last` cover the common cases; a named version pins an exact moment, which beats "the
earlier one, but not the very first earlier one".

## Teardown deletes - unless you say otherwise

At teardown the run walks every artifact that reached the `Setup` state and deletes it. `MarkReadonly()`
is what keeps one; a reference that cannot be deconstructed at all is also left in place, and logged.

That is the whole rule, and note what is missing from it: the verb. `SetupArtifact`,
`RegisterArtifact` and `FindArtifact` all put an artifact in the same store. None of them makes it
safe on its own.

| Path | Deleted at teardown? |
|---|---|
| `SetupArtifact` + `AddFileArtifact` / `AddArtifact` | yes - the run created it |
| `RegisterArtifact` | yes, unless you call `MarkReadonly()` |
| `FindArtifact` / `FindArtifacts` | yes, unless you call `MarkReadonly()` |

Deleting is the default everywhere, because a test that leaves its own data behind poisons the next
run. When you only meant to look at something, say so at the call site:

```csharp
timeline.FindArtifact("order", finder).MarkReadonly()
```

> [!WARNING]
> Discovery is not safe without it. **No shipped finder is read-only** - the LocalIO folder finder, the
> web SQL finder and all three Azure finders hand back references teardown can delete. Against a shared
> or live store, reach for `MarkReadonly()` rather than trusting the finder.

`MarkReadonly()` is the decision you can rely on, because nothing downstream can overrule it. A
reference type separately reports whether it *can* be deconstructed - `FileArtifactReference.Observed()`
still clears that - but that is the reference author's answer to a different question: can this be
deleted, not may it be.

Deleting data you merely looked at is not tidying up.

## Where to look next

- Run it: chapters 05 and 14 in the [Examples](../examples/index.md)
- Concept: [artifacts](../guide/concepts/artifacts.md)
- Reference: <xref:TestFramework.Core.Artifacts>

Next: [when things fail](when-things-fail.md).
