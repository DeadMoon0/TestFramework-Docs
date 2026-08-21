# Artifacts

An artifact is a resource that exists outside the test: a file, a blob, a table row, a queue entry. The
framework tracks it so that its state can be asserted, and so that teardown is deterministic rather than
hopeful.

## Four paths, one decision

Almost all confusion about artifacts comes from mixing these up. Decide which one applies before
choosing an API.

| Path | The resource | Call |
|---|---|---|
| Declare | does not exist; the run creates it from data you supply | `SetupArtifact("name")` + `AddFileArtifact(...)` / `AddArtifact(...)` |
| Register | is created by a step during the run | `RegisterArtifact("name", reference)` |
| Discover | already exists, created by something else | `FindArtifact(...)`, `FindArtifacts(...)`, `FindArtifactsAs(...)` |
| Capture | exists and is changing; you want a specific moment | `CaptureArtifactVersion("name", "label")` |

## Deleting is the default, and `MarkReadonly()` is the opt-out

**Every artifact is deleted at teardown unless you say otherwise.** That is deliberate: a test that
leaves its own data behind poisons the next run. The verb you used to declare the artifact does not
change it - `RegisterArtifact` and `FindArtifact` are treated alike.

The one way to opt out is `MarkReadonly()`, chained onto the call that declares the artifact:

```csharp
timeline.FindArtifact("order", finder).MarkReadonly()
```

That is the test author's decision, and nothing can overrule it - not the finder, not the reference
type, not anything a package does while resolving. `MarkReadonly()` is offered only on the verbs that
produce an artifact (`RegisterArtifact`, `FindArtifact`, `FindArtifacts`, `FindArtifactsAs`); chaining
it anywhere else does not compile.

It applies per step, to every artifact that step produces, so `FindArtifacts(...).MarkReadonly()`
protects the whole result set. And it means what it says: an explicit `RemoveArtifact(...)` on a
readonly artifact fails the run rather than quietly deleting or quietly skipping.

## Ownership is also a property of the reference

Underneath that choice, a reference reports whether it *can* be deconstructed at all. The two answer
different questions - `CanDeconstruct` is "is there enough information to delete this?", while
`MarkReadonly()` is "may it be deleted?" - and teardown needs a yes from both:

Every shipped reference can, bar one conditional case: each is handed the key, path or coordinates it
would need, and none of them carries a policy opt-out of its own any more.

| Reference | Deconstructable |
|---|---|
| `FileArtifactReference(path)` | yes |
| `SqlRowArtifactReference` (web) | yes - a key value is required |
| `SqlRowArtifactReference` (azure) | yes - a primary key is required |
| `TableStorageEntityArtifactReference` | yes - table, partition key and row key are all required |
| `CosmosDbItemArtifactReference` | only once it has both a partition key and an id |

> [!WARNING]
> So **no shipped finder is read-only**. LocalIO's folder finder, the web package's SQL finder and all
> three in `TestFramework.Azure` hand back references teardown can delete. A file, SQL row, Cosmos item
> or table entity found by a timeline **is deleted** when the run ends unless you call `MarkReadonly()`.
> That is the same default a registered artifact gets, and it is why the opt-out lives where the author
> is rather than inside a finder.

## References: the address, not the value

`RegisterArtifact(...)` takes a reference, and each package provides its own:

```csharp
LocalIOExt.Artifacts.FileRef(Var.Ref<string>("artifactPath"))
WebExt.Artifact.Sql.Row<Order>("main", Var.Const("1"))
```

A reference says where the resource is. Without one there is nothing to track - only a side effect and
an assumption about where it landed.

## Finders: discovery with a predicate

A finder locates resources by criteria rather than by name:

```csharp
.FindArtifacts("foundFile", new FileArtifactFolderFinder(Var.Ref<string>("folder")))

.FindArtifact("written", WebExt.ArtifactFinder.Sql.Where<ShowroomOrder>("orders-db", "Name = @name")
    .WithParameter("name", Var.Const("Complete Order")))
```

Results are named in the order found - `foundFile_0`, `foundFile_1` - so a discovery of unknown size
still produces stable handles.

`FindArtifactsAs([...], finder)` is the strict variant: it names the results explicitly, and therefore
fails when the count does not match. That failure is a genuine signal - the environment held a different
number of things than the scenario allows - and it is reported as a run failure naming
`FindArtifactsAs` and the expected count.

## Versions

An artifact is a series of states, not one value. The store keeps them:

```csharp
run.ArtifactStore.GetFileArtifact("newFile").First.DataAsUtf8String;
run.ArtifactStore.GetFileArtifact("newFile").Last.DataAsUtf8String;
run.ArtifactStore.GetFileArtifact("newFile")["laterVersion"].DataAsUtf8String;
```

`CaptureArtifactVersion(...)` is how the named entry gets there. Use it when the interesting state is
mid-run - "after the first append, before the second" - which no amount of `Last` will give you.

## Asserting on artifacts

```csharp
run.FileArtifact("newFile").Utf8Text().Should().Be("Hello from the new Artifact\r\n");
run.SqlRow<ShowroomOrder>("written").Select(order => order.Quantity).Should().Be(6);
```

Note the line ending in the first example. A file artifact's text is exactly what was written, including
the platform's newline - which is the sort of detail that makes an assertion pass on one machine and
fail on another. Assert what the producer actually writes.

## See also

- [Learn: artifacts](../../learn/artifacts.md)
- <xref:TestFramework.Core.Artifacts>
