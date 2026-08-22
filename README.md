![Icon](https://raw.githubusercontent.com/DeadMoon0/TestFramework-Common/96ef4240c1e55ba95a20b99285219a61407c6355/Assets/Icon.svg)

# TestFramework-Docu

The documentation site for the TestFramework packages. This repository holds no framework code - it
builds a site out of what the packages already ship and what the Showroom already runs.

## What the site is made of

| Section | Where it comes from |
|---|---|
| Learn, Guide | Hand-written markdown in `learn/` and `guide/` |
| Examples | Generated from the Showroom chapters by `tools/ShowroomDocs` |
| API | Reflected out of the shipped assemblies, their XML docs and their symbols |

Two rules hold the whole thing together:

1. **Nothing edits generated output.** Every path runs one way and ends in HTML that is left alone.
   Anything that looks like it needs a post-pass over the output belongs in `template/public/main.css`
   or `main.js`, which run at render time.
2. **The API reference documents packages, not source.** `docs.csproj` names the versions; the build
   restores them and reads the assemblies. The site therefore cannot describe API that a consumer is
   unable to install, and no framework repository has to be cloned or built to document it.

## Building it locally

```bash
./Build-Docs.ps1 -Feed ../artifacts/nuget-local -ShowroomPath ../TestFramework-Showroom -Serve
```

Then open <http://localhost:8080>.

Without `-Feed`, packages are restored from nuget.org - which works once the package chain is
published. Without `-ShowroomPath`, the Showroom submodule at `Modules/TestFramework-Showroom` is
used, and the Examples section is skipped if it is not checked out.

`-AllowMissingNarration` downgrades un-narrated chapters from an error to a warning. It exists for the
stretch while chapters are still being written; a normal build refuses them.

## Where the view-source links come from

Every type page links to its source, and that comes from the SourceLink map inside each package's `.pdb`.
Those are not in the `.nupkg`, and - worth knowing before you go looking - **nuget.org does not serve
`.snupkg` from its flat container at all**, for any package. Symbol packages live on symbols.nuget.org
and are only retrievable through the symbol-server protocol, keyed by the signature inside the assembly
rather than by id and version.

So `Build-Docs.ps1` takes whichever route the feed allows: a local feed directory is read for the
`.snupkg` beside the package, and nuget.org is queried through `dotnet-symbol` against
symbols.nuget.org. If neither yields a pdb the build still succeeds, warns which packages lost their
symbols, and those types simply carry no view-source link.

## Capturing example output

The output panels under each chapter's code show what the test actually printed. That is captured
separately, because capturing needs a Docker daemon and a restorable feed while building the site
needs neither:

```bash
./Capture-ShowroomOutput.ps1 -ShowroomPath ../TestFramework-Showroom -Lane TestFramework.Showroom.Basic
./Merge-ShowroomOutput.ps1 -Path run-data/captures
```

Capture measures; merge publishes. The capture writes a working record under `run-data/captures/`, and
the merge turns one or more of those into the committed `showroom-output.json` plus the run-local
`run-data/showroom-measurements.json` that the next build reads timings from.

The result lands in `showroom-output.json`, which is committed on purpose. Volatile detail inside the
captured output - timestamps, ids, paths, box padding - is normalised, so re-capturing an unchanged
Showroom produces no diff in what a panel *shows*. Durations are the deliberate exception: exact, and
recorded with the machine that produced them. If a capture does churn, the normalisation in that script is incomplete; fix it there rather
than committing noise. The one known exception is a chapter whose steps run in parallel: its log lines
genuinely reorder between runs.

## Writing a chapter

Chapters are the Showroom's own `.cs` files. Prose meant for the site goes on `//doc:` lines:
consecutive lines form one block, a bare `//doc:` breaks a paragraph, and everything between two
narration blocks becomes a code block. Ordinary `//` comments stay in the code, because they comment
on the code rather than on the chapter.

That means the page's order is the file's order - no configuration anywhere. Usings, the namespace and
a bare type declaration are dropped as ceremony; a primary constructor is kept, because it is where a
chapter's dependencies come from. `//doc:hide-start` and `//doc:hide-end` drop anything else.

## Publishing

`.github/workflows/publish-docs.yml` does the whole job on a push to `main`, or on demand:

```
discover  ->  capture (one job per lane, in parallel)  ->  publish (merge, build, commit, tag)  ->  deploy
```

**The capture runs before docfx, not after.** The output panels are read from `showroom-output.json`
while the chapter pages are generated, so building first would publish the previous run's panels beside
this run's code.

`discover` asks the generator which lanes exist rather than globbing directories, so what counts as a
lane or a chapter is defined in one place. `capture` runs each lane in its own job with `fail-fast`
off - a lane that cannot start its containers must not stop one that can, and its skip reasons are
themselves publishable. `publish` merges the lane captures, builds the site, uploads it, and commits
the captured output back to `main` tagged `Docu-v<date>.<run>`.

**Content is committed; measurements are not.** A capture produces two kinds of fact and they live in
different places. What an example printed, and whether it passed or skipped, goes in
`showroom-output.json` - committed, so a clone can render every panel without Docker or a package feed.
How long it took, and on what machine, goes in `run-data/` - git-ignored, because a duration is true of
one run on one machine and of nothing else.

That split buys two things. The committed file changes only when an example's behaviour changes, so a
diff always means something. And a timing shows up on the site exactly where it was measured: the
pipeline builds in the same run that measured, so its pages carry exact figures labelled as pipeline
figures, while a local build simply shows no duration - it measured none.

**CI is the source of truth for captured output.** `Merge-ShowroomOutput.ps1` replaces the committed
file rather than merging into it, so a chapter the pipeline could only skip is published as skipped.
The alternative would keep a passing panel alive after the environment that produced it was gone, with
no way for a reader to tell.

**It does not loop**, for three independent reasons - any one would be enough:

1. Pushes made with `GITHUB_TOKEN` do not start new workflow runs. This is the real guard.
2. `paths-ignore` excludes `showroom-output.json`, the only file the workflow commits.
3. Tags are not a trigger; only pushes to `main` are.

### Two things must be true before it can go green

Neither is a fault in the pipeline:

- **The package chain must be on nuget.org.** `Build-Docs.ps1` restores from the public feed, and the
  Showroom deliberately pins published versions only.
- **The Showroom submodule must point at a commit whose chapters carry `//doc:` narration.** A build
  refuses an un-narrated chapter rather than publishing a code dump. Until the narration is pushed and
  the submodule bumped, run the workflow manually with `allow_missing_narration` to see it work.
