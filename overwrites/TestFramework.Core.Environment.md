---
uid: TestFramework.Core.Environment
---

**Extension API.** Environment components and requirements: the seam that decides what an identifier
actually points at, so one timeline can run against a fake, a container, or a deployed system.

Consumers meet this only as `SetEnv(...)` on the run builder. See
[environments and providers](../guide/concepts/environments-and-providers.md).
