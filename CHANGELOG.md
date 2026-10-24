# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0]

Not released yet.

2.0 reads and writes **bytes only**, where 1.x had a `char` and a UTF-8 version of each reader,
parser and writer, and goes through **one entry point**: every `FixedWidth` method comes in an
overload for generated models and one that takes a `FixedWidthLayout<T>`, built from the attributes or
fluently in code. The 1.x runtime classes are gone. Column positions are bytes; text columns are decoded with an ASCII-compatible encoding, UTF-8 by default. Settings travel
in options objects and a `FixedWidthContext`, so later versions can add to them without another break.
2.0 targets `net10.0` only.

### Migrating from 1.x

| 1.x | 2.0 |
|---|---|
| `FixedWidthReader<T>` over a `TextReader`, `FixedWidthByteReader<T>` | `FixedWidth.Read(stream, FixedWidthLayout.FromAttributes<T>(), …)` or `FixedWidth.ReadFile(path, FixedWidthLayout.FromAttributes<T>(), …)` |
| `FixedWidthUtf8.Read*`, `FixedWidth.Read*` over a `TextReader` | `FixedWidth.Read*` over a `Stream` or a file |
| `new FixedWidthReader<T>(provider, pool, bufferSize).Read(…)` | `FixedWidth.Read(…, FixedWidthLayout.FromAttributes<T>(), options: new FixedWidthReaderOptions { FormatProvider = …, StringPool = …, BufferSize = … })` |
| `FixedWidth.Read<T>(stream, encoding, leaveOpen, provider, pool, bufferSize)` | `FixedWidth.Read<T>(stream, leaveOpen, options)`; the encoding is `FixedWidthReaderOptions.Encoding` |
| `FixedWidthParser<T>`, `Utf8FixedWidthParser<T>` `.TryParse(line, provider, pool, out m)` | `FixedWidth.TryParse(ReadOnlySpan<byte> line, FixedWidthLayout.FromAttributes<T>(), in FixedWidthContext context, out m)` |
| `FixedWidth.TryParse<T>(chars, provider, pool, out m)`, `FixedWidthUtf8.TryParse<T>(bytes, …)` | `FixedWidth.TryParse<T>(bytes, in FixedWidthContext context, out m)` |
| `FixedWidth.TryFormat<T>(in m, Span<char>, provider, out n)` | `FixedWidth.TryFormat<T>(in m, Span<byte>, in FixedWidthContext context, out n)` |
| `IFixedWidthModel<T>` + `IUtf8FixedWidthModel<T>` | `IFixedWidthModel<T>`, whose generated members take bytes and a context |
| `IFixedWidthConverter<T>` + `IUtf8FixedWidthConverter<T>` with `IFormatProvider?` | `IFixedWidthConverter<T>` over bytes, with `in FixedWidthContext` |
| `FixedWidthWriter<T>.Write*(StreamWriter or Stream, …, provider)` | `FixedWidth.Write*(Stream, …, FixedWidthLayout.FromAttributes<T>(), new FixedWidthWriterOptions { FormatProvider = …, Encoding = …, NewLine = … })` |
| `catch (FormatException)` on a bad line | still works; the exception is now `FixedWidthParseException` |
| `using FixedWidthParser.Attributes;`, `using FixedWidthParser.Processors;` | `using FixedWidthParser;` — `FixedColumnAttribute`, `Alignment`, `TrimMode`, `OverflowBehavior` and `IFixedWidthConverter<T>` moved to the root namespace |
| `IFixedWidthConverter<T>.TryParse(field, …)`, `TryFormat(…, out int written)` | the same, with the parameters named `column` and `bytesWritten` |

A UTF-8 file with multi-byte text whose layout counts characters no longer lines up: positions are
bytes. Read files written in a single-byte encoding with that encoding (`Encoding.Latin1`, or
Windows-1252 after registering `CodePagesEncodingProvider`).

### Added
- `FixedWidthLayout<T>`: how a model maps to the columns of a line, for reading and writing, passed to
  the `FixedWidth` methods. `FixedWidthLayout.FromAttributes<T>()` builds it from `[FixedColumn]`
  attributes; `FixedWidthLayout.Build<T>(b => …)` describes it in code, binding each column with a
  `ColumnGetter`/`ColumnSetter` delegate and `FixedWidthColumnOptions`, with no reflection, so it works
  under trimming and NativeAOT. A layout can read only or write only.
