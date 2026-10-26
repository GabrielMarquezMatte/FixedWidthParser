"""Writes a synthetic COTAHIST table with fixedwidth and with the ways pandas and polars users write one, checks
every writer produces the same bytes, and times them.

    python python/benchmarks/bench_write.py [--lines 1000000] [--runs 5] [--savetxt-runs 1]

The table is read once, before any timing, from a generated file; every writer then turns that same table back
into its record lines, so the expected output is the file's body, byte for byte. pandas and polars have no
fixed-width writer: the baselines are numpy.savetxt with one printf field per column, and polars string
expressions padded and concatenated into one column written as CSV.
"""

from __future__ import annotations

import argparse
import platform
import sys
import tempfile
import time
from pathlib import Path

import fixedwidth as fw
import numpy as np
import pandas as pd
import polars as pl
import pyarrow as pa

sys.path.insert(0, str(Path(__file__).parent))
import cotahist  # noqa: E402


def savetxt(frame: pd.DataFrame, path: Path) -> None:
    columns, formats = [], []
    for name, _, length, kind, scale in cotahist.FIELDS:
        column = frame[name]
        if kind is fw.String:
            columns.append(column.to_numpy(dtype=object))
            formats.append(f"%-{length}s")
        elif kind is fw.Date:
            dates = pd.to_datetime(column)
            columns.append((dates.dt.year * 10000 + dates.dt.month * 100 + dates.dt.day).to_numpy())
            formats.append(f"%0{length}d")
        elif kind is fw.Float64:
            columns.append(np.round(column.to_numpy() * 10**scale).astype(np.int64))
            formats.append(f"%0{length}d")
        else:
            columns.append(column.to_numpy())
            formats.append(f"%0{length}d")
    rows = np.empty((len(frame), len(columns)), dtype=object)
    for index, values in enumerate(columns):
        rows[:, index] = values
    # A binary file, or Windows turns every "\n" into "\r\n".
    with path.open("wb") as file:
        np.savetxt(file, rows, fmt="".join(formats), newline="\n")


def zero_padded(integers: pl.Expr, length: int) -> pl.Expr:
    # The sign stays leftmost, ahead of the zeros, as printf's %0Nd and fixedwidth write it.
    digits = integers.abs().cast(pl.String)
    return pl.when(integers < 0).then(pl.lit("-") + digits.str.pad_start(length - 1, "0")).otherwise(digits.str.pad_start(length, "0"))


def polars_strings(frame: pl.DataFrame, path: Path) -> None:
    fields = []
    for name, _, length, kind, scale in cotahist.FIELDS:
        column = pl.col(name)
        if kind is fw.String:
            fields.append(column.str.pad_end(length))
        elif kind is fw.Date:
            fields.append(column.dt.strftime("%Y%m%d"))
        elif kind is fw.Float64:
            fields.append(zero_padded((column * 10**scale).round().cast(pl.Int64), length))
        else:
            fields.append(zero_padded(column, length))
    frame.select(pl.concat_str(fields).alias("line")).write_csv(path, include_header=False, quote_style="never")


def best(runs: int, work, path: Path, expected: bytes) -> float:
    times = []
    for _ in range(runs):
        path.unlink(missing_ok=True)
        start = time.perf_counter()
        work()
        times.append(time.perf_counter() - start)
    assert path.read_bytes() == expected, f"{path.name} differs from the expected record lines"
    return min(times)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lines", type=int, default=1_000_000)
    parser.add_argument("--runs", type=int, default=5)
    parser.add_argument("--savetxt-runs", type=int, default=1, help="numpy.savetxt is slow; one run is usually enough")
    args = parser.parse_args()

    layout = cotahist.layout()
    read = fw.ReadOptions(header_lines=1, skip_invalid=True)
    sequential = fw.WriteOptions(degree_of_parallelism=1)
    with tempfile.TemporaryDirectory() as directory:
        source = Path(directory) / "COTAHIST_SYNTHETIC.TXT"
        cotahist.generate(source, args.lines)
        lines = source.read_bytes().splitlines(keepends=True)
        expected = b"".join(lines[1:-1])
        arrow = fw.read_arrow(source, layout, read)
        frame = fw.read_pandas(source, layout, read)
        polars = pl.from_arrow(arrow)
        size = len(expected) / 2**20
        print(f"{args.lines:,} lines, {size:.0f} MiB, Python {platform.python_version()}, pandas {pd.__version__}, "
              f"polars {pl.__version__}, pyarrow {pa.__version__}")

        out = Path(directory) / "out.txt"
        rows = [
            ("numpy.savetxt (pandas)", best(args.savetxt_runs, lambda: savetxt(frame, out), out, expected)),
            ("polars pad + concat_str", best(args.runs, lambda: polars_strings(polars, out), out, expected)),
            ("fw.write (pandas)", best(args.runs, lambda: fw.write(frame, out, layout), out, expected)),
            ("fw.write (polars)", best(args.runs, lambda: fw.write(polars, out, layout), out, expected)),
            ("fw.write (pyarrow)", best(args.runs, lambda: fw.write(arrow, out, layout), out, expected)),
            ("fw.write (pyarrow), 1 thread", best(args.runs, lambda: fw.write(arrow, out, layout, sequential), out, expected)),
            ("fw.write_bytes (pyarrow)", best(args.runs, lambda: out.write_bytes(fw.write_bytes(arrow, layout)), out, expected)),
        ]
        print("every writer produced the generated file's record lines, byte for byte")
    baseline = rows[0][1]
    print(f"{'writer':<32}{'seconds':>10}{'vs savetxt':>13}")
    for name, seconds in rows:
        print(f"{name:<32}{seconds:>10.3f}{baseline / seconds:>12.1f}x")
    return 0


if __name__ == "__main__":
    sys.exit(main())
