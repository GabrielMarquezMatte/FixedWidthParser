# FixedWidthParser

[![CI](https://github.com/GabrielMarquezMatte/FixedWidthParser/actions/workflows/ci.yml/badge.svg)](https://github.com/GabrielMarquezMatte/FixedWidthParser/actions/workflows/ci.yml)
[![CodeQL](https://github.com/GabrielMarquezMatte/FixedWidthParser/actions/workflows/codeql.yml/badge.svg)](https://github.com/GabrielMarquezMatte/FixedWidthParser/actions/workflows/codeql.yml)
[![codecov](https://codecov.io/gh/GabrielMarquezMatte/FixedWidthParser/branch/master/graph/badge.svg)](https://codecov.io/gh/GabrielMarquezMatte/FixedWidthParser)
[![NuGet](https://img.shields.io/nuget/v/FixedWidthParser.NET.svg)](https://www.nuget.org/packages/FixedWidthParser.NET)
[![NuGet downloads](https://img.shields.io/nuget/dt/FixedWidthParser.NET.svg)](https://www.nuget.org/packages/FixedWidthParser.NET)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Benchmarks](https://img.shields.io/badge/benchmarks-GitHub%20Pages-informational)](https://gabrielmarquezmatte.github.io/FixedWidthParser/dev/bench/)

A high-performance, low-allocation library for **parsing and writing fixed-width (flat) files** in .NET. Columns are declared with attributes or described in code, layouts are validated up front, and lines are parsed straight from the file's bytes, without a string per line.

Everything goes through one static class, `FixedWidth`, whose every method comes in two overloads:

- one for models the bundled Roslyn source generator implements (`IFixedWidthModel<TSelf>`): reflection-free and statically dispatched; and
- one that takes a `FixedWidthLayout<T>`, built from `[FixedColumn]` attributes or fluently in code.

Upgrading from 1.x? See [the 2.0 entry in the changelog](CHANGELOG.md#200).

## Features

- **Attribute-driven** column mapping (`[FixedColumn(start, length)]`) on properties and public fields, or a **fluent layout** bound with delegates, for types you cannot annotate. Fluent layouts are trimming- and Native AOT-safe.
- **Byte positions.** Lines are read and written as bytes, with no transcoding to UTF-16. Text columns are decoded with the file's encoding: UTF-8 by default, or any ASCII-compatible encoding such as Latin-1 or Windows-1252.
- **Lazy reading** from a `Stream` or a file, synchronous (`IEnumerable<T>` with a struct enumerator) and asynchronous (`IAsyncEnumerable<T>`).
- **Source-generated parsing and writing** for `partial` models implementing `IFixedWidthModel<TSelf>`.
- **Errors that name the column**: a `FixedWidthParseException` with the line number, column, position and raw text, and an `OnParseError` callback to skip bad lines.
- **Bounded memory on untrusted input**: `MaxLineLength` stops a source without line breaks from growing the buffer.
- **Writing** of single records and batches, synchronous and asynchronous, batched into one pooled buffer.
- **Configurable formatting** per column: alignment, padding character, format string and explicit overflow policy.
- **Culture-aware** numbers and dates, including `double`/`float` via csFastFloat and any `IUtf8SpanParsable`/`ISpanParsable` type.
- **Layout validation** when a layout is built or generated: negative `Start`, non-positive `Length`, overlapping columns and non-ASCII padding fail clearly.
- **`ref struct` model support** on single-line parsing and formatting.

## Requirements

- `net10.0`.

Dependencies: [CommunityToolkit.HighPerformance](https://www.nuget.org/packages/CommunityToolkit.HighPerformance) (`StringPool`) and [csFastFloat](https://www.nuget.org/packages/csFastFloat) (fast `double`/`float` parsing).

## Installation

```bash
dotnet add package FixedWidthParser.NET
```

The package ships the Roslyn source generator bundled as an analyzer. Models that implement `IFixedWidthModel<TSelf>` get a generated parser and writer automatically; no extra package or setup is required.

## Defining a Model

An attribute model needs a public parameterless constructor and mapped fields or properties; `FixedWidthLayout.FromAttributes<Person>()` turns it into a layout, built once per type by reflection:

```csharp
using FixedWidthParser.Attributes;

public readonly record struct Person
{
    public Person()
    {
        Name = string.Empty;
    }

    [FixedColumn(0, 10)] public string Name { get; init; }
    [FixedColumn(10, 5)] public int Age { get; init; }
    [FixedColumn(15, 10)] public double Salary { get; init; }
}
```

`start` is the 0-based offset and `length` is the column width, both **in bytes**. In UTF-8 a character such as `é` takes two bytes, so a layout that counts characters only matches a UTF-8 file whose text is ASCII; for accented text, read the file in the single-byte encoding it was written in (see [Encodings](#encodings)).

For source generation, make the model `partial` and implement the marker interface:

```csharp
using FixedWidthParser;
using FixedWidthParser.Attributes;

public readonly partial record struct GeneratedPerson : IFixedWidthModel<GeneratedPerson>
{
    [FixedColumn(0, 10)] public string Name { get; init; }
    [FixedColumn(10, 5)] public int Age { get; init; }
    [FixedColumn(15, 10)] public double Salary { get; init; }
}
```

## Layouts without attributes

For a type you cannot annotate, or a layout known only at run time, describe the columns in code. Each column binds through a getter (used to write) and a setter (used to read), so nothing is reflected over:

```csharp
using FixedWidthParser;
using FixedWidthParser.Attributes;

var layout = FixedWidthLayout.Build<Order>(b => b
    .Factory(() => new Order())
    .Column<int>("Id", 0, 6, (in Order o) => o.Id, (ref Order o, int v) => o.Id = v,
        new FixedWidthColumnOptions { Alignment = Alignment.Right, Padding = '0' })
    .Column("Name", 6, 20, (in Order o) => o.Name, (ref Order o, string v) => o.Name = v)
    .NullableColumn<decimal>("Discount", 26, 10, (in Order o) => o.Discount, (ref Order o, decimal? v) => o.Discount = v)
    .DecodedColumn<DateOnly>("Date", 36, 8, (in Order o) => o.Date, (ref Order o, DateOnly v) => o.Date = v,
        new FixedWidthColumnOptions { Format = "yyyyMMdd" })
    .ConvertedColumn("Total", 44, 12, (in Order o) => o.Total, (ref Order o, Money v) => o.Total = v, new MoneyConverter()));
```

| Method | Column type |
|---|---|
| `Column` | `string` |
| `Column<T>` | a type that parses from and formats to UTF-8: numbers, `Guid`, … |
| `DecodedColumn<T>` | a type that parses only from characters (decoded first) |
| `NullableColumn<T>` | `T?` of a `Column<T>` type; a blank column is `null` |
| `ConvertedColumn<T>` | any type, through an `IFixedWidthConverter<T>` instance |

`FixedWidthColumnOptions` carries the same settings as `[FixedColumn]`, with the same defaults (see [Formatting Options](#formatting-options)). A reference-type model needs `Factory` to be read; a value type without one starts from `default`. Pass `null` as the getter or the setter to build a layout that only reads or only writes; using it the other way throws `InvalidOperationException` before any line is touched. The column name appears in error messages.

## Reading

```csharp
using FixedWidthParser;

var layout = FixedWidthLayout.FromAttributes<Person>();

foreach (var person in FixedWidth.ReadFile("people.txt", layout))
{
    // ...
}

foreach (var person in FixedWidth.ReadFile<GeneratedPerson>("people.txt"))
{
    // generated TryParse for each line
}
```

`Read(Stream, …, leaveOpen)` reads a stream once; `ReadFile` reopens the file on each enumeration. Reading is lazy and reuses a pooled buffer; each line is parsed in place, without a string per line. A layout is immutable: build it once and share it across reads and threads.

Empty lines (including a trailing newline at end of file) are skipped: they are counted toward the line number but not yielded. Lines end at `\n`, with a preceding `\r` dropped, and a leading UTF-8 byte order mark is skipped.

### Async

```csharp
await foreach (var person in FixedWidth.ReadFileAsync("people.txt", layout))
{
    // ...
}
```

`ReadAsync(Stream, leaveOpen)` mirrors the synchronous overload, and cancellation is honored via `WithCancellation`. A [`PipeReader`](https://learn.microsoft.com/dotnet/standard/io/pipelines) — a Kestrel request body, a socket — is read through `pipeReader.AsStream()`.

### Parallel aggregation

To fold a large file into one result — totals, counts, groups — read it on several threads:

```csharp
public sealed class Payroll : IRecordAccumulator<Payroll, Person>
{
    public double Total { get; private set; }

    public void Add(in Person person)
    {
        Total += person.Salary;
    }

    public void Merge(Payroll following)
    {
        Total += following.Total;
    }
}

var payroll = await FixedWidth.AggregateFileAsync<Person, Payroll>("people.txt", layout);
// GeneratedPayroll: the same accumulator over GeneratedPerson.
var generated = await FixedWidth.AggregateFileAsync<GeneratedPerson, GeneratedPayroll>("people.txt");
```

The file is cut into byte ranges, each folded into its own accumulator on a worker thread, and the
accumulators are merged left to right in file order. `Add` must touch only its own instance; `Merge`
runs on one thread. Every line is parsed exactly once, and a failing line reports the same line number
as a sequential read. `FixedWidthParallelOptions.DegreeOfParallelism` caps the threads (`0`, the
default, means one per processor; `1` reads the file in one pass). `OnParseError`, when set, runs on
the worker threads.

`AggregateAsync` does the same over a `Stream`, which is read once: one thread reads it into chunks
and the others parse.

### Options

Every read takes an optional `FixedWidthReaderOptions`:

```csharp
var options = new FixedWidthReaderOptions
{
    FormatProvider = CultureInfo.GetCultureInfo("pt-BR"),
    Encoding = Encoding.Latin1,
    StringPool = new StringPool(),
    BufferSize = 64 * 1024,
    MaxLineLength = 4096,
    OnParseError = ex => { log.Warning(ex.Message); return true; }, // skip the line
};
var people = FixedWidth.ReadFile("people.txt", layout, options);
```

| Option | Default | Meaning |
|---|---|---|
| `FormatProvider` | invariant culture | Numbers and dates. |
| `Encoding` | UTF-8 | Text columns. Must be ASCII-compatible. |
| `StringPool` | none | Interns repeated string values. |
| `BufferSize` | 65,536 | Bytes read from the source at a time. |
| `MaxLineLength` | 1,048,576 | Longest line accepted; a longer one throws `InvalidDataException`. |
| `OnParseError` | throw | Return `true` to skip a line that fails to parse. |

### Errors

A line that fails to parse throws `FixedWidthParseException`, a `FormatException` that says where:

```text
Line 2 could not be parsed into Person: column 'Age' (start 10, length 5) holds "XX   ".
```

`LineNumber`, `ColumnName`, `ColumnStart`, `ColumnLength` and `RawValue` carry the same information. A line shorter than the layout reports how long it is and how long the layout needs it to be. The parser pays nothing for this on lines that parse: a failed line is parsed a second time to find the column.

### Untrusted input

A reader buffers a line until it finds its line break. `MaxLineLength` bounds that buffer, so a request body or file without line breaks throws `InvalidDataException` instead of growing the buffer until memory runs out. Set it close to your layout's line length when the source is untrusted.

## Single Lines

```csharp
FixedWidth.TryParse("John Doe  30   60000.00  "u8, layout, default, out Person person);

FixedWidth.TryParse<GeneratedPerson>(lineBytes, new FixedWidthContext { FormatProvider = culture }, out var generated);
```

`FixedWidthContext` carries the format provider, the encoding and the string pool for one line; `default` means invariant culture, UTF-8 and no pool.

## Writing

```csharp
var people = new[]
{
    new Person { Name = "John Doe", Age = 30, Salary = 60000 },
    new Person { Name = "Jane",     Age = 28, Salary = 55000 },
};

using var stream = File.Create("out.txt");
FixedWidth.WriteMany(stream, people.AsSpan(), layout);
FixedWidth.WriteMany(stream, generatedPeople);   // generated models need no layout
```

`Write`, `WriteMany` (span or enumerable) and their async versions write each record followed by `NewLine`, batched into one pooled buffer. `FixedWidthWriterOptions` sets the `FormatProvider`, the `Encoding` and the `NewLine` (default `Environment.NewLine`). `FixedWidth.TryFormat(in model, layout, Span<byte>, in FixedWidthContext, out int)`, or its generated overload without the layout, formats one line into your own buffer.

## Formatting Options

Each column can be tuned through named attribute arguments:

```csharp
[FixedColumn(0, 8, Alignment = Alignment.Right, Padding = '0')] public int Id { get; init; }       // "00000042"
[FixedColumn(8, 10, Format = "F2")]                            public double Amount { get; init; } // "1234.50   "
[FixedColumn(18, 5, Overflow = OverflowBehavior.Truncate)]     public string Code { get; init; }
[FixedColumn(23, 5, TrimChar = '0', TrimMode = TrimMode.Leading)] public int Count { get; init; }
```

- **`Alignment`**: `Left` (default) or `Right`.
- **`Padding`**: fill byte when writing (default space; for example `'0'` for zero-padding). Must be ASCII.
- **`TrimChar`** / **`TrimMode`**: what to strip when parsing, and from which side. Must be ASCII.
- **`Format`**: format string for `ISpanFormattable` when writing, and the exact format for `DateTime`, `DateOnly`, `TimeOnly` and `DateTimeOffset` when parsing.
- **`Overflow`**: `Default`, `Truncate` or `Throw`. `Default` resolves per type: strings truncate, everything else throws. A truncated string keeps whole characters, so a multi-byte character is never split.
- **`Converter`**: a type implementing `IFixedWidthConverter<T>`, used instead of the built-in parsing and formatting.

## Column Types

`string`, `double` and `float`, any `IUtf8SpanParsable<T>` (parsed straight from bytes), any other `ISpanParsable<T>` (decoded first), `T?` of any of them (a blank column is `null`), and anything with a `Converter`. Writing needs `string`, `ISpanFormattable` or a converter.

## Encodings

Column positions are bytes, so the encoding only decides how text inside a column is read and written. UTF-8 files with ASCII text and single-byte encodings (Latin-1, Windows-1252, …) keep one byte per character. UTF-16 and EBCDIC are not supported.

```csharp
var options = new FixedWidthReaderOptions { Encoding = Encoding.Latin1 };
var records = FixedWidth.ReadFile<GeneratedPerson>("legacy.txt", options);
```

On .NET, Windows-1252 and other code pages need `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` first.

## Validation

Invalid layouts fail fast with an `InvalidOperationException` from `FixedWidthLayout.Build` or `FromAttributes`, or a generator diagnostic on generated models: negative `Start`, `Length < 1`, overlapping columns, a non-ASCII `Padding` or `TrimChar` (FWP012), an unsupported column type and a converter for the wrong type. Adjacent columns are valid. An attribute model that can be read but not written (or the reverse), such as one with a column type that only formats, gets a layout anyway; the side it cannot do throws on first use.

## Versioning

The package follows [Semantic Versioning](https://semver.org/). A minor version may add types, members, options and enum values, but does not remove or change anything; every pull request checks the package against the last release.

- **Interfaces you implement** — `IFixedWidthModel<TSelf>` (normally through the generator) and `IFixedWidthConverter<T>` — never gain a member in a minor version. New settings arrive as new properties on `FixedWidthContext`, which every call receives.
- **Types that appear in signatures only so enumeration stays allocation-free** — `IRecordLineParser<TModel>`, the `*LineParser` strategy structs and the `*RecordEnumerable`/`*RecordEnumerator` types — are implemented only by the library. Program against `IEnumerable<T>`/`IAsyncEnumerable<T>`; their shape can change in a minor version.
- **`FixedWidthRuntime`** and **`ColumnFormatOptions`** are public because generated code calls them; they are not meant to be called directly.

## Performance

Measured with BenchmarkDotNet (`MemoryDiagnoser`) on .NET 10 with 1.1, whose reader decoded text to UTF-16 first; the 2.0 numbers are yet to be measured. Highlights:

- Parsing a line is allocation-light on the runtime path and reflection-free on the generated path.
- Readers parse each line in place, without a string per line.
- Writing a span of records is zero-alloc per line.

### Comparison with other libraries

#### Reading

Batched read of N fixed-width records (10-char string + 5-char int + 10-char double), compared against [FileHelpers](https://www.filehelpers.net/), [FlatFiles](https://github.com/jehugaleahsa/FlatFiles), and [RecordParser](https://github.com/leandromoh/RecordParser).

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8655/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-1355U 1.70GHz, 1 CPU, 12 logical and 10 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
```

| Method                     | Count |       Mean |    Error |    StdDev | Ratio | RatioSD |     Gen0 |    Gen1 | Allocated | Alloc Ratio |
|--------------------------- |------:|-----------:|---------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|
| FixedWidthParser_Generated |   100 |   2.661 μs | 0.052 μs |  0.062 μs |  0.10 |    0.02 |   0.6294 |       - |   3.86 KB |        0.09 |
| FixedWidthParser_Read      |   100 |   4.460 μs | 0.074 μs |  0.065 μs |  0.16 |    0.04 |   0.6256 |       - |   3.86 KB |        0.09 |
| RecordParser_Read          |   100 |   7.047 μs | 0.091 μs |  0.080 μs |  0.26 |    0.06 |   0.6866 |       - |   4.23 KB |        0.10 |
| FileHelpers_Read           |   100 |  28.860 μs | 2.630 μs |  7.671 μs |  1.07 |    0.39 |   6.7444 |  0.2136 |  41.49 KB |        1.00 |
| FlatFiles_Read             |   100 |  40.173 μs | 0.747 μs |  0.699 μs |  1.49 |    0.36 |  16.7847 |  0.5493 | 103.13 KB |        2.49 |
|                            |       |            |          |           |       |         |          |         |           |             |
| FixedWidthParser_Generated |  1000 |  26.273 μs | 0.347 μs |  0.325 μs |  0.13 |    0.00 |   6.2256 |       - |  38.31 KB |        0.09 |
| FixedWidthParser_Read      |  1000 |  43.935 μs | 0.467 μs |  0.390 μs |  0.21 |    0.00 |   6.2256 |       - |  38.31 KB |        0.09 |
| RecordParser_Read          |  1000 |  72.391 μs | 1.392 μs |  1.429 μs |  0.35 |    0.01 |   6.2256 |       - |  38.69 KB |        0.10 |
| FileHelpers_Read           |  1000 | 205.683 μs | 3.662 μs |  3.058 μs |  1.00 |    0.02 |  66.1621 | 17.0898 | 406.42 KB |        1.00 |
| FlatFiles_Read             |  1000 | 402.237 μs | 6.329 μs |  5.610 μs |  1.96 |    0.04 | 155.2734 |  4.8828 |  953.2 KB |        2.35 |

The reflection-based reader is **~5× faster** and allocates **11× less** than FileHelpers. The source-generated reader reaches **~8× faster** at the same allocation footprint.

#### Writing

Write of N fixed-width records to `Stream.Null` (formatting cost only, no I/O).

| Method                 | Count |          Mean |        Error |       StdDev |        Median | Ratio | RatioSD |      Gen0 |      Gen1 | Allocated | Alloc Ratio |
|----------------------- |------:|--------------:|-------------:|-------------:|--------------:|------:|--------:|----------:|----------:|----------:|------------:|
| FixedWidthParser_Write |     1 |      121.4 ns |      2.01 ns |      1.57 ns |      120.9 ns |  0.71 |    0.01 |         - |         - |         - |        0.00 |
| FileHelpers_Write      |     1 |      170.2 ns |      1.40 ns |      1.17 ns |      170.3 ns |  1.00 |    0.01 |    0.0572 |         - |     360 B |        1.00 |
| FlatFiles_Write        |     1 |      357.2 ns |      5.71 ns |      5.07 ns |      355.9 ns |  2.10 |    0.03 |    0.1135 |         - |     712 B |        1.98 |
| RecordParser_Write     |     1 |   29,258.0 ns |    542.81 ns |    507.75 ns |   29,232.1 ns | 171.96 |    3.11 |    7.9956 |    1.0376 |  49,640 B |      137.89 |
|                        |       |               |              |              |               |       |         |           |           |           |             |
| FixedWidthParser_Write |   100 |    8,665.8 ns |    169.30 ns |    424.73 ns |    8,519.2 ns |  0.77 |    0.04 |         - |         - |         - |        0.00 |
| FileHelpers_Write      |   100 |   11,283.8 ns |    187.38 ns |    166.11 ns |   11,255.9 ns |  1.00 |    0.02 |    5.2338 |         - |  32,832 B |        1.00 |
| FlatFiles_Write        |   100 |   25,250.0 ns |    493.33 ns |    411.95 ns |   25,367.7 ns |  2.24 |    0.05 |   11.3220 |         - |  71,200 B |        2.17 |
| RecordParser_Write     |   100 |   87,556.0 ns |  1,820.98 ns |  5,224.72 ns |   86,343.9 ns |  7.76 |    0.47 |    8.3008 |    0.9766 |  51,953 B |        1.58 |
|                        |       |               |              |              |               |       |         |           |           |           |             |
| FixedWidthParser_Write |  1000 |   86,454.8 ns |  1,695.66 ns |  1,952.72 ns |   86,407.9 ns |  0.71 |    0.03 |         - |         - |         - |        0.00 |
| FileHelpers_Write      |  1000 |  122,427.3 ns |  2,439.24 ns |  5,797.11 ns |  120,769.0 ns |  1.00 |    0.06 |   52.2461 |         - | 328,032 B |        1.00 |
| FlatFiles_Write        |  1000 |  258,895.1 ns |  5,051.42 ns |  4,725.10 ns |  258,890.0 ns |  2.12 |    0.10 |  113.2813 |         - | 712,000 B |        2.17 |
| RecordParser_Write     |  1000 |  754,218.4 ns | 14,792.11 ns | 29,880.79 ns |  749,527.8 ns |  6.17 |    0.37 |  173.8281 |  171.8750 | 1,076,624 B |        3.28 |

The writer is **~30% faster** than FileHelpers and allocates **nothing** per batch, regardless of record count.

Run benchmarks:

```bash
dotnet run -c Release --project tests/Benchmarks/Benchmarks.csproj -- --filter "*Comparison*"
dotnet run -c Release --project tests/Benchmarks/Benchmarks.csproj -- --filter "*ReaderBenchmarks*"
```

Benchmark reports are written to `tests/Benchmarks/BenchmarkDotNet.Artifacts/results`.

## Project Layout

```text
src/FixedWidthParser/                  The library
src/FixedWidthParser.Generator/        Roslyn source generator
tests/FixedWidthParser.Tests/          Runtime, reader, writer and parity tests
tests/FixedWidthParser.Generator.Tests/Source generator tests
tests/Benchmarks/                      BenchmarkDotNet benchmarks
tests/AotSmoke/                        Fluent layout published with NativeAOT
docs/                                  Measurements behind design decisions
```

[ARCHITECTURE.md](ARCHITECTURE.md) maps the code; [CONTRIBUTING.md](CONTRIBUTING.md) and
[STYLEGUIDE.md](STYLEGUIDE.md) cover how to change it.

## Building and Testing

```bash
dotnet build FixedWidthParser.slnx -c Release
dotnet test --solution FixedWidthParser.slnx -c Release
```
