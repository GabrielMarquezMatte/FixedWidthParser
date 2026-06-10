# FixedWidthParser

A high-performance, low-allocation library for **parsing and writing fixed-width (flat) files** in .NET 10. Columns are declared with attributes; accessors are compiled once per type via expression trees, so the hot path is allocation-free per line (only the string columns allocate, and even those can be interned with a `StringPool`).

## Features

- **Attribute-driven** column mapping (`[FixedColumn(start, length)]`) on properties *and* public fields.
- **Parsing**: single line, plus lazy batch reading from a `TextReader`, `Stream` or file — synchronous (`IEnumerable<T>` with a struct enumerator) and asynchronous (`IAsyncEnumerable<T>`), without allocating a string per line.
- **Writing**: single record and batches, synchronous and asynchronous, with `StreamWriter` reuse and `ReadOnlySpan<T>` overloads for zero-allocation output.
- **Configurable formatting** per column: alignment, padding character, format string, and an explicit overflow policy (no silent data loss).
- **Culture-aware** for numeric and `ISpanParsable`/`ISpanFormattable` types (including `double`/`float` via csFastFloat).
- **Layout validation** at construction: rejects negative `Start`, non-positive `Length` and overlapping columns with a clear error.
- **`ref struct` model support** on the parser (verified on .NET 10).

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

The package ships the Roslyn source generator bundled as an analyzer, so models that implement
`IFixedWidthModel<TSelf>` get a reflection-free `TryParse` generated automatically — no extra
package or setup required.

## Defining a model

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

A model only needs a parameterless constructor. `start` is the 0-based offset and `length` the column width.

## Parsing

### A single line

```csharp
using System.Globalization;
using FixedWidthParser.Parsers;

var parser = new FixedWidthParser<Person>();

if (parser.TryParse("John Doe  30   60000.00  ", CultureInfo.InvariantCulture, stringPool: null, out var person))
{
    // person.Name == "John Doe", person.Age == 30, person.Salary == 60000.0
}
```

### Many lines / files (synchronous)

```csharp
using FixedWidthParser.Readers;

var reader = new FixedWidthReader<Person>(CultureInfo.InvariantCulture);

foreach (var person in reader.ReadFile("people.txt"))
{
    // ...
}
```

`Read(TextReader)` and `Read(Stream, encoding, leaveOpen)` are also available. Reading is lazy and reuses a single pooled buffer; lines are sliced directly from the buffer, so **no string is allocated per line**. A malformed line throws a `FormatException` carrying the line number.

### Many lines / files (asynchronous)

```csharp
await foreach (var person in reader.ReadFileAsync("people.txt"))
{
    // ...
}
```

`ReadAsync(TextReader)` and `ReadAsync(Stream, encoding, leaveOpen)` mirror the synchronous overloads; `ReadFileAsync` uses true async file I/O. Cancellation is honored via `WithCancellation`.

## Writing

```csharp
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

Overloads cover `Stream`/`StreamWriter` × `IEnumerable<T>`/`ReadOnlySpan<T>`, plus `WriteAsync`/`WriteManyAsync`. Reusing a `StreamWriter` (or passing a span) keeps writing allocation-free per line.

## Formatting options

Each column can be tuned through named attribute arguments:

```csharp
[FixedColumn(0, 8, Alignment = Alignment.Right, Padding = '0')] public int Id { get; init; }      // "00000042"
[FixedColumn(8, 10, Format = "F2")]                            public double Amount { get; init; } // "1234.50   "
[FixedColumn(18, 5, Overflow = OverflowBehavior.Truncate)]     public string Code { get; init; }
```

- **`Alignment`** — `Left` (default) or `Right`.
- **`Padding`** — fill character (default space; e.g. `'0'` for zero-padding).
- **`Format`** — format string passed to `ISpanFormattable` (e.g. `"F2"`, `"N0"`); ignored for `string`.
- **`Overflow`** — `Default`, `Truncate` or `Throw`. `Default` resolves per type: **strings truncate, numeric types throw** — so an out-of-range number is never written blank silently.

## Culture handling

Pass an `IFormatProvider` to `TryParse`, the reader constructor, or the write methods. The generic path (`ISpanParsable`/`ISpanFormattable`, e.g. `decimal`) and the `double`/`float` processors all honor it (the decimal separator is derived from the culture). When the provider is `null`, `'.'` is used.

## StringPool (interning)

Pass a `CommunityToolkit.HighPerformance.Buffers.StringPool` to intern repeated string-column values, driving allocations toward zero:

```csharp
var pool = new StringPool();
var reader = new FixedWidthReader<Person>(CultureInfo.InvariantCulture, stringPool: pool);
```

This is a deliberate **time vs. memory** trade-off: pooling removes per-line string allocations but costs extra CPU (hashing + lookup). Prefer it for GC-sensitive / high-concurrency workloads; skip it for raw throughput.

## Validation

Invalid layouts fail fast at construction (`new FixedWidthParser<T>()` / `new FixedWidthWriter<T>()`) with an `InvalidOperationException`: negative `Start`, `Length < 1`, or overlapping columns. Adjacent columns (end of one == start of the next) are valid.

## Performance

Measured with BenchmarkDotNet (`MemoryDiagnoser`) on .NET 10. Highlights:

- **Parsing** a line is ~30 ns and allocates only the string column (~40–48 B); with a `StringPool` it is zero-alloc.
- **Reading** span-based vs. a naive `ReadLine()` + parse: faster and ~3× less memory; with a pool, allocations are a small constant regardless of line count.
- **Writing** with `StreamWriter` reuse (or a `ReadOnlySpan<T>`) is zero-alloc per line.

Run them yourself:

```bash
dotnet run -c Release --project tests/Benchmarks/Benchmarks.csproj -- --filter "*ReaderBenchmarks*"
```

Reports (including full JSON for cross-commit comparison) are written to `tests/Benchmarks/BenchmarkDotNet.Artifacts/results`.

## `ref struct` models

The parser accepts `ref struct` models (`where TModel : new(), allows ref struct`), useful for stack-only, zero-heap row processing:

```csharp
public ref struct Row
{
    public Row() { Name = string.Empty; Age = 0; }
    [FixedColumn(0, 10)] public string Name { get; set; }
    [FixedColumn(10, 5)] public int Age { get; set; }
}

var parser = new FixedWidthParser<Row>();
parser.TryParse(line, CultureInfo.InvariantCulture, null, out var row);
```

(The batch readers and the writer use a regular `where TModel : new()` constraint, since `IEnumerable<T>` cannot carry a `ref struct`.)

## Project layout

```
src/FixedWidthParser/            The library
tests/FixedWidthParser.Tests/    xUnit test suite
tests/Benchmarks/                BenchmarkDotNet benchmarks
```

## Building and testing

```bash
dotnet build Benchmarks.slnx -c Release
dotnet test  tests/FixedWidthParser.Tests/FixedWidthParser.Tests.csproj
```
