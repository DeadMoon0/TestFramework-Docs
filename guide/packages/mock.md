# TestFramework.Mock

The system under test, hosted in-process: your application's own service composition, with chosen
dependencies replaced by declared test doubles. Public entry points are `MockDefinition<TService>`,
`MockEnvironment`, `MockExt.Host` and `MockArtifactFinder`.

```bash
dotnet add package TestFramework.Mock
```

## Quickstart

A Mock-Pack declares one double, once:

```csharp
public sealed class MailSenderPack : MockDefinition<IMailSender>
{
    protected override void Configure(MockBuilder<IMailSender> mock)
    {
        mock.Call(m => m.SendAsync(Arg.Any<string>(), Arg.Any<string>()))
            .Returns(Task.FromResult(true))
            .ProducesArtifact((string to, string subject) => new("sentMail", $"{to}: {subject}"));
    }
}
```

The environment hosts the registrations production makes, with the double where the real dependency was:

```csharp
MockEnvironment environment = MockEnvironment.For(services =>
{
    services.AddSingleton<IMailSender, SmtpMailSender>();
    services.AddSingleton<SignupService>();
}).Include<MailSenderPack>();

Timeline timeline = Timeline.Create()
    .Trigger(MockExt.Host((SignupService signup) => signup.RegisterAsync("ada@example.com"))).Name("register")
    .FindArtifact("sentMail", new MockArtifactFinder("sentMail"))
    .Build();

TimelineRun run = await timeline.SetupRun().SetEnv(environment).RunAsync();

run.EnsureRanToCompletion();
Assert.True(run.MockResult<bool>("register"));
Assert.Equal(1, run.Mock<IMailSender>().CountCalls(m => m.SendAsync("ada@example.com", Arg.Any<string>())));
Assert.Equal("ada@example.com: Welcome", run.ArtifactStore.GetMockArtifact("sentMail").Last.Payload);
```

## Mock is an environment

`MockEnvironment` is an [environment provider](../concepts/environments-and-providers.md) like any other.
Its one component builds a service provider per run from your composition, removes every registration of
each replaced service, and adds that run's double in its place. Runs never share a double, a call log or
a record, and the finished run's effective settings say which pack stood in for which service.

A double never writes into the run. It records what a call left behind — as a real dependency would leave
a file — and a step brings that into the run with Core's `FindArtifact`, exactly as it would find a file
another program wrote. `CaptureArtifactVersion` takes a later look; each look is one
[artifact](../concepts/artifacts.md) version.

## Strict doubles

- A call no setup matches is refused, naming the call and listing the setups that exist.
- A call more than one setup matches is refused, naming them all. There is no precedence rule.
- A setup for a method that returns something must say what it returns: `Returns`, `Compute` or `Throws`.
- `ProducesArtifact` records only when the call completes; a call that throws never produced it.

Pack mistakes surface when the run's environment starts, before the first step.

## Async calls

`MockExt.Host` awaits a returned `Task` or `Task<T>`, so the step finishes when the call does and its
result is what the task yielded. Any other awaitable — a `ValueTask`, a task of a task — is refused when
the timeline is built; call `.AsTask()` on a `ValueTask`.

## Troubleshooting

**`No setup matches ...`.** The system under test made a call the pack does not declare. Add a setup or
widen a matcher. If the step passed anyway, the system under test swallowed the refusal — the call is
still in `run.Mock<T>().RecordedCalls` with `Matched` false.

**`This run has no mock host`.** The run was started without `SetEnv(MockEnvironment.For(...))`.

**The found artifact holds only the last payload.** A version is a look the timeline took, not one per
call. Add a `CaptureArtifactVersion` after each step whose effect you want to keep.

## Going deeper

- [Package guide](https://github.com/DeadMoon0/TestFramework-Mock/blob/main/TestFramework.Mock/README.md),
  [arc42 notes](https://github.com/DeadMoon0/TestFramework-Mock/blob/main/Documentation/Arc42.md)
  and [error handling](https://github.com/DeadMoon0/TestFramework-Mock/blob/main/Documentation/ERROR-HANDLING-MOCK.md)
