from __future__ import annotations

import gc
import gzip
import io

import numpy as np
import pytest

import fixedwidth as fw
from conftest import CONTENT, layout

DATA = CONTENT.encode("utf-8")


class Trickle:
    """A stream with only read(), handing out a few bytes per call, that can fail partway."""

    def __init__(self, data: bytes, chunk: int = 3, fail_at: int = -1) -> None:
        self.data, self.chunk, self.fail_at, self.position = data, chunk, fail_at, 0

    def read(self, size: int = -1) -> bytes:
        if 0 <= self.fail_at <= self.position:
            raise ConnectionError("the network went away")
        size = len(self.data) if size < 0 else min(size, self.chunk)
        out = self.data[self.position : self.position + size]
        self.position += len(out)
        return out


def codes(tables) -> list[str]:
    return [code for table in tables for code in table["codneg"]]


@pytest.mark.parametrize("make", [lambda: io.BufferedReader(io.BytesIO(DATA)), lambda: Trickle(DATA)])
def test_read_batches_from_a_stream_equals_memory(options, make):
    reader = fw.ReaderOptions(read=options, batch_rows=2)
    expected = list(fw.read_batches(DATA, layout(), reader))

    batches = list(fw.read_batches(make(), layout(), reader))

    assert [b.num_rows for b in batches] == [b.num_rows for b in expected]
    assert codes(batches) == codes(expected) == ["PETR4", "VALE3", "ITUB4"]
    assert np.array_equal(np.concatenate([b["quatot"] for b in batches]), np.concatenate([b["quatot"] for b in expected]))


def test_read_batches_from_gzip(tmp_path, options):
    path = tmp_path / "cotahist.txt.gz"
    with gzip.open(path, "wb") as out:
        out.write(DATA)

    with gzip.open(path, "rb") as stream:
        batches = list(fw.read_batches(stream, layout(), fw.ReaderOptions(read=options)))

    assert codes(batches) == ["PETR4", "VALE3", "ITUB4"]


def test_whole_read_from_a_stream_equals_memory(options):
    table = fw.read(Trickle(DATA, chunk=1000), layout(), options)

    assert table["codneg"] == fw.read(DATA, layout(), options)["codneg"]
    assert table.lines_skipped == 3  # the header, the invalid line and the trailer


def test_bytesio_is_read_from_its_position(options):
    buffer = io.BytesIO(b"junk" + DATA)
    buffer.seek(4)

    assert fw.read(buffer, layout(), options)["codneg"] == ["PETR4", "VALE3", "ITUB4"]
    assert codes(fw.read_batches(buffer, layout(), fw.ReaderOptions(read=options))) == ["PETR4", "VALE3", "ITUB4"]


def test_a_failing_stream_reports_its_error(options):
    with pytest.raises(fw.FixedWidthError, match="the network went away"):
        list(fw.read_batches(Trickle(DATA, fail_at=80), layout(), fw.ReaderOptions(read=options, batch_rows=1)))


def test_parse_error_counts_the_header_lines():
    strict = fw.ReaderOptions(read=fw.ReadOptions(header_lines=1, degree_of_parallelism=1))

    with pytest.raises(fw.ParseError) as error:
        list(fw.read_batches(Trickle(DATA), layout(), strict))

    assert error.value.line_number == 5


def test_the_stream_is_released_when_the_reader_closes(options):
    from fixedwidth import reader as module

    batches = fw.read_batches(Trickle(DATA), layout(), fw.ReaderOptions(read=options, batch_rows=1))
    next(batches)
    assert len(module._live) == 1
    batches.close()
    gc.collect()

    assert module._live == {}


def test_arrow_stream_from_a_stream(options):
    pl = pytest.importorskip("polars")

    frame = pl.DataFrame(fw.read_arrow_stream(Trickle(DATA), layout(), fw.ReaderOptions(read=options, batch_rows=2)))

    assert frame["codneg"].to_list() == ["PETR4", "VALE3", "ITUB4"]
