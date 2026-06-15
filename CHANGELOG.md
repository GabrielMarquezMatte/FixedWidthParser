# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- **Breaking:** the internal column formatters `StringColumnFormatter<TModel>` and
  `SpanFormattableColumnFormatter<TModel, TProperty>` are now `internal` (they were unintentionally
  `public`). They are implementation details resolved by the writer; the public extension point is the
  `IColumnFormatter<TModel>` interface. The reader/enumerator strategy types remain `public` because
  they appear in `GetAsyncEnumerator()` return types (a deliberate allocation-free design choice).

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
- Expanded the `ColumnParserRegistry` / `Utf8ColumnParserRegistry` lifecycle docs: registration must
  happen before a model is first parsed (parsers are cached per model in a static constructor, so later
  `Register`/`Unregister` calls do not affect already-built parsers), and the registries are individually
  thread-safe (backed by a `ConcurrentDictionary`).

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