- `FixedWidth.Write`, `WriteMany`, `WriteAsync` and `WriteManyAsync` for generated models, which had no
  stream writer.
- Parallel aggregation: `FixedWidth.AggregateFileAsync`, for generated models and layouts alike,
  reads a file in byte-range partitions on several threads and fold each into an
  `IRecordAccumulator<TSelf, TModel>`, merged in file order. Every line is parsed exactly once, and
  errors carry the same line number as a sequential read. `FixedWidthReaderOptions.DegreeOfParallelism`
  sets the thread count. `AggregateAsync` does the same over a `Stream`: one thread
  reads chunks, the others parse them.
- `FixedWidthReaderOptions`: `FormatProvider`, `Encoding`, `StringPool`, `BufferSize`,
  `MaxLineLength` and `OnParseError`, taken by every read. `FixedWidthWriterOptions`: `FormatProvider`,
  `Encoding` and `NewLine`.
- `FixedWidthContext`, passed by `in` reference to generated code and converters: the format provider,
  the encoding and the string pool for one line. New settings arrive as new properties.
- `FixedWidthParseException`, a `FormatException` carrying `LineNumber`, `ColumnName`, `ColumnStart`,
  `ColumnLength` and `RawValue`. A short line says how long it is and how long the layout needs it to
  be. A line that parses pays nothing for this: a failed line is parsed again to find the column.
- `FixedWidthReaderOptions.OnParseError`: return `true` to skip a line that fails to parse.
- `FixedWidthParser.Native`: the library published with NativeAOT as a shared library with a C ABI
  (`fixedwidth.h`), for languages without a .NET runtime. Layouts are described at run time
  (`fw_layout_create`), including implied decimals (`scale`) for vendor formats such as B3's COTAHIST.
  Reads fill a columnar `fw_table` or Arrow C Data Interface arrays and streams, in parallel by byte range,
  with `header_lines` and `skip_invalid`; writes take a columnar table and are byte-identical whatever the
  thread count. Not on NuGet.
- The `fixedwidth` Python package over it (NumPy views, pyarrow, pandas, polars), 52 times faster than
  `pandas.read_fwf` on a 1,000,000-line COTAHIST-shaped file, and a header-only C++23 wrapper
  (`fixedwidth.hpp`) that also maps structs to columns through `FixedWidthMapper<T>`.
- `ref struct` models on `Read`, `ReadFile`, `ReadAsync`, `ReadFileAsync`, `AggregateFileAsync` and
  `AggregateAsync`, generated and through a layout. The enumerators parse a line when `Current` is
  read, so reading it twice parses twice; with `OnParseError` set, each line is parsed once more to
  decide whether to skip it.
- `FixedWidthReaderOptions.MaxLineLength` (default 1 MiB): a longer line throws
  `InvalidDataException`. Before, a source without line breaks doubled the read buffer until memory ran
  out, which a request body made reachable from the network.
- Encodings other than UTF-8 for text columns, dates parsed with a format and types that parse only
  from characters. A UTF-8 byte order mark is skipped only when the encoding is UTF-8.
- Columns of any `ISpanParsable<T>` type without a UTF-8 parser, decoded first (they used to be
  refused on the byte path).
- `FixedWidth.TryFormat(in model, layout, Span<byte>, in FixedWidthContext, out int)`.
- `FixedColumnAttribute.Converter`: a per-property `IFixedWidthConverter<T>` that takes priority over the
  built-in parsing and formatting, with diagnostic `FWP008` when it targets the wrong type.
- Nullable value-type columns (`int?`, `decimal?`, `DateTime?`, …): a blank column parses to `null`
  without invoking the parser or converter, and `null` writes as a blank column.
- The source-generated writer: `IFixedWidthModel<TSelf>.TryFormat`, with the same alignment, padding,
  format, overflow, nullable and converter handling as the model's attribute layout, and diagnostic `FWP010`
  when a column's type cannot be written.
- `FixedColumnAttribute.TrimChar` and `TrimMode`: what to strip from a column when parsing, and from
  which side.
- `FixedColumnAttribute.Format` parses `DateTime`, `DateOnly`, `TimeOnly` and `DateTimeOffset` with
  that exact format.
- Diagnostic `FWP012`: `Padding` or `TrimChar` is not ASCII. Layouts throw
  `InvalidOperationException` for the same models.
- Public API tracking (`PublicAPI.*.txt`) and package validation against the last release, run by CI on
  every pull request.

