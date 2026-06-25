# FixedWidthParser

[![CI](https://github.com/GabrielMarquezMatte/FixedWidthParser/actions/workflows/ci.yml/badge.svg)](https://github.com/GabrielMarquezMatte/FixedWidthParser/actions/workflows/ci.yml)
[![CodeQL](https://github.com/GabrielMarquezMatte/FixedWidthParser/actions/workflows/codeql.yml/badge.svg)](https://github.com/GabrielMarquezMatte/FixedWidthParser/actions/workflows/codeql.yml)
[![codecov](https://codecov.io/gh/GabrielMarquezMatte/FixedWidthParser/branch/master/graph/badge.svg)](https://codecov.io/gh/GabrielMarquezMatte/FixedWidthParser)
[![NuGet](https://img.shields.io/nuget/v/FixedWidthParser.NET.svg)](https://www.nuget.org/packages/FixedWidthParser.NET)
[![NuGet downloads](https://img.shields.io/nuget/dt/FixedWidthParser.NET.svg)](https://www.nuget.org/packages/FixedWidthParser.NET)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Benchmarks](https://img.shields.io/badge/benchmarks-GitHub%20Pages-informational)](https://gabrielmarquezmatte.github.io/FixedWidthParser/dev/bench/)

A high-performance, low-allocation library for **parsing and writing fixed-width (flat) files** in .NET 10. Columns are declared with attributes, layouts are validated up front, and the hot paths work over spans so fixed-width records can be parsed, streamed and written without the usual per-line churn.

The package includes both:

- a runtime parser/writer API for regular attribute-mapped models; and
- a bundled Roslyn source generator for reflection-free `TryParse` implementations, including a UTF-8 byte path.

## Features

- **Attribute-driven** column mapping (`[FixedColumn(start, length)]`) on properties and public fields.
- **Runtime parsing** for single lines and lazy batch reading from a `TextReader`, `Stream` or file.
- **Source-generated parsing** for `partial` models implementing `IFixedWidthModel<TSelf>`.
- **UTF-8 byte parsing** via `Utf8FixedWidthParser<T>`, `FixedWidthByteReader<T>` and generated `IUtf8FixedWidthModel<TSelf>` models, avoiding `StreamReader` and UTF-16 transcoding for ASCII-style flat files.
- **Synchronous and asynchronous readers** (`IEnumerable<T>` / `IAsyncEnumerable<T>`) with struct enumerators on the synchronous path.
- **`System.IO.Pipelines` support** on the byte path: read records straight from a `PipeReader` (`FixedWidthByteReader<T>.ReadAsync(PipeReader)` / `FixedWidthUtf8.ReadAsync<T>(PipeReader)`).
- **Writing** for single records and batches, synchronous and asynchronous, with `StreamWriter` reuse and `ReadOnlySpan<T>` overloads for zero-allocation output.
- **Configurable formatting** per column: alignment, padding character, format string and explicit overflow policy.
- **Culture-aware** numeric parsing/formatting, including `double`/`float` via csFastFloat and generic `ISpanParsable` / `ISpanFormattable` support.
- **Layout validation** at construction or generation time: negative `Start`, non-positive `Length` and overlapping columns fail clearly.
- **`ref struct` model support** on parser/source-generated single-line parsing.

## Requirements

- .NET 10 (`net10.0`)

Dependencies: [CommunityToolkit.HighPerformance](https://www.nuget.org/packages/CommunityToolkit.HighPerformance) (`StringPool`) and [csFastFloat](https://www.nuget.org/packages/csFastFloat) (fast `double`/`float` parsing).

## Installation

```bash
dotnet add package FixedWidthParser.NET
```

Or as a `<PackageReference>`:

```xml
<PackageReference Include="FixedWidthParser.NET" Version="1.0.0" />
```

The package ships the Roslyn source generator bundled as an analyzer. Models that implement `IFixedWidthModel<TSelf>` or `IUtf8FixedWidthModel<TSelf>` get generated parsers automatically; no extra package or setup is required.

## Defining a Model

Runtime/reflection models only need a public parameterless constructor and mapped fields or properties:

```csharp
using FixedWidthParser.Attributes;

public readonly record struct Person
{
    public Person()
    {
        Name = string.Empty;
        Age = 0;
        Salary = 0.0;
    }

    [FixedColumn(0, 10)] public string Name { get; init; }
    [FixedColumn(10, 5)] public int Age { get; init; }
    [FixedColumn(15, 10)] public double Salary { get; init; }
}
```

`start` is the 0-based offset and `length` is the column width.

For source generation, make the model `partial` and implement one or both marker interfaces:

```csharp
using FixedWidthParser;
using FixedWidthParser.Attributes;

public readonly partial record struct GeneratedPerson :
    IFixedWidthModel<GeneratedPerson>,
    IUtf8FixedWidthModel<GeneratedPerson>
{
    [FixedColumn(0, 10)] public string Name { get; init; }
    [FixedColumn(10, 5)] public int Age { get; init; }
    [FixedColumn(15, 10)] public double Salary { get; init; }
}
```

The generator emits distinct `TryParse` overloads for `ReadOnlySpan<char>` and `ReadOnlySpan<byte>` when both interfaces are present.

## Parsing

### Runtime Single-Line Parsing

```csharp
using System.Globalization;
using FixedWidthParser.Parsers;

var parser = new FixedWidthParser<Person>();

if (parser.TryParse("John Doe  30   60000.00  ", CultureInfo.InvariantCulture, stringPool: null, out var person))
{
    // person.Name == "John Doe"
    // person.Age == 30
    // person.Salary == 60000.0
}
```

### Source-Generated Single-Line Parsing

```csharp
using System.Globalization;
using FixedWidthParser;

if (FixedWidth.TryParse<GeneratedPerson>(
        "John Doe  30   60000.00  ",
        CultureInfo.InvariantCulture,
        stringPool: null,
        out var person))
{
    // reflection-free generated parser
}
```

### Reading Text Files

```csharp
using System.Globalization;
using FixedWidthParser.Readers;

var reader = new FixedWidthReader<Person>(CultureInfo.InvariantCulture);

foreach (var person in reader.ReadFile("people.txt"))
{
    // ...
}
```

`Read(TextReader)` and `Read(Stream, encoding, leaveOpen)` are also available. Reading is lazy and reuses a pooled buffer; lines are sliced directly from the buffer, so the reader does not allocate a string per line. A malformed line throws a `FormatException` carrying the line number.

Empty lines (including a trailing newline at end of file) are skipped: they are counted toward the line number but not yielded as records. A line that is non-empty but shorter than the declared layout is treated as malformed and throws.

The source-generated facade has matching overloads:

```csharp
foreach (var person in FixedWidth.ReadFile<GeneratedPerson>("people.txt", formatProvider: CultureInfo.InvariantCulture))
{
    // generated TryParse for each line
}
```

### Async Reading

```csharp
await foreach (var person in reader.ReadFileAsync("people.txt"))
{
    // ...
}
```

`ReadAsync(TextReader)` and `ReadAsync(Stream, encoding, leaveOpen)` mirror the synchronous overloads; `ReadFileAsync` uses true async file I/O. Cancellation is honored via `WithCancellation`.

## UTF-8 Byte Parsing

For ASCII/single-byte fixed-width files, the UTF-8 APIs parse directly from bytes. This avoids `StreamReader`, avoids UTF-8 to UTF-16 transcoding, and keeps offsets measured in bytes.

```csharp
using System.Globalization;
using FixedWidthParser.Readers;

var reader = new FixedWidthByteReader<Person>(CultureInfo.InvariantCulture);

foreach (var person in reader.ReadFile("people.txt"))
{
    // parsed from raw UTF-8 bytes
}
```

Generated UTF-8 models use `FixedWidthUtf8`:

```csharp
using System.Globalization;
using FixedWidthParser;

if (FixedWidthUtf8.TryParse<GeneratedPerson>(
        "John Doe  30   60000.00  "u8,
        CultureInfo.InvariantCulture,
        stringPool: null,
        out var person))
{
    // generated byte parser
}

await using var stream = File.OpenRead("people.txt");
await foreach (var person in FixedWidthUtf8.ReadAsync<GeneratedPerson>(stream, formatProvider: CultureInfo.InvariantCulture))
{
    // async raw-byte streaming
}
```

Column offsets on the UTF-8 path are byte offsets. That is ideal for the ASCII-style payloads common in flat files; with multi-byte UTF-8 characters, byte offsets and character offsets are not the same.

The byte path supports the same `StringPool` interning as the char path: pass a pool to `FixedWidthByteReader<T>` / the `FixedWidthUtf8` methods (the `stringPool` argument above) and string columns are interned through it; pass `null` to decode a fresh string per value.

### Reading from a PipeReader

When the source is already a [`System.IO.Pipelines.PipeReader`](https://learn.microsoft.com/dotnet/standard/io/pipelines) — a Kestrel request body, a socket, a named pipe, or an upstream pipeline stage — you can parse straight off it, letting the pipe own buffering and read-ahead. Both the reflection reader and the generated facade expose a `PipeReader` overload of `ReadAsync`:

```csharp
using System.IO.Pipelines;
using System.Globalization;
using FixedWidthParser;
using FixedWidthParser.Readers;

// Reflection reader:
var reader = new FixedWidthByteReader<Person>(CultureInfo.InvariantCulture);
await foreach (var person in reader.ReadAsync(pipeReader))
{
    // parsed from the pipe
}

// Source-generated facade:
await foreach (var person in FixedWidthUtf8.ReadAsync<GeneratedPerson>(pipeReader, formatProvider: CultureInfo.InvariantCulture))
{
    // ...
}
```

Lines are sliced from the pipe's `ReadOnlySequence<byte>` and parsed in place when contiguous, copying into a pooled buffer only when a line spans segment boundaries. The same line semantics apply (BOM skipped once, `\n`/`\r\n`, empty lines skipped, trailing line without a newline yielded). By default the reader is completed when iteration ends; pass `leaveOpen: true` to leave it open.

> **When to use it.** Prefer the `PipeReader` overload when you already hold a pipe. For plain files and streams the `Stream`/file overloads remain the faster default — a `PipeReader` adds per-read overhead that only pays off when there is real I/O to overlap (network, slow disk), not for in-memory or local-file sources.

## Writing

```csharp
using System.Globalization;
using FixedWidthParser.Writers;

var writer = new FixedWidthWriter<Person>();
var people = new[]
{
    new Person { Name = "John Doe", Age = 30, Salary = 60000 },
    new Person { Name = "Jane",     Age = 28, Salary = 55000 },
};

using var stream = File.Create("out.txt");
writer.WriteMany(stream, people.AsSpan(), CultureInfo.InvariantCulture);
```

Overloads cover `Stream`/`StreamWriter` with `IEnumerable<T>`/`ReadOnlySpan<T>`, plus `WriteAsync` and `WriteManyAsync`. Reusing a `StreamWriter`, or passing a span, keeps writing allocation-free per line.

## Formatting Options

Each column can be tuned through named attribute arguments:

```csharp
[FixedColumn(0, 8, Alignment = Alignment.Right, Padding = '0')] public int Id { get; init; }       // "00000042"
[FixedColumn(8, 10, Format = "F2")]                            public double Amount { get; init; } // "1234.50   "
[FixedColumn(18, 5, Overflow = OverflowBehavior.Truncate)]     public string Code { get; init; }
```

- **`Alignment`**: `Left` (default) or `Right`.
- **`Padding`**: fill character (default space; for example `'0'` for zero-padding).
- **`Format`**: format string passed to `ISpanFormattable` (for example `"F2"` or `"N0"`); ignored for `string`.
- **`Overflow`**: `Default`, `Truncate` or `Throw`. `Default` resolves per type: strings truncate, numeric types throw.

## Culture Handling

Pass an `IFormatProvider` to `TryParse`, the reader constructor, the source-generated facade methods or the write methods. The generic path (`ISpanParsable`/`ISpanFormattable`) and the `double`/`float` processors honor it. When the provider is `null`, `'.'` is used as the decimal separator.

## StringPool

Pass a `CommunityToolkit.HighPerformance.Buffers.StringPool` to intern repeated string-column values:

```csharp
using System.Globalization;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Readers;

var pool = new StringPool();
var reader = new FixedWidthReader<Person>(CultureInfo.InvariantCulture, stringPool: pool);
```

This is a time-vs-memory trade-off: pooling removes repeated string allocations but costs extra CPU for hashing and lookup. Prefer it for GC-sensitive or high-concurrency workloads; skip it for raw throughput.

Pooling applies to both the `char` and UTF-8 byte paths (`FixedWidthReader<T>`/`FixedWidthByteReader<T>` and the `FixedWidth`/`FixedWidthUtf8` facades), and to `ref struct` models. When no pool is supplied, each string column is decoded into a fresh string.

## Validation

Invalid layouts fail fast with an `InvalidOperationException` on the runtime parser/writer paths, or generator diagnostics on generated models. Negative `Start`, `Length < 1`, and overlapping columns are rejected. Adjacent columns are valid.

## `ref struct` Models

The parser accepts `ref struct` models (`where TModel : new(), allows ref struct`), useful for stack-only row processing:

```csharp
using FixedWidthParser.Attributes;
using FixedWidthParser.Parsers;

public ref struct Row
{
    public Row()
    {
        Name = string.Empty;
        Age = 0;
    }

    [FixedColumn(0, 10)] public string Name { get; set; }
    [FixedColumn(10, 5)] public int Age { get; set; }
}

var parser = new FixedWidthParser<Row>();
parser.TryParse(line, CultureInfo.InvariantCulture, null, out var row);
```

Batch readers and the writer use regular generic constraints because `IEnumerable<T>` cannot carry a `ref struct`.

## Performance

Measured with BenchmarkDotNet (`MemoryDiagnoser`) on .NET 10. Highlights:

- Parsing a line is allocation-light on the runtime path and reflection-free on the generated path.
- Text readers avoid allocating a string per line by slicing a reusable buffer.
- UTF-8 byte readers avoid `StreamReader` and transcoding for ASCII-style flat files.
- Writing with `StreamWriter` reuse or `ReadOnlySpan<T>` is zero-alloc per line.

### Comparison with other libraries

#### Reading

Batched read of N fixed-width records (10-char string + 5-char int + 10-char double) from an in-memory string, compared against [FileHelpers](https://www.filehelpers.net/), [FlatFiles](https://github.com/jehugaleahsa/FlatFiles), and [RecordParser](https://github.com/leandromoh/RecordParser).

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
```

## Building and Testing

```bash
dotnet build FixedWidthParser.slnx -c Release
dotnet test tests/FixedWidthParser.Tests/FixedWidthParser.Tests.csproj
dotnet test tests/FixedWidthParser.Generator.Tests/FixedWidthParser.Generator.Tests.csproj
```
