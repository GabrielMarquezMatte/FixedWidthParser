# fixedwidthparser-native

Read and write fixed-width files from Python through FixedWidthParser's NativeAOT library: no .NET runtime,
parallel reads, results as NumPy views, Arrow, pandas or polars.

```python
import fixedwidth as fw

# B3's COTAHIST quote history: 245-byte lines, a header (00) and a trailer (99) around the quotes (01).
layout = fw.Layout([
    fw.Column("tipreg", 0, 2, fw.String),
    fw.Column("data_pregao", 2, 8, fw.Date, format="yyyyMMdd"),
    fw.Column("codneg", 12, 12, fw.String),
    fw.Column("preabe", 56, 13, fw.Float64, scale=2),   # N(11)V99: two implied decimals
    fw.Column("quatot", 152, 18, fw.Int64),
])
options = fw.ReadOptions(header_lines=1, skip_invalid=True)   # the trailer fails to parse and is skipped

df = fw.read_pandas("COTAHIST_A2023.TXT", layout, options)
```

## Reading

| Function | Returns |
|---|---|
| `fw.read(source, layout, options)` | `fw.Table`: NumPy views over the native columns, no copy |
| `fw.read_arrow(source, layout, options)` | `pyarrow.Table` (needs pyarrow) |
| `fw.read_pandas(source, layout, options)` | `pandas.DataFrame` (needs pyarrow and pandas) |
| `fw.read_polars(source, layout, options)` | `polars.DataFrame` |
| `fw.read_batches(source, layout, fw.ReaderOptions(batch_rows=...))` | an iterator of `fw.Table` |
| `fw.read_arrow_stream(source, layout, fw.ReaderOptions(...))` | an Arrow PyCapsule stream for `polars.DataFrame(...)` or `pyarrow.RecordBatchReader.from_stream(...)` |

`source` is a path, or `bytes`/`bytearray`/`memoryview` to read from memory. Every optional setting lives in
`fw.ReadOptions`: `header_lines`, `skip_invalid`, `degree_of_parallelism` (0 = one thread per processor,
1 = sequential), `encoding` (a code page or `"latin-1"`, `"cp1252"`, ...), `max_line_length` and
`skip_byte_order_mark`. A parallel read gives the same rows, in the same order, as a sequential one.

A line that fails to parse raises `fw.ParseError` with `line_number` and `column`, unless `skip_invalid` is set.

## Columns

`fw.Column(name, start, length, type, *, nullable, scale, format, trim_char, trim_mode, alignment, padding, overflow)`.
Positions are bytes. Types: `fw.String`, `fw.Int64`, `fw.Float64`, `fw.Decimal` (int64 unscaled, Arrow
`decimal128(18, scale)`), `fw.Date`, `fw.Time`, `fw.Timestamp`. `scale` is the number of implied decimals:
`0000000123456` with `scale=2` reads as `1234.56`. `format` uses .NET date syntax (`yyyyMMdd`, `HHmmss`).

## Writing

```python
fw.write(df, "out.txt", layout, fw.WriteOptions(degree_of_parallelism=0))
data = fw.write_bytes(df, layout)
```

Takes pandas, polars, pyarrow or anything with `__arrow_c_stream__` (needs pyarrow), matching columns by name.
`padding="0"` writes zero-padded numbers with the sign leftmost; a null writes as its column filled with
`trim_char`. Parallel writes are byte-identical to serial ones.

## Building from source

    python python/scripts/build_native.py      # publishes the library into src/fixedwidth/_lib
    pip install -e "python[dev,pandas,polars]"
    pytest python/tests
