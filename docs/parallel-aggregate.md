# Parallel aggregation

## Machine and method

AMD Ryzen 7 5700X (8C/16T), Windows 10 Pro 19045, `net10.0`, Release.

A Stopwatch console app over a 104 MB file of 4,000,000 lines, each a 10-byte string, a 5-byte `int`
and a 10-byte `double`. The accumulator sums the `int` and the `double`. Each figure is the best of
five runs after one warm-up run, with the file in the OS cache; every run's total is checked against
the first. The sequential rows are a `foreach` over `ReadFile` into the same accumulator.

## Result

| Path | Sequential `foreach` | dop 1 | dop 2 | dop 4 | dop 8 | dop 16 |
|---|--:|--:|--:|--:|--:|--:|
| runtime | 246.1 ms | 260.9 | 136.1 | 83.2 | 54.2 | 49.7 |
| generated | 210.0 ms | 183.9 | 103.9 | 57.5 | 42.0 | 41.2 |

Scaling is close to linear up to 4 workers and flattens past the 8 physical cores. Each line still
allocates its `string` column, so the GC and memory bandwidth are the likely ceiling; a model without
string columns, or with a `StringPool`, was not measured.

`dop 1` is one range through the same worker code as the parallel case, and lands within a few
percent of the sequential enumerator on both paths.

## Stream

Same file, generated path, the source opened fresh for each run. The stream engine runs `dop` parsing
workers beside the producer, so `dop 8` is eight parsing threads and one reading flow. The pipe rows
read a `PipeReader.Create(stream, ...)` through `AsStream()`, with the segment size shown.

| Source | dop 1 | dop 2 | dop 4 | dop 8 | dop 16 |
|---|--:|--:|--:|--:|--:|
| file (`AggregateFileAsync`) | 181.5 | 104.5 | 61.8 | 46.8 | 46.4 |
| `Stream` | 192.2 | 85.1 | 44.9 | 35.6 | 31.8 |
| `PipeReader.AsStream()`, 64 KB segments | 194.8 | 78.1 | 46.8 | 38.0 | 39.6 |
| `PipeReader.AsStream()`, default 4 KB segments | 423.7 | 108.1 | 111.9 | 112.5 | 109.2 |

A pipe with 4 KB segments caps the run at the speed one thread can copy 4 KB reads into chunks, so
the pipe's own buffer size, not the engine, sets the ceiling.

The stream engine beats the file engine from `dop 2` up on this cached file. The file engine's ranges
are 3.25 MB at `dop 8` against the stream engine's 256 KB chunks, and each file worker refills a 64 KB
buffer with one `RandomAccess.Read` call at a time. Measured warm, the read size is not it, and the
gap is nearer 10 % than these figures suggest ([reader-performance.md](reader-performance.md)).

## Not measured

- A cold file: every worker reads its own range, so on a spinning disk the access pattern is random.
- Files on a network share.
