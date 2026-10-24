from __future__ import annotations

import datetime as dt
import gc
import io

import numpy as np
import pandas as pd
import pytest

import fixedwidth as fw
from conftest import CONTENT, layout


def test_read_every_type(cotahist, options):
    table = fw.read(cotahist, layout(), options)

    assert table.num_rows == 3
    assert table.column_names == ["tipreg", "data", "codneg", "nomres", "preabe", "quatot"]
    assert table["codneg"] == ["PETR4", "VALE3", "ITUB4"]
    assert table["nomres"][2] == "ITAU UNI PN"
    assert table["preabe"].tolist() == [1234.56, 678.9, 43.21]
    assert table["quatot"].dtype == np.int64
    assert table.validity("quatot").tolist() == [True, False, True]
    assert table.validity("preabe") is None
    days = (dt.date(2023, 10, 24) - dt.date(1970, 1, 1)).days
    assert table["data"].tolist() == [days, days, days + 1]


def test_read_options_header_and_skip(cotahist):
    table = fw.read(cotahist, layout(), fw.ReadOptions(header_lines=1, skip_invalid=True))

    assert table.num_rows == 3
    assert table.lines_skipped == 3


def test_parse_error_has_line_and_column(cotahist):
    with pytest.raises(fw.ParseError) as error:
        fw.read(cotahist, layout(), fw.ReadOptions(header_lines=1))

    assert error.value.line_number == 5
    assert error.value.column == 4
    assert "ABCDE" in str(error.value)


def test_read_memory_equals_file(cotahist, options):
    from_file = fw.read(cotahist, layout(), options)
    from_bytes = fw.read(CONTENT.encode("utf-8"), layout(), options)
    from_view = fw.read(memoryview(bytearray(CONTENT.encode("utf-8"))), layout(), options)

    for name in from_file.column_names:
        assert list(from_bytes[name]) == list(from_file[name])
        assert list(from_view[name]) == list(from_file[name])


def test_parallel_equals_sequential(tmp_path):
    body = "".join(f"{i:06d}{i * 7 % 1000:04d}\n" for i in range(20000))
    path = tmp_path / "big.txt"
    path.write_text(body)
    columns = fw.Layout([fw.Column("id", 0, 6, fw.Int64), fw.Column("n", 6, 4, fw.Float64, scale=1)])

    sequential = fw.read(path, columns, fw.ReadOptions(degree_of_parallelism=1))
    parallel = fw.read(path, columns, fw.ReadOptions(degree_of_parallelism=4))

    assert np.array_equal(sequential["id"], parallel["id"])
    assert np.array_equal(sequential["n"], parallel["n"])
    assert parallel.num_rows == 20000


def test_views_keep_table_alive(cotahist, options):
    table = fw.read(cotahist, layout(), options)
    prices = table["preabe"]
    collected = []
    table._owner._on_free = collected.append

    del table
    gc.collect()
    assert prices.tolist() == [1234.56, 678.9, 43.21]
    assert collected == []

    del prices
    gc.collect()
    assert collected == [True]


def test_read_pandas_matches_read_fwf(cotahist, options):
    frame = fw.read_pandas(cotahist, layout(), options)

    expected = pd.read_fwf(
        io.StringIO(CONTENT),
        colspecs=[(0, 2), (2, 10), (10, 22), (22, 34), (34, 47), (47, 65)],
        names=["tipreg", "data", "codneg", "nomres", "preabe", "quatot"],
        dtype={"tipreg": str},
        skiprows=1,
        skipfooter=2,
        engine="python",
    )
    expected = expected[expected["codneg"] != "XXXXX"].reset_index(drop=True)
    assert frame["codneg"].tolist() == expected["codneg"].tolist()
    assert frame["nomres"].tolist() == expected["nomres"].tolist()
    assert frame["preabe"].tolist() == (expected["preabe"] / 100).tolist()
    assert frame["quatot"].tolist()[0::2] == expected["quatot"].tolist()[0::2]
    assert pd.isna(frame["quatot"][1])
    assert frame["data"].tolist()[0] == dt.date(2023, 10, 24)


def test_read_polars(cotahist, options):
    frame = fw.read_polars(cotahist, layout(), options)

    assert frame.shape == (3, 6)
    assert frame["codneg"].to_list() == ["PETR4", "VALE3", "ITUB4"]
    assert frame["quatot"].null_count() == 1


def test_read_batches(cotahist, options):
    batches = list(fw.read_batches(cotahist, layout(), fw.ReaderOptions(read=options, batch_rows=2)))

    assert [b.num_rows for b in batches] == [2, 1]
    assert batches[0]["codneg"] + batches[1]["codneg"] == ["PETR4", "VALE3", "ITUB4"]


def test_read_latin1(tmp_path):
    path = tmp_path / "latin1.txt"
    path.write_bytes("AÇÃO  01\n".encode("latin-1"))
    columns = fw.Layout([fw.Column("name", 0, 6, fw.String), fw.Column("n", 6, 2, fw.Int64)])

    assert fw.read(path, columns, fw.ReadOptions(encoding="latin-1"))["name"] == ["AÇÃO"]
