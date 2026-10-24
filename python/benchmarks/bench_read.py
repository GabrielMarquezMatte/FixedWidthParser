"""Reads a synthetic COTAHIST file with fixedwidth and with pandas.read_fwf, checks they agree, and times them.

    python python/benchmarks/bench_read.py [--lines 1000000] [--runs 3] [--fwf-runs 1]
"""

from __future__ import annotations

import argparse
import platform
import sys
import tempfile
import time
from pathlib import Path

import numpy as np
import pandas as pd
import polars as pl

import fixedwidth as fw

sys.path.insert(0, str(Path(__file__).parent))
import cotahist  # noqa: E402

STRING_FIELDS = [name for name, _, _, kind, _ in cotahist.FIELDS if kind is fw.String]


def read_fwf(path: Path) -> pd.DataFrame:
    frame = pd.read_fwf(
        path,
        colspecs=cotahist.colspecs(),
        names=[name for name, *_ in cotahist.FIELDS],
        dtype={name: str for name in STRING_FIELDS},
        skiprows=1,
        skipfooter=1,
        engine="python",
    )
    for name, _, _, kind, scale in cotahist.FIELDS:
        if kind is fw.Float64:
            frame[name] = frame[name] / 10**scale
        if kind is fw.Date:
            frame[name] = pd.to_datetime(frame[name].astype(str), format="%Y%m%d").dt.date
    return frame


def polars_slices(path: Path) -> pl.DataFrame:
    lines = pl.read_csv(path, has_header=False, separator="\x01", new_columns=["line"], quote_char=None, skip_rows=1)
    lines = lines.filter(pl.col("line").str.starts_with("01"))
    expressions = []
    for name, first, length, kind, scale in cotahist.FIELDS:
        text = pl.col("line").str.slice(first - 1, length).str.strip_chars()
        if kind is fw.String:
            expressions.append(text.alias(name))
        elif kind is fw.Date:
            expressions.append(text.str.to_date("%Y%m%d").alias(name))
        elif kind is fw.Float64:
            expressions.append((text.cast(pl.Int64) / 10**scale).alias(name))
        else:
            expressions.append(text.cast(pl.Int64).alias(name))
    return lines.select(expressions)


def check(native: pd.DataFrame, expected: pd.DataFrame, exact: bool = True) -> None:
    assert len(native) == len(expected), (len(native), len(expected))
    for name, _, _, kind, _ in cotahist.FIELDS:
        left, right = native[name], expected[name]
        if kind is fw.String:
            assert left.fillna("").tolist() == right.fillna("").tolist(), name
        elif kind is fw.Date:
            assert [pd.Timestamp(v).date() for v in left] == [pd.Timestamp(v).date() for v in right], name
        else:
            same = np.array_equal if exact else np.allclose
            assert same(left.to_numpy(dtype=float), right.to_numpy(dtype=float)), name


def best(runs: int, work) -> float:
    times = []
    for _ in range(runs):
        start = time.perf_counter()
        work()
        times.append(time.perf_counter() - start)
    return min(times)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lines", type=int, default=1_000_000)
    parser.add_argument("--runs", type=int, default=5)
    parser.add_argument("--fwf-runs", type=int, default=1, help="read_fwf is slow; one run is usually enough")
    args = parser.parse_args()

    layout = cotahist.layout()
    options = fw.ReadOptions(header_lines=1, skip_invalid=True)
    sequential = fw.ReadOptions(header_lines=1, skip_invalid=True, degree_of_parallelism=1)
    with tempfile.TemporaryDirectory() as directory:
        path = Path(directory) / "COTAHIST_SYNTHETIC.TXT"
        cotahist.generate(path, args.lines)
        size = path.stat().st_size / 2**20
        print(f"{args.lines:,} lines, {size:.0f} MiB, Python {platform.python_version()}, pandas {pd.__version__}, polars {pl.__version__}")

        expected = read_fwf(path)
        check(fw.read_pandas(path, layout, options), expected)
        check(fw.read_pandas(path, layout, sequential), expected)
        check(polars_slices(path).to_pandas(), expected, exact=False)
        print("values agree with pandas.read_fwf (fixedwidth exactly; the polars reference within rounding)")

        rows = [
            ("pandas.read_fwf", best(args.fwf_runs, lambda: read_fwf(path))),
            ("polars str.slice (reference)", best(args.runs, lambda: polars_slices(path))),
            ("fw.read_pandas", best(args.runs, lambda: fw.read_pandas(path, layout, options))),
            ("fw.read_pandas, 1 thread", best(args.runs, lambda: fw.read_pandas(path, layout, sequential))),
            ("fw.read_polars", best(args.runs, lambda: fw.read_polars(path, layout, options))),
            ("fw.read_arrow", best(args.runs, lambda: fw.read_arrow(path, layout, options))),
            ("fw.read (NumPy views)", best(args.runs, lambda: fw.read(path, layout, options))),
            ("fw.read, 1 thread", best(args.runs, lambda: fw.read(path, layout, sequential))),
        ]
    baseline = rows[0][1]
    print(f"{'reader':<32}{'seconds':>10}{'vs read_fwf':>14}")
    for name, seconds in rows:
        print(f"{name:<32}{seconds:>10.3f}{baseline / seconds:>13.1f}x")
    return 0


if __name__ == "__main__":
    sys.exit(main())
