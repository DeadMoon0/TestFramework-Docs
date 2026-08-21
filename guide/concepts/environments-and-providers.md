# Environments and providers

An environment decides what a timeline's identifiers actually point at. It is the seam that lets one
timeline run against an in-process fake, a container, or a deployed system without being rewritten.

## The shape

```csharp
TimelineRun run = await timeline.SetupRun(serviceProvider, outputHelper)
    .SetEnv(DockerWebEnvironment.For<SampleSqlDefinition>())
    .RunAsync();
```

Everything above `SetEnv(...)` is the plan. `SetEnv(...)` is the only line that knows about Docker, and
removing it does not change a single step.

## Requirements and providers

Steps do not ask for infrastructure directly; they **declare a requirement**. A `TestFramework.Web` HTTP
step declares the `web.restapi` kind against an identifier, and something must satisfy it.

That indirection is why the same step can travel:

| Provider | Satisfies the requirement with |
|---|---|
| configuration only | an address from a settings file |
| a container environment | a container it starts, publishing the mapped address |
| a persistent environment | a component that was already running before this run began |

The step is unaware of which happened. What it sees is a resolved endpoint.

## Senders: how a request travels

One level below that sits the transport seam. For the web package it is `IHttpSender`, but you never
resolve one yourself: a step calls
`serviceProvider.GetWebComponentFactory().CreateSender(identifier, config)`, and the factory decides what
a request travels over - the built-in one hands back an `HttpClient`-backed sender. Replacing that factory
in the provider is therefore how a host changes transport for every step at once, which is what lets an
in-process host and a container-hosted API look identical to the same step.

## What actually happens when a run has an environment

Core contributes one `Prepare` step, "Create Environment Components", and one cleanup step. Between them:

1. **Resolution.** `ResolveComponents(...)` is handed the artifacts already in the store plus the
   requirements the steps declared, and returns the components this run needs. Nothing starts for a
   component nothing asked for.
2. **Creation, in dependency order.** By default components are created one at a time in graph order -
   `SupportsParallelComponentCreation` is `false` on the base provider, and `DockerWebEnvironment` leaves
   it that way. A provider can opt in, and `DockerAzureEnvironment` does: creation then runs in dependency
   *layers*, with everything inside one layer created concurrently.
3. **Readiness, per component.** Each component waits for its own service to answer as part of being
   created, rather than a central step guessing at it: the API component waits on the configured health
   path (or `/` when there is none), the SQL components wait until a connection actually opens, and the
   Function App component waits on `admin/host/status`.
4. **Publishing.** Components write their mapped addresses into the same configuration store a settings
   file would have filled, which is why the timeline's identifier keeps working unchanged.
5. **Teardown, in reverse.** The cleanup step walks the creation order backwards, so nothing is torn down
   before the things depending on it. It runs in the cleanup stage and is registered to ignore every
   exception type - a failing teardown must not mask the failure the test was actually about.

Step 3 is what removes most flakiness from container-backed tests. A started SQL Server is not a ready
database, and readiness is checked rather than assumed.

## Persistent environments

Some infrastructure is too expensive to rebuild per run. A persistent environment keeps a shared root
component alive across runs while per-run components stay per-run:

- a shared root is created once and reused,
- a worker component that depends on it is still fresh for every run,
- so you get freshness where it matters and reuse where rebuilding would be waste.

Create one with the asynchronous factory rather than the constructor - constructing one blocks the
calling thread for the whole bootstrap and can deadlock under a synchronisation context, which is why
that overload is obsolete.

## Choosing where a test runs

Environments are what make the [local to Docker to live](../how-to/local-to-docker-to-live.md)
progression a single test suite instead of three. Same timeline, different provider, increasing realism
and cost.

## See also

- <xref:TestFramework.Core.Environment>
- [TestFramework.Container](../packages/container.md) - the shared Docker building blocks
- [TestFramework.Container.Web](../packages/container-web.md),
  [TestFramework.Container.Azure](../packages/container-azure.md)
