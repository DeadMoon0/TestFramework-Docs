# Where to go from here

You have the whole model now: a frozen plan, isolated runs, variables between steps, declared waits,
tracked artifacts, assertions with names, and an environment that decides what is real.

## Pick your next move

**"I need to do something specific."** The [Guide](../guide/index.md) is organised by topic - concepts,
one page per package, and how-tos for the awkward parts.

**"I want to see it working."** The [Examples](../examples/index.md) are runnable chapters with the
output they actually produced. Each one has a copy-pasteable command for the single test.

**"I need a signature."** The [API reference](../api/index.md) covers every public type in all nine
packages, with a link to the source of each one.

## The packages you have not met

You only installed what the track needed. The rest follow the same shape:

| Package | Adds |
|---|---|
| [TestFramework.Azure](../guide/packages/azure.md) | Function Apps, Logic Apps, Service Bus, Storage, Cosmos, SQL |
| [TestFramework.LocalIO](../guide/packages/localio.md) | shell commands, file artifacts, file events |
| [TestFramework.Config](../guide/packages/config.md) | configuration and dependency injection setup |
| [TestFramework.Simple](../guide/packages/simple.md) | inline actions and messages, without a custom step class |
| [TestFramework.Container.Azure](../guide/packages/container-azure.md) | Azure emulators in Docker |

## When the log is not enough

A run prints itself, but reading a large run as text has limits. The
[DebugUI](../guide/ecosystem.md) inspects a run as a tree - `Run → Stage → Layer → Step → Attempt` -
with variables, artifacts, logs and assertions per node, and breakpoints you can continue from.

## Extending the framework

If you are about to write a `Step<T>` of your own, you have crossed from consumer into extension author.
Two things to read in that order:

1. [Runs, stages and steps](../guide/concepts/runs-stages-steps.md) - the contract a step implements.
2. Each package repository's own architecture notes, listed at the bottom of the [Guide](../guide/index.md).
   They are written for readers already in the code, which at that point is you.
