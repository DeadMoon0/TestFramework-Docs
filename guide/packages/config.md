# TestFramework.Config

Prepares the `IServiceProvider` and `IConfiguration` a timeline run needs. Use it as soon as a test
needs environment-specific settings, service registration, or per-test overrides.

```bash
dotnet add package TestFramework.Config
```

## Quickstart

```csharp
using ServiceProvider serviceProvider = ConfigInstance
    .FromJsonFile("appsettings.test.json")
    .OverrideConfig("FeatureFlags:UseMockService", "true")
    .AddService((services, configuration) =>
    {
        services.AddHttpClient();
        services.AddSingleton<IMyDependency, MyDependency>();
    })
    .BuildServiceProvider();

TimelineRun run = await timeline.SetupRun(serviceProvider).RunAsync();
```

Note the `using`. The provider owns every singleton it creates, so the test owns the provider - dispose
it, or leak whatever it built.

## The conceptual model

`ConfigInstance` is the entry point and the thing you should reach for by default. It does four jobs:

1. loads configuration - from JSON, or starting empty with `Create()`,
2. applies overrides for this test,
3. registers services,
4. builds the provider you hand to `SetupRun(...)`.

## Shared base, per-test variants

The pattern that keeps a test suite's configuration honest: define the shared base once, then derive:

```csharp
ConfigInstance shared = ConfigInstance
    .FromJsonFile("appsettings.test.json")
    .Build();

var providerA = shared.SetupSubInstance().OverrideConfig("Run:Tenant", "A").BuildServiceProvider();
var providerB = shared.SetupSubInstance().OverrideConfig("Run:Tenant", "B").BuildServiceProvider();
```

`SetupSubInstance()` derives rather than mutates, so one test's override cannot leak into another's.

## ConfigInstance versus typed stores

Other packages expose typed stores - Azure's `ConfigStore<T>`, the web package's API and SQL stores. It
is easy to read those as a competing setup model. They are not.

| Your question | The answer |
|---|---|
| How do I prepare config and services for `SetupRun(...)`? | `ConfigInstance` |
| How does a module look up a named record like `MainDb` at run time? | that module's typed store, resolved from DI |
| Must I choose one model for the whole test? | no - start with `ConfigInstance`; typed stores live inside it |

The ownership rule: `ConfigInstance` owns the run's container; typed stores live inside that container
and answer identifier lookups at run time.

That is why package setup reads as a chained call rather than a separate system:

```csharp
ConfigInstance config = ConfigInstance.FromJsonFile("local.testsettings.json")
    .LoadWebConfig()      // registers the web package's stores
    .LoadAzureConfig()    // and the Azure package's
    .Build();
```

## Decision guide

- JSON files, a few overrides, ordinary service registration → `ConfigInstance`.
- A reusable base with per-test variants → build one, then `SetupSubInstance()`.
- A module needs named records such as `MainSql` or `Default` → still `ConfigInstance` for setup; treat
  the typed store as an implementation detail consumed from DI.

## Troubleshooting

**A module cannot find its identifier.** The relevant `Load*Config()` call is missing. Some of them are
required even when the settings file has no matching section, because they register the store that the
environment publishes into.

**Singletons outliving the test.** The provider was not disposed. `using` on the provider, not on the
`ConfigInstance`.

## Going deeper

- [Configuration patterns](../how-to/configuration-patterns.md)
- <xref:TestFramework.Config>
- [Package guide in the repository](https://github.com/DeadMoon0/TestFramework-Core/blob/main/TestFramework.Config/README.md)
