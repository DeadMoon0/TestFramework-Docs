---
uid: TestFramework.Core.Steps.Options
---

**Consumer-first API, reached through modifiers.** `Name(...)`, `WithRetry(...)`, `WithTimeOut(...)` and
`DoNotParallelize()` set the options in here rather than you constructing them.

Worth knowing: `TimeOutOptions` defaults to 10 minutes, so every step already has a deadline - see
[when things fail](../learn/when-things-fail.md). `StepExecutionPhase` is what makes authored order and
executed order agree, described in [parallel execution](../guide/concepts/parallel-execution.md).
