# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- `System.IO.Pipelines` integration on the UTF-8 byte path: `FixedWidthByteReader<T>.ReadAsync(PipeReader)`
  (reflection) and `FixedWidthUtf8.ReadAsync<T>(PipeReader)` (source-generated) stream records straight
  off a `PipeReader` via `await foreach`. Lines are sliced from the `ReadOnlySequence<byte>` and parsed in
  place when contiguous, copied into a pooled scratch buffer only when they span segments. Intended for
  sources that are already pipes (Kestrel request bodies, sockets, upstream pipeline stages); for plain
  files/streams the existing `Stream` overloads remain the faster default (see `PipeReaderBenchmarks`).
- `FixedColumnAttribute.Converter`: a per-property custom converter (`IFixedWidthConverter<T>` for the
  `char` path, `IUtf8FixedWidthConverter<T>` for the UTF-8 path — one type can implement both) that
  takes priority over the built-in `ISpanParsable`/`IUtf8SpanParsable` fallback, for both parsing and
  writing. Wired into reflection (parse + write, both element types) and the source generator (parse,
  both element types), with build-time diagnostics `FWP008`/`FWP009` when the converter doesn't
  implement the interface the column's type requires.
- Nullable value-type columns (`int?`, `decimal?`, `DateTime?`, …): a blank (trimmed-empty) column
  parses to `null` without invoking the underlying parser/converter, and `null` writes as a blank
  (padding-filled) column. Works across reflection and generated, char and UTF-8, and composes with
  `FixedColumnAttribute.Converter` (the converter always targets the non-nullable `T`).

### Changed
- **Breaking:** the internal column formatters `StringColumnFormatter<TModel>` and
  `SpanFormattableColumnFormatter<TModel, TProperty>` are now `internal` (they were unintentionally
  `public`). They are implementation details resolved by the writer; the public extension point is the
  `IColumnFormatter<TModel>` interface. The reader/enumerator strategy types remain `public` because
  they appear in `GetAsyncEnumerator()` return types (a deliberate allocation-free design choice).

### Removed
- **Breaking:** `ColumnParserRegistry` and `Utf8ColumnParserRegistry` (and the `ColumnValueParser<T>`/
  `Utf8ColumnValueParser<T>` delegates they used) are gone. They were process-wide mutable state with a
  documented footgun (registration only took effect if it happened before a model's first parse) and no
  write-side counterpart; `FixedColumnAttribute.Converter` replaces them with a per-column, type-checked
  alternative that also covers writing. The built-in `double`/`float` fast path (csFastFloat) no longer
  goes through a registry lookup — it's now a direct type check in the parser factories, with no
  behavior change for consumers.

### Fixed
- UTF-8 byte parser: a culture whose decimal separator is not a single ASCII character (e.g. the
  Arabic decimal separator U+066B) previously had its separator silently truncated to the wrong
  byte, mis-parsing `double`/`float` columns. Such cultures now throw a clear `NotSupportedException`
  on the byte path; use the `char`-based parser for them. ASCII separators (`.`/`,`) are unaffected.
- Writer: a value whose `ISpanFormattable.TryFormat` never succeeded could overflow the formatter's
  buffer-growth counter into a negative/huge `ArrayPool` rent. The grow loop is now bounded (1M chars)
  and throws a clear `InvalidOperationException` naming the column instead.

### Documentation
- Corrected the UTF-8 byte path docs: `string` columns **are** interned through a supplied
  `StringPool` (identical to the `char` path); the previous "no pooling" note was inaccurate.
- Documented that empty lines are skipped (counted but not yielded) while a non-empty line shorter
  than the declared layout is treated as malformed and throws.

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
  layouts and unsupported column types. Shipped inside the package as an analyzer — no extra package
  required.

[Unreleased]: https://github.com/GabrielMarquezMatte/FixedWidthParser/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/GabrielMarquezMatte/FixedWidthParser/releases/tag/v1.0.0
