# TestFramework.Container.Azure

Runs a normal [TestFramework.Azure](azure.md) timeline against Docker-backed emulators: Blob, Table,
Cosmos, SQL Server and Service Bus from local containers instead of a live Azure environment.

```bash
dotnet add package TestFramework.Container.Azure
```

> [!IMPORTANT]
> Logic Apps are not supported in container mode. Keep Logic App tests on the live Azure-hosted path with
> `TestFramework.Azure`.

## Pick the smallest starting point

| You have | Use |
|---|---|
| An existing Azure timeline and an explicit component graph | `DockerAzureEnvironment.For<TRootDefinition>()` |
| One local Function App plus common bindings | the additive helpers: `ForFunctionApp<TFunctionApp>(...)`, `ForFunctionAppWithStorage<...>(...)`, `ForFunctionAppWithStorageAndServiceBus<...>(...)`, `ForFunctionAppWithCommonBindings<...>(...)` |
| Live Azure resources, or Logic Apps | stay on `TestFramework.Azure` - Docker is deliberately a Container concern |

## What the environment does

It plugs into the run through `SetEnv(...)`, and then:

- starts the required emulator components before the main timeline steps run,
- validates the resolved component graph and binds compatible contracts before startup,
- rewrites the registered Azure config entries to the mapped local Docker endpoints,
- keeps the identifier-driven Azure config contract intact.

The timeline itself remains an ordinary Azure timeline. The environment is the switch that makes the run
container-backed.

## Migrating an existing Azure timeline

The path is deliberately small:

1. Keep the timeline unchanged.
2. Keep the same Azure identifier names in your config stores.
3. Register placeholder config values for those identifiers.
4. Add emulator-specific client options where required.
5. Switch the run builder to `SetEnv(DockerAzureEnvironment.For<TRootDefinition>())`.

In the normal case the only runtime change is that `SetEnv(...)` call plus the definition class describing
the emulator-backed graph. The placeholder values are logical identifiers - the environment rewrites them
to real mapped endpoints during the run - and the packaged `example.local.testsettings.json` shows the
expected shape for the `StorageAccount`, `CosmosDb`, `ServiceBus` and `SqlDatabase` sections.

Placeholders can be registered by the test project, or owned by the definition classes themselves when a
shared test stack wants each component to describe its own shape.

## Prerequisites

- Docker Desktop or another compatible engine, running.
- The test project registers the Azure identifiers the timeline uses.
- Service Bus scenarios need a valid topology - preferably via `ConfigureServiceBusTopology(...)`.
- Cosmos scenarios usually need emulator-specific client options.

**Cosmos is the exception worth calling out.** The emulator uses a development certificate, so tests
generally need a `CosmosClientOptions` override with `DangerousAcceptAnyServerCertificateValidator`. That
is emulator-specific and should not follow you to the live path.

## A documentation gap, stated plainly

This package is the one place where the generated [API reference](../../api/index.md) is thinner than its
siblings: its public surface is not yet fully covered by XML documentation, and the reference shows those
gaps rather than hiding them. Prose here and in the repository's architecture notes is the better source
until that pass is done.

## Going deeper

- Chapters A0 to A9 in the [Examples](../../examples/index.md)
- <xref:TestFramework.Container.Azure>
- [Package guide](https://github.com/DeadMoon0/TestFramework-Container/blob/main/TestFramework.Container.Azure/README.md)
  and [architecture notes](https://github.com/DeadMoon0/TestFramework-Container/blob/main/TestFramework.Container.Azure/Documentation/Architecture.md)
