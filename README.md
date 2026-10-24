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
- **`ref struct` model support** on single-line parsing and formatting, on every reader and on parallel aggregation.
- **Python and C/C++** through a NativeAOT library with a C ABI: columnar and Arrow reads, parallel reads and writes. See [Python and C/C++](#python-and-cc).

## Requirements

- `net10.0`.

Dependencies: [CommunityToolkit.HighPerformance](https://www.nuget.org/packages/CommunityToolkit.HighPerformance) (`StringPool`) and [csFastFloat](https://www.nuget.org/packages/csFastFloat) (fast `double`/`float` parsing).

## Installation

```bash
dotnet add package FixedWidthParser.NET
```

The package ships the Roslyn source generator bundled as an analyzer. Models that implement `IFixedWidthModel<TSelf>` get a generated parser and writer automatically; no extra package or setup is required.

## Quick Start

Describe a line with `[FixedColumn(start, length)]`, both in bytes, then read and write through the
model's layout:

```csharp
using FixedWidthParser;

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

var layout = FixedWidthLayout.FromAttributes<Person>();

foreach (var person in FixedWidth.ReadFile("people.txt", layout))
{
    // ...
}

var people = new[] { new Person { Name = "John Doe", Age = 30, Salary = 60000 } };
using var stream = File.Create("out.txt");
FixedWidth.WriteMany(stream, people, layout);
```

Make the model `partial` and implement `IFixedWidthModel<Person>` to have the generator write its
parser and writer instead; the generated overloads take no layout: `FixedWidth.ReadFile<Person>("people.txt")`.

## Guide

- [Models and layouts](docs/guide/models.md) — attribute and generated models, fluent layouts for types you
  cannot annotate, formatting options, column types, encodings and validation.
- [Reading](docs/guide/reading.md) — files and streams, async, parallel aggregation, options, errors,
  untrusted input and single lines.
- [Writing](docs/guide/writing.md) — records and batches, synchronous and asynchronous, and formatting into
  your own buffer.
- [Versioning](docs/guide/versioning.md) — what a minor version may change, and the `Infrastructure` namespace.
- [Benchmarks](docs/performance/benchmarks.md) — reading and writing against FileHelpers, FlatFiles and
  RecordParser. [Live results](https://gabrielmarquezmatte.github.io/FixedWidthParser/dev/bench/).

## Python and C/C++

`src/FixedWidthParser.Native` publishes the library as a NativeAOT shared library with a C ABI
(`include/fixedwidth.h`), so Python and C/C++ read and write fixed-width files without a .NET runtime. The
caller describes the columns at run time; a read fills one native buffer per column, or Arrow arrays, in
parallel by byte range.

```python
import fixedwidth as fw

layout = fw.Layout([
    fw.Column("codneg", 12, 12, fw.String),
    fw.Column("preabe", 56, 13, fw.Float64, scale=2),   # B3 N(11)V99: two implied decimals
])
df = fw.read_pandas("COTAHIST_A2023.TXT", layout, fw.ReadOptions(header_lines=1, skip_invalid=True))
```

On a 1,000,000-line COTAHIST-shaped file, `fw.read_pandas` is 52 times faster than `pandas.read_fwf`
([measurements](docs/python-benchmarks.md)). The [Python README](python/README.md), the
[C++ README](cpp/README.md) and the [native library README](src/FixedWidthParser.Native/README.md) cover
the rest.

## Project Layout

```text
src/FixedWidthParser/                  The library
src/FixedWidthParser.Generator/        Roslyn source generator
src/FixedWidthParser.Native/           NativeAOT C ABI, its C and C++ headers
python/                                The fixedwidth Python package
cpp/                                   CMake target and tests for the C++ wrapper
tests/FixedWidthParser.Tests/          Runtime, reader, writer, parity and native-layer tests
tests/FixedWidthParser.Generator.Tests/Source generator tests
tests/FixedWidthParser.NativeSmoke/    C consumer of the native library
tests/Benchmarks/                      BenchmarkDotNet benchmarks
tests/AotSmoke/                        Fluent layout published with NativeAOT
docs/                                  Usage guide and the measurements behind design decisions
```

[ARCHITECTURE.md](ARCHITECTURE.md) maps the code; [CONTRIBUTING.md](CONTRIBUTING.md) and
[STYLEGUIDE.md](STYLEGUIDE.md) cover how to change it.

## Building and Testing

```bash
dotnet build FixedWidthParser.slnx -c Release
dotnet test --solution FixedWidthParser.slnx -c Release
```
