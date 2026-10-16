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

## Not measured

- A cold file: every worker reads its own range, so on a spinning disk the access pattern is random.
- Files on a network share.
