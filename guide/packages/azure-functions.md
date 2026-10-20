# TestFramework.Azure.Functions

The parts of [TestFramework.Azure](azure.md) that need a Function App's own code: running an HTTP handler
in-process, and reading a function's route, verb or name off its isolated-worker attributes.

```bash
dotnet add package TestFramework.Azure.Functions
```

They live in a package of their own because they need ASP.NET Core and the Azure Functions Worker, which
`TestFramework.Azure` does not bring. A test that only calls deployed or container-hosted Function Apps does not
need it. Configuration, identifiers and results are `TestFramework.Azure`'s.

## Quickstart

Run a handler in the test process, with no Functions host and no deployment:

```csharp
using TestFramework.Azure;
using TestFramework.Azure.Functions;

Timeline timeline = Timeline.Create()
    .Trigger(AzureExt.Trigger.FunctionApp
        .InProcessHttp<OrderFunction>((function, call) => function.Run(call.HttpRequest))
        .WithHeader(Var.Const("x-trace"), Var.Const("order-42"))
        .Call())
    .Build();
```

`OrderFunction` is resolved from the run's services, so register it there. `call` hands the delegate the
ASP.NET Core `HttpRequest` built from the payload stage, `GetService<T>()` from the run's services, and
`GetVariable(...)` for timeline variables.

## What it adds

Everything extends builders `TestFramework.Azure` already has, so a call reads exactly as it does there.

| Call | What it does |
|---|---|
| `AzureExt.Trigger.FunctionApp.InProcessHttp<T>(...)` | runs your delegate against `T` from the run's services, with a request built from the payload stage |
| `AzureExt.Trigger.FunctionApp.Http(id).SelectEndpointWithMethod<T>(method)` | calls a remote Function App at the route and verb the method's `[Function]` and `[HttpTrigger]` attributes declare |
| `AzureExt.Trigger.FunctionApp.Managed<T>(id, method)` | runs a function through the host's admin endpoint, by the name in its `[Function]` attribute |

Without this package, `Http(id).SelectFunction(name, verb)` and `ManagedFunction(id, name)` make the remote calls
by name.

## The response is the host's

An in-process call answers with what the Functions host would send for the same `IActionResult` - an object as
camelCase JSON, a string as text - so assertions written against it still hold when the test moves to a deployed
app. `ObjectResult`, `ContentResult`, `JsonResult` and `StatusCodeResult` are supported.

Pick the overload that matches what the handler returns: an `Action` or a `Task` for a handler with no result, an
`IActionResult` or a `Task<IActionResult>` for one that returns it.

## Endpoints from attributes

```csharp
.Trigger(AzureExt.Trigger.FunctionApp.Http("Default")
    .SelectEndpointWithMethod<HttpTests>(nameof(HttpTests.Run))
    .Call())
```

The route is read from the method's `[HttpTrigger]` and taken relative to the host's route prefix, so
`FunctionAppConfig.RoutePrefix` is applied once.

## Troubleshooting

**The step fails on the handler's result.** Only the four result types above are supported; any other is
refused as a configuration mistake rather than turned into a response the host would not send.

**A trigger with more than one verb is refused.** The attributes do not say which one this test means. State it:
`SelectEndpointWithMethod<T>(method, HttpMethod.Post)`.

**`[FunctionName]` methods are not found.** Those belong to the in-process Functions model, whose attributes this
package does not read. Select them by name with `SelectFunction(name, method)`.

**The function type cannot be resolved.** `InProcessHttp<T>` takes `T` from the run's services; register it
there.

## Going deeper

- [Package guide](https://github.com/DeadMoon0/TestFramework-Azure/blob/main/TestFramework.Azure.Functions/README.md)
- [TestFramework.Azure](azure.md), whose configuration and identifiers this package uses
- <xref:TestFramework.Azure.Functions>
