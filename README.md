# FixedWidthParser

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

Run benchmarks:

```bash
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
