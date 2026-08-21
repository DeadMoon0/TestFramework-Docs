# Your first real system

Everything so far ran inside the test process. Now point the same shape at an HTTP API and the database
behind it.

```bash
dotnet add package TestFramework.Web
dotnet add package TestFramework.Config
```

## Timelines name identifiers, never URLs

This is the rule that makes the rest possible. Configure the API once, under a logical name:

```jsonc
{
  "Api": {
    "orders": {
      "BaseUrl": "http://localhost:5080/",
      "HealthPath": "/health",
      "Auth": "None"
    }
  }
}
```

The timeline then refers to `"orders"` and never to an address:

```csharp
ConfigInstance config = ConfigInstance.FromJsonFile("local.testsettings.json")
    .LoadWebConfig()
    .Build();

Timeline timeline = Timeline.Create()
    .SetVariable("itemId", Var.Const("2"))
    .Trigger(WebExt.Api.IsLive("orders", ApiAlivenessLevel.Healthy)).Name("live")
    .Trigger(WebExt.Api.Http("orders")
        .Get("api/items/{id}")
        .WithRouteValue("id", Var.Ref<string>("itemId"))
        .Call()).Name("get-item")
    .Build();

TimelineRun run = await timeline.SetupRun(config).RunAsync();
run.EnsureRanToCompletion();

run.ApiStatus("get-item").Should().Be(HttpStatusCode.OK);
run.ApiJson<SampleItem>("get-item").Select(item => item.Id).Should().Be("2");
```

Because the address lives in configuration and not in the plan, this exact timeline will later run
against a container or a deployed environment with nothing changed but the environment.

## Ask whether anyone is home

`IsLive(...)` is a step, so a startup delay is absorbed by the step that is meant to absorb it rather
than by the call you are actually testing:

```csharp
.Trigger(WebExt.Api.IsLive("orders", ApiAlivenessLevel.Healthy)).Name("live")
// ...
run.ApiProbe("live").Select(probe => probe.Success).Should().Be(true);
```

`Reachable` proves the socket opened. `Healthy` proves the health path answered. They are different
questions, and the framework declines to conflate them.

## A request is a step; a response is a result

Every part of a request is variable-backed - path, route values, query, headers, body - which is what
lets one timeline run with many inputs:

```csharp
.Trigger(WebExt.Api.Http("orders")
    .Post("api/orders")
    .WithJsonBody(Var.Const(new { name = "Calibration Order", quantity = 3 }))
    .Call()).Name("create")
```

And remember the rule from the previous page: a non-2xx status is a result you assert on, not an
exception.

## The database is not a second framework

A row is an artifact, so everything you learned about artifacts applies unchanged - including who owns
teardown:

```csharp
Timeline.Create()
    .Trigger(WebExt.Sql.IsLive("orders-db", SqlAlivenessLevel.Database)).Name("live")
    .SetupArtifact("seeded")
    .Trigger(WebExt.Sql.Scalar<int>("orders-db", "SELECT COUNT(1) FROM [Orders]")).Name("count")
    .Build();

// ...
run.SqlScalar<int>("count").Should().Be(1);
```

A seeded row is owned, so teardown removes it.

> [!WARNING]
> Do not read that as "found things are safe". Whether teardown deletes an artifact depends on the
> **reference**, never on the verb that put it there - and some finders hand back deletable references.
> The web package's SQL finder does not, so a row found here is left alone; Azure's Cosmos and Table
> Storage finders do, so a found item there **is deleted** when the run ends.
>
> Before pointing any timeline at data you did not create, read
> [ownership lives on the reference](artifacts.md#ownership-decides-teardown---and-ownership-lives-on-the-reference).

## Two sources of truth beat one

The response says what the application *claims* happened. The row says what it actually *wrote*. Assert
on both and the application has nowhere to hide:

```csharp
run.ApiStatus("create").Should().Be(HttpStatusCode.Created);
run.SqlRow<ShowroomOrder>("written").Select(order => order.Quantity).Should().Be(6);
```

## Where to look next

- Run it: chapters W1 and W2 in the [Examples](../examples/index.md)
- Package: [TestFramework.Web](../guide/packages/web.md)

Next: [running it against Docker](into-docker.md).
