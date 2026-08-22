# TestFramework.Container.Web

Serves the API, the database and the stubbed dependencies a [TestFramework.Web](web.md) timeline needs -
from Docker containers.

A timeline written against a deployed system runs here unchanged. It still names an identifier; this
environment decides that identifier is served by a container it starts, and publishes the address into the
same configuration store a settings file would have filled.

```bash
dotnet add package TestFramework.Container.Web
```

Needs a reachable Docker daemon.

## Quickstart

Declare what the database is made of:

```csharp
internal sealed class SampleSqlDefinition : DockerSqlDefinition
{
    public override SqlIdentifier Identifier => "main";

    protected override void Configure(DockerSqlBuilder builder) => builder
        .WithDatabase("SampleDb")
        .WithSchemaFromModels<Order, Customer>()
        .WithResetMode(SqlResetMode.RecreateDatabase);
}
```

Then point a run at it:

```csharp
ConfigInstance config = ConfigInstance.Create()
    .LoadWebConfig()
    .AddWebSqlModels(models => models.For<Order>().Table("Orders").Key(x => x.Id).MaxLength(x => x.Name, 200))
    .Build();

TimelineRun run = await timeline.SetupRun(config)
    .SetEnv(DockerWebEnvironment.For<SampleSqlDefinition>())
    .AddArtifact("order",
        WebExt.Artifact.Sql.Row<Order>("main", Var.Const("1")),
        new SqlRowArtifactData<Order>(new Order { Id = 1, Name = "sample", Quantity = 3 }))
    .RunAsync();

run.EnsureRanToCompletion();
run.SqlScalar<int>("count").Should().Be(1);
```

`LoadWebConfig()` is required even with no `Sql` section in the settings file: it registers the store the
container publishes into.

## Schema from models

`WithSchemaFromModels<...>()` derives the database schema from the model types, so the test database and
the application's model cannot drift apart silently. `AddWebSqlModels(...)` is where the mapping details
live - table name, key, column lengths - and it is worth being explicit there, because a `MaxLength` that
exists in the application and not in the test schema is a bug the test cannot see.

`WithResetMode(...)` decides what "clean" means between runs, and there are three answers: `None` creates
the database if it is missing and leaves whatever it contains, `RunResetScript` runs your reset script
after the schema is in place, and `RecreateDatabase` drops and recreates it so every run starts from the
declared schema alone. Start with `RecreateDatabase` and trade down only when its cost is measurable.

## The four sources of truth

Containers make the assertion that a deployed environment cannot: what the application *sent* to its
dependencies.

```csharp
run.ApiStatus("create").Should().Be(HttpStatusCode.Created);                          // claimed
run.SqlRow<ShowroomOrder>("written").Select(order => order.Quantity).Should().Be(6);  // wrote
run.StubCall("quoted").Select(call => call.Body).Should().Contain("\"quantity\":6");  // sent
run.StubUnmatchedCalls("audit").Should().HaveCount(0);                                // unauthorised
```

An application can fake any one of those. Faking all four requires being correct.

## Tests should skip, not fail, without Docker

A machine without a daemon should report "requires Docker", not a wall of errors. The pattern the Showroom
uses: a custom `[DockerFact]` attribute that probes for a reachable daemon in its constructor and sets the
xunit skip reason when there is none.

Pair it with a trait such as `[Trait("Category", "DockerSmoke")]` and the two concerns stay separate: the
trait answers "should the fast lane run this?" and needs a filter; the skip answers "will this fail
environmentally?" and needs nothing from anybody.

## Troubleshooting

**The daemon is not found on Windows.** The shared container layer points `DOCKER_HOST` at whichever Docker
Desktop named pipe exists and logs its choice - read that line first.

**A container started but the test failed to connect.** Readiness is checked before the main stage, so this
usually means the component's readiness probe is testing the wrong thing - a started SQL Server is not a
ready database.

## Going deeper

- [Learn: running it against Docker](../../learn/into-docker.md)
- <xref:TestFramework.Container.Web>
- [Package guide](https://github.com/DeadMoon0/TestFramework-Container/blob/main/TestFramework.Container.Web/README.md)