### Changed
- **Breaking:** bytes only; see the table above. The column positions of a model are bytes.
- **Breaking:** `FixedColumnAttribute`, `Alignment`, `TrimMode`, `OverflowBehavior` and
  `IFixedWidthConverter<T>` moved to the `FixedWidthParser` namespace. The types public only for
  generated code or allocation-free enumeration (`FixedWidthRuntime`, `ColumnFormatOptions`, the new
  `ColumnParseOptions`, the record enumerables and enumerators, `IRecordLineParser<T>` and the
  line-parser structs) moved to `FixedWidthParser.Infrastructure` and are hidden from IntelliSense.
- **Breaking:** without `OnParseError`, a line that fails to parse throws when the enumerator's
  `Current` is read, not from `MoveNext`. A `foreach` sees no difference. The non-generic
  `IEnumerator.Current` throws `NotSupportedException`.
- **Breaking:** targets `net10.0` only. .NET 8 reaches end of support on 10 November 2026.
- **Breaking:** a `null` format provider means the invariant culture for every column type, on parse and
  write. 1.x used it for `double`/`float` only and fell back to `CurrentCulture` for the rest.
- A culture whose decimal separator is not one byte (U+066B, for example) now parses, where 1.1 threw
  `NotSupportedException` on the byte path.
- File-backed readers open the `FileStream` with `FileOptions.SequentialScan` and no internal buffer of
  their own, since the reader already buffers. The default buffer is 65,536 bytes.
- The writer batches records into one pooled buffer and writes bytes straight to the stream, instead of
  going through a `StreamWriter`.
- The writer formats `IUtf8SpanFormattable` columns (every BCL number and date) straight into the line,
  and transcodes left-aligned string columns straight into it when the encoding is UTF-8. A formatted
  value that is not ASCII under another encoding still goes through that encoding.
- A `double` column with no `Format`, at most four decimals and a magnitude under 1e9 is written as a
  scaled integer, about 5x faster, with the same bytes. A line of a string, an `int` and a `double`
  writes about 2x faster.
- The generator targets `netstandard2.0`, so Visual Studio's .NET Framework compiler host can load it.
- `double`, `float` and `decimal` columns parse through a built-in fast path (ExcelReader's `FastDouble`
  and `FastDecimal`) under any culture whose decimal separator is one ASCII character and whose
  negative sign is `-`, not only `'.'`: a pt-BR read is about 40% faster
  ([docs/reader-performance.md](docs/reader-performance.md)). Anything the fast path refuses goes to
  the BCL parser, which decides the result.
- **Breaking:** the csFastFloat dependency is gone.
- Dates and times with a format made only of fixed-width digit fields (`yyyyMMdd`, `yyyy-MM-dd`, `HHmmss`, …)
  and `int`/`long` columns of plain digits parse straight from the bytes, about 5x and 1.5x faster; any
  other text still goes to the BCL. A single-threaded B3 COTAHIST read through Python is 26% faster
  ([docs/python-benchmarks.md](docs/python-benchmarks.md)).
- Native and Python whole-file reads size their column buffers once per partition, skip bad lines under
  `skip_invalid` without numbering them, and concatenate the partitions' columns in parallel: a parallel
  COTAHIST read is about 32% faster.
- Writing: right-aligned `int`/`long` columns without a format put their digits straight into the line,
  digit-only date and time formats reuse the reader's fast path, and `fw_write_file` opens its file for
  asynchronous I/O so parallel batches write at once. A COTAHIST write through Python is about 37% faster.
- Reading with a `StringPool` checks a small per-thread cache before the pool, about halving the cost of
  a pooled read.
- The library, generator and native library build with warnings as errors.

### Removed
- **Breaking:** `FixedWidthParser<T>`, `FixedWidthReader<T>` and `FixedWidthWriter<T>`. Pass
  `FixedWidthLayout.FromAttributes<T>()` to the matching `FixedWidth` method instead (see the table above); the layout is
  built once per type and shared.
- **Breaking:** reading from a `PipeReader` (`ReadAsync(PipeReader)` and its enumerator types). Pass
  `pipeReader.AsStream()` to the `Stream` overloads instead; it reads twice as fast as the dedicated
  path did ([docs/pipe-reader.md](docs/pipe-reader.md)).
- **Breaking:** `FixedWidthUtf8`, `FixedWidthByteReader<T>`, `Utf8FixedWidthParser<T>`,
  `IUtf8FixedWidthModel<T>`, `IUtf8FixedWidthConverter<T>`, `Utf8FixedWidthRuntime` and the `TextReader`
  overloads. Diagnostics `FWP007` and `FWP009`, which only applied to the second path, are retired.
