"""Read and write fixed-width files through the FixedWidthParser native library.

    import fixedwidth as fw

    layout = fw.Layout([
        fw.Column("codneg", 12, 12, fw.String),
        fw.Column("preabe", 56, 13, fw.Float64, scale=2),
    ])
    df = fw.read_pandas("COTAHIST_A2023.TXT", layout, fw.ReadOptions(header_lines=1, skip_invalid=True))
"""

from fixedwidth.arrow_stream import ArrowStream
from fixedwidth.layout import Layout
from fixedwidth.reader import read, read_arrow, read_arrow_stream, read_batches, read_pandas, read_polars
from fixedwidth.table import Table
from fixedwidth.types import (
    Alignment,
    Column,
    ColumnType,
    FixedWidthError,
    LayoutOptions,
    Overflow,
    ParseError,
    ReaderOptions,
    ReadOptions,
    TrimMode,
    WriteOptions,
)
from fixedwidth.writer import write, write_bytes

String = ColumnType.STRING
Int64 = ColumnType.INT64
Float64 = ColumnType.FLOAT64
Decimal = ColumnType.DECIMAL
Date = ColumnType.DATE
Time = ColumnType.TIME
Timestamp = ColumnType.TIMESTAMP

__all__ = [
    "Alignment",
    "ArrowStream",
    "Column",
    "ColumnType",
    "Date",
    "Decimal",
    "FixedWidthError",
    "Float64",
    "Int64",
    "Layout",
    "LayoutOptions",
    "Overflow",
    "ParseError",
    "ReadOptions",
    "ReaderOptions",
    "String",
    "Table",
    "Time",
    "Timestamp",
    "TrimMode",
    "WriteOptions",
    "read",
    "read_arrow",
    "read_arrow_stream",
    "read_batches",
    "read_pandas",
    "read_polars",
    "write",
    "write_bytes",
]
