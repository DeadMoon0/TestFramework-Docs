# TestFramework.Web

Drives REST APIs, the SQL Server databases behind them, and stubbed dependencies - from inside a
timeline. A request is a step; the response is a step result.

```bash
dotnet add package TestFramework.Web
```

## Quickstart

Configure the API by identifier:

```jsonc
{
  "Api": {
    "sample": {
      "BaseUrl": "http://localhost:5080/",
      "HealthPath": "/health",
      "Auth": "None"
    }
  }
}
```

Then write the timeline:

```csharp
ConfigInstance config = ConfigInstance.FromJsonFile("local.testsettings.json")
    .LoadWebConfig()
    .Build();

Timeline timeline = Timeline.Create()
    .SetVariable("itemId", Var.Const("2"))
    .Trigger(WebExt.Api.IsLive("sample", ApiAlivenessLevel.Healthy)).Name("live")
    .Trigger(WebExt.Api.Http("sample")
        .Get("api/items/{id}")
        .WithRouteValue("id", Var.Ref<string>("itemId"))
        .Call()).Name("get-item")
    .Build();

TimelineRun run = await timeline.SetupRun(config).RunAsync();
run.EnsureRanToCompletion();

run.ApiStatus("get-item").Should().Be(HttpStatusCode.OK);
run.ApiJson<SampleItem>("get-item").Select(item => item.Id).Should().Be("2");
```

## Conceptual model

| Concept | What it is |
|---|---|
| **Identifier** | the logical name of an API. Timelines name it; they never name a URL |
| **`ApiConfig`** | everything needed to reach one identifier: base URL, health path, auth, timeout |
| **`WebExt.Api.Http(...)`** | a two-stage builder: choose method and path, then shape and `Call()` |
| **`HttpResponseContext`** | the step result - plain data, so it survives the debugging transport |
| **`IHttpSender`** | the seam deciding *how* a request travels; swapped by hosting environments |
| **`web.restapi`** | the environment requirement kind an HTTP step declares |

The first row is the one that pays off later: because the plan names an identifier, the same timeline runs
against a deployed API, a locally hosted one, or a container - only the environment changes.

## Status codes are results

An unsuccessful status is returned to the timeline so you can assert on it:

```csharp
run.ApiStatus("missing").Should().Be(HttpStatusCode.NotFound);
```

Only transport problems - connection refused, DNS failure, timeout - raise `ApiRequestFailedException`. A
404 is an answer; an unopened socket is not.

This also means `Get(...)` does not retry a 404 away. When a locally started host may still be warming up,
put `IsLive(...)` in front: that step absorbs the startup 404 or 503, and the call you are testing stays
honest.

## Liveness has two levels

`Reachable` proves the socket opened. `Healthy` proves the health path answered. Different questions, kept
separate on purpose.

```csharp
run.ApiProbe("live").Select(probe => probe.Success).Should().Be(true);
```

## SQL: a row is an artifact

```csharp
Timeline.Create()
    .Trigger(WebExt.Sql.IsLive("orders-db", SqlAlivenessLevel.Database)).Name("live")
    .SetupArtifact("seeded")
    .Trigger(WebExt.Sql.Scalar<int>("orders-db", "SELECT COUNT(1) FROM [Orders]")).Name("count")
    .Build();
```

Teardown deletes by default, and that covers found rows as much as seeded ones: a
`SqlRowArtifactReference` always knows its key, so it can always delete its row. `SqlRowWhereFinder` is
no exception - discovering a row does not protect it. Say so yourself when a timeline only reads:

```csharp
timeline.FindArtifact("order", WebExt.ArtifactFinder.Sql.Where<Order>("main", "Name = @name"))
    .MarkReadonly()
```

See [deleting is the default](../concepts/artifacts.md#deleting-is-the-default-and-markreadonly-is-the-opt-out).

A SQL script executes as one session, so `#temp` tables, `SET` options and a transaction opened in one
`GO` batch survive into the next - as they do in SSMS.

## Stubs: what the application sent

```csharp
.WaitForEvent(WebExt.Stub.Called("pricing", HttpMethod.Post, "/api/quotes"))
    .WithTimeOut(TimeSpan.FromSeconds(30)).Name("quoted")
.Trigger(WebExt.Stub.Calls("pricing")).Name("audit")
```

```csharp
run.StubCall("quoted").Select(call => call.Body).Should().Contain("\"quantity\":6");
run.StubUnmatchedCalls("audit").Should().HaveCount(0);
```

`StubUnmatchedCalls` is the assertion people forget: it catches calls the application made that no stub
expected - behaviour you did not know to look for.

`Stub.Reset(...)` records a watermark rather than deleting the request log, so it is safe against a stub
shared with other runs. Only `ClearServerLog` destroys evidence, and only a stub this run owns gives fully
isolated verification - on a shared one, narrow the wait with `WithHeader(...)`.

## Gotchas

**Cookies are off by default.** The HTTP clients are pooled per identifier, so a cookie jar would be shared
by every run against that identifier. Set `ApiConfig.UseCookies` only when the session really is a cookie,
and expect it to be shared.

**Auth includes Windows Negotiate**, configured per identifier rather than per request.

## Going deeper

- [Learn: your first real system](../../learn/first-real-system.md)
- Chapters W1 to W5 in the [Examples](../../examples/index.md)
- <xref:TestFramework.Web>, <xref:TestFramework.Web.Trigger.IsLive>
- [Package guide](https://github.com/DeadMoon0/TestFramework-Web/blob/main/TestFramework.Web/README.md) and
  [error handling notes](https://github.com/DeadMoon0/TestFramework-Web/blob/main/Documentation/ERROR-HANDLING-WEB.md)
