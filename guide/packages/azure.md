# TestFramework.Azure

Azure components inside a timeline: Function Apps, Logic Apps, Service Bus, and the data systems behind
them - SQL, Cosmos DB, Table Storage, Blob Storage.

```bash
dotnet add package TestFramework.Azure
```

## Minimal setup

```csharp
ConfigInstance config = ConfigInstance.FromJsonFile("local.testSettings.json")
    .LoadAzureConfig()
    .Build();
```

`LoadAzureConfig()` reads named records out of the configuration sections and populates the typed stores
the Azure steps resolve at run time.

## Start here

The package is broad, so learn it in this order rather than by reading the support matrix:

1. **Pick stable identifier names** - `Default`, `MainDb`, `MainSBQueue` - and keep them identical in
   timeline code and configuration.
2. **Register the smallest config shape** that matches the resource you actually need.
3. **Run one canonical flow per resource family** before combining them.

The three canonical flows:

| Family | Flow |
|---|---|
| Function App | `AzureExt.Trigger.FunctionApp.Http("Default") ... .Call()` |
| Service Bus | send with `AzureExt.Trigger.ServiceBus.Send(...)`, then wait with `AzureExt.Event.ServiceBus.MessageReceived(...)` |
| Logic App | `CallAndCapture()` for stateless workflows; `Call()` plus `RunCompleted(...)` for stateful ones |

For data systems, start with a single artifact or a single finder against one named identifier before
composing an end-to-end scenario.

## Identifiers, not connection strings

Each identifier such as `"MainDb"` maps to one child object inside the matching configuration section.
Timelines name the identifier; the record supplies the connection details. That indirection is what lets
the same timeline run against live Azure or against
[emulators in Docker](container-azure.md) with no change to the plan.

## Config: one setup model, several stores

`ConfigInstance` remains the setup entry point. `ConfigStore<T>` is not a competing model - it is the
Azure package's typed runtime registry for named records.

The shape stays:

1. build the run provider with `ConfigInstance`,
2. let `LoadAzureConfig()` populate the typed stores inside DI,
3. let Azure steps and finders resolve those stores by identifier at run time.

When you see `ConfigStore<T>` in an advanced sample, read it as a lookup service inside the provider, not
as a second top-level configuration system you must choose between.

## Output bindings are explicit results

Azure APIs surface callback URLs, response payloads and workflow run identifiers. Treat all of them as
ordinary step-result extraction:

- they are not a second, hidden execution model,
- they do not replace `TimelineRun` assertions,
- they are the Azure-shaped way to surface a value the scenario then registers, asserts, or passes on.

If a scenario starts to feel Azure-magic-heavy, pull the extracted value into a named variable or
artifact and keep the remaining assertions generic. That is a reliable signal you have drifted from the
readable path.

## Discovery can delete what it finds

> [!WARNING]
> All three finders in this package hand back deconstructable references, so an artifact they discover
> **is removed at teardown**: `CosmosDbItemArtifactQueryFinder`, `TableStorageEntityArtifactQueryFinder`
> and `SqlEFCoreArtifactQueryFinder`. That is the framework-wide default rather than anything specific to
> Azure - no shipped finder is read-only.
>
> Against a shared or live store, do not rely on `FindArtifact(...)` being read-only. Protect what you
> only meant to read:
>
> ```csharp
> timeline.FindArtifact("order", AzureExt.Artifacts.Cosmos.Query<Order>(...)).MarkReadonly()
> ```
>
> See [deleting is the default](../concepts/artifacts.md#deleting-is-the-default-and-markreadonly-is-the-opt-out).

## Troubleshooting

**An identifier cannot be resolved.** Either `LoadAzureConfig()` is missing, or the section name and the
identifier disagree. The identifier is the child object's name, not the section's.

**Cosmos rejects the certificate.** Against the emulator, tests usually need a `CosmosClientOptions`
override with `DangerousAcceptAnyServerCertificateValidator`. That is emulator-specific, not a general
setting.

**Service Bus receives nothing.** The topology has to exist before the wait does. Prefer configuring it
explicitly through `ConfigureServiceBusTopology(...)` rather than assuming it.

## Going deeper

- Chapters A0 to A9 in the [Examples](../../examples/index.md)
- <xref:TestFramework.Azure>, <xref:TestFramework.Azure.Extensions>
- [Package guide](https://github.com/DeadMoon0/TestFramework-Azure/blob/main/TestFramework.Azure/README.md)
  and [arc42 notes](https://github.com/DeadMoon0/TestFramework-Azure/blob/main/Documentation/Arc42.md)