- **Breaking:** `ColumnParserRegistry` and `Utf8ColumnParserRegistry` (and their value-parser delegates):
  process-wide mutable state that only took effect before a model's first parse.
  `FixedColumnAttribute.Converter` replaces them.
- **Breaking:** types that were public without a public use: `ColumnParser<T>`, `Utf8ColumnParser<T>`,
  `RefAction`, `RefGetter`, `IColumnFormatter<T>`, `ColumnLayoutValidator`, `ISource<T>`, `TextReaderSource`, `StreamSource`,
  `ILineFormat<T>` and the line-format structs. The enumerator and strategy types that stay public
  (they appear in return types, so `foreach` allocates nothing) lost their element-type and source
  type parameters.

### Fixed
- Right-aligned `double`/`float` columns could fail to parse under the invariant fast path: csFastFloat
  did not consistently count skipped leading whitespace.
- `double`/`float` columns with a thousands separator under a `'.'` culture (`"1,234.56"`) failed
  instead of parsing.
- `double`/`float` columns under a `','` culture could silently return a truncated value: `"1.234,50"`
  became `1.0`. The fast path is now trusted only when it consumes the whole field.
- A generated model skipped `[FixedColumn]` members declared on a base class: it left them at their
  default when reading and wrote filler over their columns. Inherited columns are now generated as
  reflection maps them, with an override or a `new` member replacing the one it hides.
- The decimal-separator cache thrashed when two format providers alternated.

## [1.1.1]

### Added
- Targets `net8.0` alongside `net10.0`. `ref struct` models need .NET 9 or later, so they are
  `net10.0`-only.

## [1.1.0]

### Added
- `System.IO.Pipelines` integration on the UTF-8 byte path: `FixedWidthByteReader<T>.ReadAsync(PipeReader)`
  and `FixedWidthUtf8.ReadAsync<T>(PipeReader)` stream records off a `PipeReader` via `await foreach`.

### Changed
- **Breaking:** the column formatters `StringColumnFormatter<TModel>` and
  `SpanFormattableColumnFormatter<TModel, TProperty>` became `internal`; they were unintentionally
  `public`.

### Fixed
- UTF-8 byte parser: a culture whose decimal separator is not a single ASCII character had its separator
  silently truncated to the wrong byte; it now throws `NotSupportedException`.
- Writer: a value whose `ISpanFormattable.TryFormat` never succeeded could overflow the buffer-growth
  counter into a negative rent; the loop is bounded and throws a clear error naming the column.

### Documentation
- `string` columns on the UTF-8 path are interned through a supplied `StringPool`, like the char path.
- Empty lines are skipped (counted but not yielded); a non-empty line shorter than the layout throws.

## [1.0.0]

Initial release.

### Added
- Attribute-driven column mapping (`[FixedColumn(start, length)]`) on properties and public fields.
- Parsing: single line plus lazy batch reading from a `TextReader`, `Stream` or file —
  synchronous (`IEnumerable<T>` with an allocation-free struct enumerator) and asynchronous
  (`IAsyncEnumerable<T>`), without allocating a string per line.
- Writing: single record and batches, synchronous and asynchronous, with `StreamWriter` reuse and
  `ReadOnlySpan<T>` overloads for zero-allocation output.
- Configurable per-column formatting: alignment, padding character, format string and an explicit
  overflow policy (no silent data loss).
- Culture-aware parsing/formatting for numeric and `ISpanParsable`/`ISpanFormattable` types
  (including fast `double`/`float` via csFastFloat).
- Layout validation at construction: rejects negative `Start`, non-positive `Length` and
  overlapping columns with a clear error.
- `ref struct` model support on the parser.
- A bundled Roslyn source generator: models implementing `IFixedWidthModel<TSelf>` get a
  reflection-free `TryParse` generated at compile time, with diagnostics (FWP001-FWP007) for invalid
  layouts and unsupported column types.

[2.0.0]: https://github.com/GabrielMarquezMatte/FixedWidthParser/compare/v1.1.1...HEAD
[1.1.1]: https://github.com/GabrielMarquezMatte/FixedWidthParser/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/GabrielMarquezMatte/FixedWidthParser/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/GabrielMarquezMatte/FixedWidthParser/releases/tag/v1.0.0
