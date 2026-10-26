# Writer performance

## Machine and method

AMD Ryzen 7 5700X (8C/16T), Windows 10 Pro 19045, `net10.0`, Release.

Two harnesses:

- BenchmarkDotNet, `--job short`, over 1,000 rows of the benchmark `SampleModel` (a 10-byte string, a
  5-byte `int`, a 10-byte `double`), `OperationsPerInvoke = 1000`, so every figure is per row.
- A Stopwatch console app for before/after: 2,000 warm-up rounds, then the best of five runs of 4,000
  rounds over the same 1,000 rows. Its absolute numbers run higher than BenchmarkDotNet's; only the
  ratio between its two columns is meaningful.

`Salary` is `1000.0 + i`, so every `double` is an integer value.

## Where a row's time goes

BenchmarkDotNet, before the shortcuts below:

| Operation | ns/row |
|---|--:|
| whole line, runtime writer | 82.7 |
| whole line, generated model | 68.8 |
| `double` column (`FormatUtf8Value`) | 54.8 |
| `double.TryFormat` to UTF-8, alone | 46.1 |
| `double` as a scaled integer, alone | 8.1 |
| `int` column | 8.1 |
| `int.TryFormat` to UTF-8, alone | 1.7 |
| `string` column | 4.5 |

The `double` column is two thirds of the line. Its cost is the shortest round-trip search, which .NET
runs the same way for `char` and `byte` output, so formatting straight to UTF-8 instead of UTF-16 and
transcoding saved nothing measurable on this model: the `WriterBenchmarks` suite moved within its own
noise.

The runtime writer's extra ~14 ns over the generated model is one interface call and one compiled
getter delegate per column. Compiling the whole line into one delegate would remove it, but the
generated model already does, so it was not built.

## The shortcuts

**Scaled-integer `double`.** A value with at most four decimals and a magnitude under 1e9 is written as
`long` digits with the decimal separator inserted: the smallest scale whose quotient reproduces the
value exactly is the shortest round-trip form. It comes from ExcelReader's XLSX writer, which checks it
against `Utf8Formatter`; here `DoubleFormatTests` checks it against `double.ToString` over ~300,000
values in the invariant culture and pt-BR. It applies only with no `Format` and a provider whose
decimal separator is one ASCII character and whose negative sign is `-`.

**Formatting into the column.** `IUtf8SpanFormattable` values format straight into the line's slice
and are aligned there, instead of into a stack buffer that `WriteInto` then copies. The `int` column's
6 ns above `int.TryFormat` was that buffer and copy.

## Result

Stopwatch console app, per row:

| Case | Before | After |
|---|--:|--:|
| runtime writer, `TryFormat` | 110.1 | 43.1 |
| generated model, invariant, `double` with two decimals | 101.3 | 42.6 |
| generated model, pt-BR, `double` with two decimals | 101.4 | 49.3 |
| runtime writer, `WriteMany` to `Stream.Null` | 116.0 | 57.5 |

## Not done

- `float` and `decimal` still take the BCL formatter.
- Formatting a batch in parallel: records have a fixed length, so record *i* starts at
  `i * RecordLength` and batches split cleanly. Worth it only for very large writes; unmeasured.
