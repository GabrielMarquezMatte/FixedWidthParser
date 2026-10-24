# Documentation

The evidence behind the code: measurements, and the designs they picked or rejected. Code comments state
a constraint; the numbers that justified it live here, so they can be re-run and disputed
([STYLEGUIDE.md § Comments](../STYLEGUIDE.md#findings-do-not-belong-in-code)).

- [writer-performance.md](writer-performance.md) — where a written line spends its time, and the
  formatting shortcuts that came out of it.
- [parallel-aggregate.md](parallel-aggregate.md) — how `AggregateFileAsync` and `AggregateAsync`
  scale with the number of workers.
- [pipe-reader.md](pipe-reader.md) — why a `PipeReader` is read through `AsStream()` rather than
  its own enumerator.
- [fluent-layout.md](fluent-layout.md) — reading and writing through a `FixedWidthLayout<T>` against
  the classes it replaced.

Usage documentation is the [README](../README.md); the shape of the code is
[ARCHITECTURE.md](../ARCHITECTURE.md).
