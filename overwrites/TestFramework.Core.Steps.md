---
uid: TestFramework.Core.Steps
---

**Extension API.** You are here to write a `Step<T>` of your own. Four members carry the weight:
`Execute(...)`, `Clone()`, `GetInstance()` and `DeclareIO(...)` - cloning is what lets one frozen plan
produce independent instances per run, and the IO declaration is what lets the planner reason about the
step at all.

Test authors need none of this; the fluent builder verbs cover the consumer path.
