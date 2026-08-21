---
uid: TestFramework.Core.Artifacts
---

**Consumer-first for the store, extension API for the rest.** Reading `ArtifactStore` and the artifact
instances is ordinary test code; artifact describers, references and data types are what a package author
implements to add a new kind of resource.

Teardown deletes every artifact it set up. The reference decides whether it *can* be deconstructed;
whether it *may* be is the timeline's call, through `MarkReadonly()`. See
[deleting is the default](../guide/concepts/artifacts.md#deleting-is-the-default-and-markreadonly-is-the-opt-out) before pointing a timeline at data you did
not create.
