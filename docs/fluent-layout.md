# Moving the runtime path onto `FixedWidthLayout<T>`

2.0 replaced `FixedWidthParser<T>`, `FixedWidthReader<T>` and `FixedWidthWriter<T>` with one layout
object passed to the `FixedWidth` methods. The parsers and formatters moved from `static readonly`
fields of a generic class, built in a static constructor, to fields of a layout instance, and writing
moved into `RecordWriter`, generic over a format strategy. This page checks that neither move cost
anything measurable, and records what generated models gained.

## Machine and method

AMD Ryzen 7 5700X (8C/16T), 32 GB, Windows 10 Pro 19045, .NET SDK 11.0.100-rc.2, `net10.0`, Release.
A Stopwatch console app (not BenchmarkDotNet) over a model of three columns (`string` 10, `int` 5,
`double` 10, lines of 25 bytes plus a line break):

- **read**: `ReadFile` over a 1,000,000-line file, summing one column;
- **write**: `WriteMany` of 1,000,000 records to `Stream.Null`.

Each figure is the best of five runs after one warm-up run. The baseline is `d36b958` (the old
classes); the current build calls `FixedWidth.*(…, FixedWidthLayout.FromAttributes<T>(), …)`. The two
programs ran alternately, twice each; both rounds are shown.

## Result

| Operation | `d36b958` | Layout API |
|---|--:|--:|
| read, attribute model | 66.9 / 64.4 ms | 64.6 / 67.3 ms |
| read, generated model | 66.8 / 66.6 ms | 66.6 / 69.7 ms |
| write, attribute model | 60.5 / 58.0 ms | 60.8 / 60.5 ms |
| write, generated model | — (no stream writer) | 47.1 / 45.9 ms |

Every pair sits within the run-to-run spread of the same build (up to 4.7 %, on the generated read,
whose code did not change). The layout instance costs nothing measurable over the static fields, and
batching through `RecordWriter` matches the old writer.

Generated models can now write to a stream. They do so about a quarter faster than attribute models,
since the generated `TryFormat` has no per-column delegate or interface call.
