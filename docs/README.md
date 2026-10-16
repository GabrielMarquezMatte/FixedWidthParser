# Documentation

The evidence behind the code: measurements, and the designs they picked or rejected. Code comments state
a constraint; the numbers that justified it live here, so they can be re-run and disputed
([STYLEGUIDE.md § Comments](../STYLEGUIDE.md#findings-do-not-belong-in-code)).

- [writer-performance.md](writer-performance.md) — where a written line spends its time, and the
  formatting shortcuts that came out of it.
- [parallel-aggregate.md](parallel-aggregate.md) — how `AggregateFileAsync` scales with the number of
  workers.

Usage documentation is the [README](../README.md); the shape of the code is
[ARCHITECTURE.md](../ARCHITECTURE.md).
