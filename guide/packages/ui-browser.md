# TestFramework.UI.Browser

Drives a web application in a real, locally running browser from inside a timeline. The browser is one
more door into the system under test, next to the API, the database and the stubs - the timeline,
variables, retries, timeouts and debugging are the ones the rest of the framework uses.

```bash
dotnet add package TestFramework.UI.Browser
```

## Quickstart

Configure the application by identifier. The entry says which browser and where the application is:

```jsonc
{
  "Ui": {
    "shop": {
      "Browser": "chromium",
      "Channel": "msedge",
      "BaseUrl": "http://localhost:5090/"
    }
  }
}
```

With Edge installed, `"Channel": "msedge"` means nothing has to be downloaded.

Then write the timeline:

```csharp
ConfigInstance config = ConfigInstance.FromJsonFile("local.testsettings.json")
    .LoadUiConfig()
    .Build();

Timeline timeline = Timeline.Create()
    .Trigger(BrowserExt.Session("shop")
        .Navigate("/products")
        .Click("Add Anvil to cart")
        .Expect("1 item in cart"))
        .Label("cart")
    .Build();

TimelineRun run = await timeline.SetupRun(config).RunAsync();

run.EnsureRanToCompletion();
run.UiUrl("shop").Should().Contain("/products");
run.UiLooseMatches("shop").Should().HaveNoItems();
```

## Elements are addressed the way a person describes them

`Click("Add to cart")` looks for something clickable called that - by accessible name, then label, then
placeholder, then test id, then visible text. A control that was restyled, moved or renamed from `Save` to
`Save changes` is still found, because none of those is how the test identified it.

Matching runs in two sweeps: everything exact first, then everything loose. An exact match on any channel
beats a loose match on every channel, so a test never prefers a guess over a certainty. When you want
precision, say so:

```csharp
.Click(Target.Button("Delete").InSection("Saved cards"))  // kind and region pinned
.Click(Target.TestId("submit-order"))                     // test id
.Click(Target.Css("#legacy-submit"))                      // exact selector, when nothing else fits
```

## Loose matches are recorded, never hidden

Every non-exact match lands in the session picture with the channel that matched it.
`run.UiLooseMatches("shop")` and `run.UiWeakestMatch("shop")` assert on them, so a suite can be lenient
while a test is being written and strict in the build.

When a name matches several elements, the step fails and lists the candidates with the dial that resolves
it - `.InSection(...)`, `.Nth(...)`, `.First()` - rather than picking one.

## One identifier, both doors

When the application is the one your API serves, or a site a
[container environment](container-web.md) hosts, name the UI entry like that application and leave
`BaseUrl` out: the browser resolves the address from the same configuration the API and stub steps use.
Two addresses for one name are refused before the run starts, rather than one silently winning.

Viewport, device, channel and headlessness live in configuration too, so the same timeline runs as a
desktop and as a phone by pointing at a second entry with a `Device`.

## A browser on a pipeline agent

A run checks, while it is planned, that the exact browser build this version of Playwright launches is on
the machine, and refuses before anything starts when it is not - naming the build it needs. Three ways to
have it:

- **Use a browser the agent already has.** GitHub's hosted Windows and Ubuntu runners come with Chrome and
  Edge: `"Channel": "chrome"` or `"msedge"`, nothing to download.
- **Install it in the pipeline**, before the tests, with the Playwright of the test project:

  ```yaml
  - run: dotnet build
  - uses: actions/cache@v4
    with:
      path: ~/.cache/ms-playwright
      key: playwright-${{ runner.os }}-${{ hashFiles('**/packages.lock.json', '**/*.csproj') }}
  - run: pwsh tests/MyTests/bin/Debug/net8.0/playwright.ps1 install --with-deps chromium
  ```

  Key the cache on what pins the Playwright version, or an upgrade restores the old build. `--with-deps`
  installs the system libraries Linux needs, which takes root - only the pipeline can do that.
- **Let the run install it**: `"Install": true` on the application entry. The run then installs exactly the
  build it needs, as a step of its own before the first browser step, and logs what it installed. Playwright's
  own builds only - a `Channel` is installed with the machine - and on a bare Linux agent the system libraries
  still need `playwright install-deps` in the pipeline.

## When not to use it

For a suite that tests only a browser UI and nothing behind it, Playwright's own tooling - UI mode, trace
viewer, codegen - is the better fit. This package earns its keep when the browser is one door among
several in one timeline.

## Troubleshooting

**`UiBrowserNotInstalledException`, before the first step.** The browser build the run needs is not on
this machine. Use an installed browser with `"Channel": "msedge"` or `"chrome"`, install it in the pipeline,
or set `"Install": true` - see [A browser on a pipeline agent](#a-browser-on-a-pipeline-agent). Without
`"Install": true` the package never downloads a browser on its own.

**`UiConfigurationException` before the first step.** An identifier has no entry, no `Browser`, or no
address - or two. The message lists the identifiers that are registered. A branded build (`msedge`,
`chrome`) belongs in `Channel`, not in `Browser`.

**`UiTargetNotFoundException`.** Nothing answered to the name, exact or loose. The message lists the names
of that kind the page does offer; it is almost always a typo or outdated wording.

**`UiAmbiguousTargetException`.** Several elements answered. Copy one of the dials the message offers.

Every failure keeps a screenshot, the page markup and the session story as the run's widgets, so the
[debugger](../how-to/debugging-a-run.md) shows what the browser saw.

## Going deeper

- [Package guide](https://github.com/DeadMoon0/TestFramework-UI/blob/main/TestFramework.UI.Browser/README.md),
  [arc42 notes](https://github.com/DeadMoon0/TestFramework-UI/blob/main/Documentation/Arc42.md)
  and [error handling](https://github.com/DeadMoon0/TestFramework-UI/blob/main/Documentation/ERROR-HANDLING-UI.md)
- The Showroom's UI lane: [browser examples](../../examples/ui/u1-storefront.md)
- <xref:TestFramework.UI.Browser>
