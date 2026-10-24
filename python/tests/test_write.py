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


def test_write_decimal_with_more_digits_than_the_scale_raises():
    import decimal

    columns = fw.Layout([fw.Column("v", 0, 8, fw.Decimal, scale=2)])
    exact = pa.table({"v": pa.array([decimal.Decimal("1.23"), decimal.Decimal("-0.5")], type=pa.decimal128(10, 3))})
    lossy = pa.table({"v": pa.array([decimal.Decimal("1.239")], type=pa.decimal128(10, 3))})

    assert fw.write_bytes(exact, columns) == b"     123\n     -50\n"
    with pytest.raises(fw.FixedWidthError, match="1.239"):
        fw.write_bytes(lossy, columns)


def test_write_decimal_from_integers_is_exact():
    columns = fw.Layout([fw.Column("v", 0, 20, fw.Decimal, scale=1)])
    data = pa.table({"v": pa.array([9007199254740991, None, -3], type=pa.int64())})

    assert fw.write_bytes(data, columns) == b"   90071992547409910\n                    \n                 -30\n"


@pytest.mark.parametrize(
    "values",
    [
        pa.array([1e30]),
        pa.array([float("nan")]),
        pa.array([float("-inf")]),
        pa.array([2**62], type=pa.int64()),
        pa.array([2**64 - 1], type=pa.uint64()),
        pa.array([10**17], type=pa.decimal128(38, 0)),
    ],
)
def test_write_decimal_out_of_range_or_not_finite_raises(values):
    columns = fw.Layout([fw.Column("v", 0, 20, fw.Decimal, scale=2)])

    with pytest.raises(fw.FixedWidthError, match="column 'v'"):
        fw.write_bytes(pa.table({"v": values}), columns)


def test_write_decimal_from_floats_rounds_half_to_even_to_the_scale():
    columns = fw.Layout([fw.Column("v", 0, 6, fw.Decimal, scale=2)])
    data = pa.table({"v": pa.array([1.234, 0.125, -2.5, None])})

    assert fw.write_bytes(data, columns) == b"   123\n    12\n  -250\n      \n"


def test_read_invalid_utf8_reaches_polars_and_pandas_as_valid_text():
    columns = fw.Layout([fw.Column("name", 0, 6, fw.String), fw.Column("n", 6, 2, fw.Int64)])
    data = "AÇÃO  01\n".encode("latin-1")

    assert fw.read_arrow(data, columns).validate(full=True) is None
    assert fw.read_polars(data, columns)["name"].to_list()[0].startswith("A")
    assert fw.read_pandas(data, columns)["name"][0].startswith("A")


def test_write_mismatched_columns_raise():
    columns = fw.Layout([fw.Column("a", 0, 2, fw.String), fw.Column("b", 2, 2, fw.Int64)])

    with pytest.raises(fw.FixedWidthError, match="b"):
        fw.write_bytes(pa.table({"a": ["x"]}), columns)
