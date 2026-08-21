---
uid: TestFramework.Core.Timelines.Assertions
---

**Consumer-first API.** The `Should()` handles for steps, variables and artifacts, plus assertion scopes.
Prefer these over raw `Assert.*`: they are reported to the debugging surface, they participate in
`run.AssertionScope()`, and they fail with the framework's own exception types.

See [assertions](../guide/concepts/assertions.md).
