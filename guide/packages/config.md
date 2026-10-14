# TestFramework.Config

Prepares the `IServiceProvider` and `IConfiguration` a timeline run needs. Use it as soon as a test
needs environment-specific settings, service registration, or per-test overrides.

```bash
dotnet add package TestFramework.Config
```

## Quickstart

```csharp
await using ServiceProvider serviceProvider = ConfigInstance
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

Note the `await using`. The provider owns every singleton it creates, so the test owns the provider -
dispose it, asynchronously because some singletons can only be disposed that way, or leak whatever it built.

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

## Saying it and asking for it

There is one place to say where a resource is, and one place to ask:

| Your question | The answer |
|---|---|
| How do I prepare config and services for `SetupRun(...)`? | `ConfigInstance` |
| How does a step read a named record like `MainSql`? | `context.Configured<T>("MainSql")` |
| How does a test read a value off a finished run? | `run.Values.Require(ValueRef.For(...))` |

A package's `Load*Config()` call says which sections it reads and what each describes; it does not read them.
The run does, once, before the first step:

```csharp
ConfigInstance config = ConfigInstance.FromJsonFile("local.testsettings.json")
    .LoadWebConfig()      // the web package's sections
    .LoadAzureConfig()    // and the Azure package's
    .Build();
```

`Configured<T>` does not say whether a person wrote the address down or a container decided it while
starting. That is what lets the same timeline run against a deployed resource and a containerized one.

## Decision guide

- JSON files, a few overrides, ordinary service registration → `ConfigInstance`.
- A reusable base with per-test variants → build one, then `SetupSubInstance()`.
- A step needs a named record such as `MainSql` or `Default` → `context.Configured<T>(identifier)`.

## Troubleshooting

**A module cannot find its identifier.** The relevant `Load*Config()` call is missing. Some of them are
required even when the settings file has no matching section, because they declare the section the
environment publishes into.

**Singletons outliving the test.** The provider was not disposed. `await using` on the provider, not on the
`ConfigInstance`.

## Going deeper

- [Configuration patterns](../how-to/configuration-patterns.md)
- <xref:TestFramework.Config>
- [Package guide in the repository](https://github.com/DeadMoon0/TestFramework-Core/blob/main/TestFramework.Config/README.md)
