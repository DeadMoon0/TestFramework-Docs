# Running it against Docker

The timeline you just wrote does not change. What changes is who serves the identifier.

```bash
dotnet add package TestFramework.Container.Web
```

## One call switches the world

A run gets an environment through `SetEnv(...)`. The environment starts what the timeline needs and
publishes the resulting addresses into the same configuration store a settings file would have filled:

```csharp
TimelineRun run = await timeline.SetupRun(config)
    .SetEnv(DockerWebEnvironment.For<SampleSqlDefinition>())
    .RunAsync();
```

That is the whole switch. The timeline still names `"orders"` and `"orders-db"`; the environment decides
those are containers it brought up.

## Declare what the infrastructure is made of

A definition class describes the shape, once, in code rather than in a compose file:

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

`WithSchemaFromModels<...>()` derives the schema from the model types, so the test's database and the
application's model cannot drift apart silently.

## Four sources of truth

Containers make the interesting assertion possible: the stubbed dependency. Now four independent records
exist, and an application can only satisfy all four by being correct.

```csharp
private static readonly Timeline _timeline = Timeline.Create()
    .Trigger(WebExt.Api.Http("orders")
        .Post("api/orders")
        .WithJsonBody(Var.Const(new { name = "Complete Order", quantity = 6 }))
        .Call()).Name("create")
    .WaitForEvent(WebExt.Stub.Called("pricing", HttpMethod.Post, "/api/quotes"))
        .WithTimeOut(TimeSpan.FromSeconds(30)).Name("quoted")
    .FindArtifact("written", WebExt.ArtifactFinder.Sql.Where<ShowroomOrder>("orders-db", "Name = @name")
        .WithParameter("name", Var.Const("Complete Order")))
    .Trigger(WebExt.Stub.Calls("pricing")).Name("audit")
    .Build();
```

```csharp
run.ApiStatus("create").Should().Be(HttpStatusCode.Created);                          // what it claimed
run.SqlRow<ShowroomOrder>("written").Select(order => order.Quantity).Should().Be(6);  // what it wrote
run.StubCall("quoted").Select(call => call.Body).Should().Contain("\"quantity\":6");  // what it sent
run.StubUnmatchedCalls("audit").Should().HaveCount(0);                                // what nobody authorised
```

That last line is the one people forget. It asserts on calls the application made that no stub expected -
the behaviour you did not know to look for.

## Docker chapters skip rather than fail

A test that needs a daemon should not turn red on a laptop that has none. The Showroom's own gate shows
the pattern: a custom `[DockerFact]` attribute probes for a reachable daemon and sets the xunit skip
reason when there is not one, so the test reports "requires Docker" instead of an error.

Worth knowing on Windows: the container support points `DOCKER_HOST` at whichever Docker Desktop named
pipe exists, because the client library does not probe for it and the pipe name differs between
installations. Ports are published on `127.0.0.1`, so a SQL Server a test brought up is not exposed to
the rest of the network for the length of the run.

## The progression this belongs to

Local, then Docker, then live is one path, not three test suites:

| Stage | What is real | What it costs |
|---|---|---|
| Local | nothing external; in-process steps | fast, runs anywhere |
| Docker | the API, the database, the stubs | needs a daemon; still hermetic |
| Live | the deployed system | slow, shared, and the only true answer |

The same timeline runs at every stage. See [local to Docker to live](../guide/how-to/local-to-docker-to-live.md).

## Where to look next

- Run it: chapter W5 in the [Examples](../examples/index.md)
- Package: [TestFramework.Container.Web](../guide/packages/container-web.md)

Next: [where to go from here](next-steps.md).
