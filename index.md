---
_layout: landing
title: Integration testing on a timeline
---

# TestFramework

A timeline-based framework for integration-style tests. You describe a workflow once, execute it with
run-specific inputs, and assert against an immutable result.

```csharp
Timeline timeline = Timeline.Create()
    .SetVariable("name", Var.Const("Alex"))
    .Transform("greeting", Var.Ref<string>("name"), name => $"Hello {name}")
    .AssertVariable(Var.Ref<string>("greeting"), greeting => greeting == "Hello Alex")
    .Build();

TimelineRun run = await timeline.SetupRun(serviceProvider).RunAsync();
run.EnsureRanToCompletion();
```

## Where to go

| | |
|---|---|
| **New here** | [Learn](learn/index.md) - one goal per page, in order |
| **Looking something up** | [Guide](guide/index.md) - concepts, and a page per package |
| **Want to run something** | [Examples](examples/index.md) - chapters you can execute locally |
| **Need a signature** | [API reference](api/index.md) - every public type |

## Install

```bash
dotnet add package TestFramework.Core
```

Everything else is optional and additive: `TestFramework.Azure` for cloud components,
`TestFramework.Web` for HTTP and SQL, `TestFramework.LocalIO` for shell and files,
`TestFramework.Container.*` to run any of it against Docker.

## Documented versions

This site documents these versions, and only these. The API reference is reflected out of the
packages themselves, so nothing here is API you cannot install.

[!INCLUDE [versions](guide/includes/versions.md)]
