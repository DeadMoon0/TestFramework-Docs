# TestFramework.Container

The shared Docker building blocks every container-backed environment needs, and nothing specific to any
one of them. Both [TestFramework.Container.Web](container-web.md) and
[TestFramework.Container.Azure](container-azure.md) are built on it.

You normally consume one of those instead. Reach for this package when writing an environment component
of your own.

```bash
dotnet add package TestFramework.Container
```

## What it provides

| Type | Purpose |
|---|---|
| `ContainerSource` | declares where an application comes from: an image, a project, a directory, a type |
| `ContainerSourcePlan` | what will be done to get it there, stated before anything happens |
| `ContainerImageBuilder` | carries a plan out, producing an image or a directory |
| `ProjectQuery` | asks MSBuild what a project is, instead of inferring it from paths |
| `OfflineFeed` | hands a container the packages the host already restored, so a build needs no credentials |
| `ContainerNetworkFactory` | creates the uniquely named network one environment's containers share |
| `ContainerEndpoints` | the two addresses every container has: host-mapped and network-alias |
| `ContainerReadiness` | waits until an HTTP endpoint or a SQL database actually answers |
| `ContainerLogCapture` | writes a container's output into the run log before it is removed |
| `ContainerStartCoordinator` | starts one component's containers together, and cleans up a failed batch |
| `MsSqlContainerFactory` | builds a SQL Server container from shared settings |

The `Source` → `Plan` → `Builder` sequence is the part worth understanding: what will happen is decided
and stated before anything is built, which is what makes a container environment debuggable rather than
mysterious.

## Three things it does without being asked

**It finds the Docker daemon on Windows.** `ContainerRuntime.EnsureInitialized` runs as the first statement
of the network component in both environments. The client library does not probe for Docker Desktop's
named pipe, and the pipe name differs between installations, so this points `DOCKER_HOST` at whichever one
exists - logging the choice, and changing nothing when `DOCKER_HOST` is already set.

**It keeps published ports off the network.** `ContainerPortBinding` publishes on `127.0.0.1` rather than
all interfaces, so a SQL Server or storage emulator a test started is not reachable from the rest of the
network for the length of the run. It falls back to `0.0.0.0` when the daemon is remote or in
Docker-in-Docker; set `TESTFRAMEWORK_CONTAINER_HOST_IP` to decide explicitly.

**It cleans up after a killed test host.** `ContainerLeftovers.SweepAsync` starts detached and removes what
a killed process left behind. Ryuk reaps containers, networks and volumes and nothing else - so built
images, published output and generated emulator topology would otherwise stay on the machine with nothing
that knows to come back for them.

## Readiness is the anti-flake

A started container is not a ready service. `ContainerReadiness` waits for an actual answer - an HTTP
response, a SQL connection - before the main stage begins. Most flakiness in container-backed test suites
is a missing version of this check.

## Writing an environment component

Two habits carry most of the weight:

1. **Ask, do not infer.** `ProjectQuery` exists because guessing a project's output path from its
   directory is wrong often enough to matter. `ContainerOutputResolver` - the inferring road - is kept for
   older definitions rather than recommended.
2. **State the plan before acting.** Produce a `ContainerSourcePlan`, log it, then execute it. A failure
   during the build then has a plan to be read against.

## Going deeper

- [Environments and providers](../concepts/environments-and-providers.md)
- <xref:TestFramework.Container>, <xref:TestFramework.Container.Sources>
- [Package guide](https://github.com/DeadMoon0/TestFramework-Container/blob/main/TestFramework.Container/README.md)
