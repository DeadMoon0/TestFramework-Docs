# Configuration patterns

Configuration is where an integration suite quietly rots: one test's override leaks into another, a
connection string appears in three places, and eventually nobody can say what a test is actually pointing
at. These patterns exist to prevent that.

## Start from one base, derive per test

```csharp
ConfigInstance shared = ConfigInstance
    .FromJsonFile("appsettings.test.json")
    .Build();

var providerA = shared.SetupSubInstance().OverrideConfig("Run:Tenant", "A").BuildServiceProvider();
var providerB = shared.SetupSubInstance().OverrideConfig("Run:Tenant", "B").BuildServiceProvider();
```

`SetupSubInstance()` derives rather than mutates. Two variants of the same base cannot contaminate each
other, which is the property you want when the suite grows past a handful of tests.

## Load every package's config explicitly

```csharp
ConfigInstance config = ConfigInstance.FromJsonFile("local.testsettings.json")
    .LoadWebConfig()
    .LoadAzureConfig()
    .Build();
```

Some of these calls are required even when the settings file has no matching section, because they register
the store that an environment publishes into. `LoadWebConfig()` with no `Sql` section is the canonical
example: without it, a container environment has nowhere to publish the database address it just created.

If a module reports that it cannot resolve an identifier, this is the first thing to check.

## Name identifiers for what they are, not where they are

```jsonc
{
  "Api": {
    "orders": { "BaseUrl": "http://localhost:5080/", "HealthPath": "/health", "Auth": "None" }
  }
}
```

`"orders"` is good. `"localhost5080"` is not, and neither is `"staging"` - both bake an environment into a
name that will outlive it. The identifier names the *role* a system plays in the scenario; the record says
how to reach it today.

This is what makes [local to Docker to live](local-to-docker-to-live.md) work at all.

## Keep secrets out of the file you commit

The settings file in the repository should contain placeholders and localhost addresses. Anything real
belongs in user secrets, environment variables, or a file the repository ignores - layered on with
`OverrideConfig(...)` or a second configuration source.

For container-backed runs this is easier than it sounds: the placeholders are all a definition needs,
because the environment rewrites them to the endpoints it created.

## Dispose the provider

```csharp
using ServiceProvider serviceProvider = ConfigInstance
    .FromJsonFile("appsettings.test.json")
    .BuildServiceProvider();
```

The provider owns every singleton it creates. A test class that builds one per test and disposes none will
accumulate HTTP clients, database connections and container handles until something gives.

## One config per test class, not per test

Build it once as a field and derive per-test variants with `SetupSubInstance()` when a single test needs
something different. The timeline is already shared this way; configuration should follow the same shape.

The reason is consistency, not speed:

| What | Per build |
|---|---|
| `ConfigInstance.Create()` + `BuildServiceProvider()` | ~0.011 ms |
| `SetupSubInstance()` + `BuildServiceProvider()` from a shared base | ~0.012 ms |
| `ConfigInstance.FromJsonFile(...)` + `BuildServiceProvider()` | ~0.254 ms |
| `SetupSubInstance()` from a base that already loaded that JSON | ~0.014 ms |

Measured on one workstation, 200 warmed-up iterations each - not in the pipeline, and not a benchmark;
the [example chapters](../../examples/index.md) are where pipeline-measured figures live.

So re-reading the settings file per test costs roughly twenty times as much as deriving from a shared
base - and both are a rounding error next to any test that opens a socket. Do not share configuration to
save time. Share it because one place then defines what "the base" is, and because `SetupSubInstance()`
derives rather than mutates, so a per-test override cannot reach another test.

## Going deeper

- [TestFramework.Config](../packages/config.md)
- [The Showroom's configuration notes](https://github.com/DeadMoon0/TestFramework-Showroom/blob/main/Documentation/ConfigurationPatterns.md)
- <xref:TestFramework.Config>
