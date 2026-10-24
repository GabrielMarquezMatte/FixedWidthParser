from __future__ import annotations

import datetime as dt

import pandas as pd
import polars as pl
import pyarrow as pa
import pytest

import fixedwidth as fw
from conftest import CONTENT, layout

QUOTES = "".join(line + "\n" for line in CONTENT.splitlines()[1:4])


def test_write_roundtrip_pandas_polars_pyarrow(cotahist, options):
    frame = fw.read_pandas(cotahist, layout(), options)

    from_pandas = fw.write_bytes(frame, layout())
    from_polars = fw.write_bytes(pl.from_pandas(frame), layout())
    from_arrow = fw.write_bytes(pa.Table.from_pandas(frame, preserve_index=False), layout())

    assert from_pandas == from_polars == from_arrow
    assert from_pandas.decode("utf-8") == QUOTES


def test_write_bytes_equals_file(tmp_path, cotahist, options):
    frame = fw.read_arrow(cotahist, layout(), options)
    path = tmp_path / "out.txt"

    fw.write(frame, path, layout(), fw.WriteOptions(degree_of_parallelism=4, batch_rows=1))

    assert path.read_bytes() == fw.write_bytes(frame, layout())


def test_write_options():
    columns = fw.Layout([fw.Column("s", 0, 4, fw.String)], fw.LayoutOptions(line_length=6, filler="."))
    data = pa.table({"s": ["AÇÃO", None]})

    written = fw.write_bytes(data, columns, fw.WriteOptions(encoding="latin-1", newline="\r\n"))

    assert written == "AÇÃO..\r\n".encode("latin-1") + b"    ..\r\n"


def test_write_values_from_python_types():
    columns = fw.Layout(
        [
            fw.Column("d", 0, 8, fw.Date, format="yyyyMMdd"),
            fw.Column("p", 8, 8, fw.Float64, scale=2, padding="0"),
            fw.Column("q", 16, 4, fw.Int64),
        ]
    )
    frame = pd.DataFrame({"d": [dt.date(2023, 10, 24)], "p": [-12.5], "q": [7]})

    assert fw.write_bytes(frame, columns) == b"20231024-0001250   7\n"


def test_write_mismatched_columns_raise():
    columns = fw.Layout([fw.Column("a", 0, 2, fw.String), fw.Column("b", 2, 2, fw.Int64)])

    with pytest.raises(fw.FixedWidthError, match="b"):
        fw.write_bytes(pa.table({"a": ["x"]}), columns)
