from __future__ import annotations

import datetime as dt

import polars as pl
import pyarrow as pa

import fixedwidth as fw
from conftest import CONTENT, layout


def test_read_arrow(cotahist, options):
    table = fw.read_arrow(cotahist, layout(), options)

    assert table.schema.field("preabe").type == pa.float64()
    assert table.schema.field("data").type == pa.date32()
    assert table.schema.field("quatot").nullable
    assert table.column("codneg").to_pylist() == ["PETR4", "VALE3", "ITUB4"]
    assert table.column("data").to_pylist()[2] == dt.date(2023, 10, 25)


def test_read_arrow_decimal():
    columns = fw.Layout([fw.Column("v", 0, 8, fw.Decimal, scale=2)])

    table = fw.read_arrow(b"00012345\n", columns)

    assert table.schema.field("v").type == pa.decimal128(18, 2)
    assert str(table.column("v")[0].as_py()) == "123.45"


def test_arrow_stream_to_pyarrow_and_polars(cotahist, options):
    reader_options = fw.ReaderOptions(read=options, batch_rows=2)

    batches = list(pa.RecordBatchReader.from_stream(fw.read_arrow_stream(cotahist, layout(), reader_options)))
    frame = pl.DataFrame(fw.read_arrow_stream(CONTENT.encode("utf-8"), layout(), reader_options))

    assert [b.num_rows for b in batches] == [2, 1]
    assert frame["codneg"].to_list() == ["PETR4", "VALE3", "ITUB4"]


def test_arrow_stream_error_surfaces(options):
    columns = fw.Layout([fw.Column("n", 0, 2, fw.Int64)])
    stream = fw.read_arrow_stream(b"01\nXX\n", columns, fw.ReaderOptions(batch_rows=1))

    reader = pa.RecordBatchReader.from_stream(stream)
    reader.read_next_batch()
    try:
        reader.read_next_batch()
        raise AssertionError("expected an error")
    except OSError as error:
        assert "Line 2" in str(error)


def test_table_to_arrow(cotahist, options):
    table = fw.read(cotahist, layout(), options)

    assert table.to_arrow().column("preabe").to_pylist() == [1234.56, 678.9, 43.21]
