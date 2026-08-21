# Your first timeline

The smallest useful test has three parts: a timeline built once, a run that executes it, and an
assertion that the run finished. Start with the smallest legal version of all three.

[!code-csharp[](../examples/snippets/basic/01-minimal-timeline.cs)]

Three things are worth naming before anything is added to this.

**The timeline is a field, the run is local.** A timeline is immutable once `Build()` returns, so it
costs nothing to share and cannot be corrupted by a run. The run is the thing that carries state, and
it exists only inside the test method.

**`RunAsync()` does not throw on failure.** It returns a finished run, including a failed one, because
a failure is a result you may want to assert against. `EnsureRanToCompletion()` is what turns "this
run failed" into "this test failed".

**The output helper is how you see anything.** Pass it and the run prints its whole timeline; leave it
out and the test still passes in silence. Every example in this documentation passes it.

Run this yourself: [example 01 - minimal timeline](../examples/basic/01-minimal-timeline.md), which is
the same code with its output.

Next: adding a step that actually does something.
