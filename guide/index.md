# Guide

Reference prose, organised by topic rather than by reading order. If you are here to learn the framework
rather than to look something up, the [Learn](../learn/index.md) track is the better door.

## Concepts

How the model works, independent of any one package.

- [The timeline](concepts/timeline.md) - the plan, and why it is frozen
- [Runs, stages and steps](concepts/runs-stages-steps.md) - what executes, in what order, and why
- [Variables](concepts/variables.md) - constants, run inputs, step outputs, transforms
- [Artifacts](concepts/artifacts.md) - the four lifecycle paths, and what decides whether teardown deletes
- [Assertions](concepts/assertions.md) - handles, batches, scopes, in-timeline checks
- [Environments and providers](concepts/environments-and-providers.md) - what decides an identifier's meaning
- [Parallel execution](concepts/parallel-execution.md) - when the planner merges steps into a layer

## Packages

One page each. Every package is additive: install it when a test needs what it does.

| Package | Adds |
|---|---|
| [TestFramework.Core](packages/core.md) | the timeline engine everything else builds on |
| [TestFramework.Config](packages/config.md) | configuration and dependency injection setup |
| [TestFramework.Simple](packages/simple.md) | inline actions and messages, without a custom step class |
| [TestFramework.LocalIO](packages/localio.md) | shell commands, file events, file artifacts |
| [TestFramework.Web](packages/web.md) | REST APIs, SQL Server, stubbed dependencies |
| [TestFramework.Azure](packages/azure.md) | Function Apps, Logic Apps, Service Bus, Storage, Cosmos, SQL |
| [TestFramework.Container](packages/container.md) | the shared Docker building blocks |
| [TestFramework.Container.Web](packages/container-web.md) | the web stack, served from containers |
| [TestFramework.Container.Azure](packages/container-azure.md) | Azure emulators in Docker |

## How-to

The awkward parts, addressed directly.

- [Local to Docker to live](how-to/local-to-docker-to-live.md) - one timeline, three levels of realism
- [Configuration patterns](how-to/configuration-patterns.md) - shared bases, identifiers, secrets, disposal
- [Error handling](how-to/error-handling.md) - which category of wrong you are looking at
- [Debugging a run](how-to/debugging-a-run.md) - reading the report, and what to do when it is not enough

## Reference

- [Compatibility and versions](reference/compatibility.md) - frameworks, versions, dependency order
- [The wider ecosystem](ecosystem.md) - Showroom, DebugUI, and how the repositories relate

## Deeper architecture notes

Each package repository carries architecture documentation written for readers already in the code. This
site does not duplicate it:

- [TestFramework-Core: architecture](https://github.com/DeadMoon0/TestFramework-Core/blob/main/Documentation/CoreArchitecture.md)
  and [arc42](https://github.com/DeadMoon0/TestFramework-Core/blob/main/Documentation/Arc42.md)
- [TestFramework-Azure: arc42](https://github.com/DeadMoon0/TestFramework-Azure/blob/main/Documentation/Arc42.md)
- [TestFramework-Web: arc42](https://github.com/DeadMoon0/TestFramework-Web/blob/main/Documentation/Arc42.md)
- [TestFramework-LocalIO: arc42](https://github.com/DeadMoon0/TestFramework-LocalIO/blob/main/Documentation/Arc42.md)
- [TestFramework.Container.Azure: architecture](https://github.com/DeadMoon0/TestFramework-Container/blob/main/TestFramework.Container.Azure/Documentation/Architecture.md)
