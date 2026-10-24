"""B3's COTAHIST quote history layout (record type 01) and a synthetic file generator.

Positions follow B3's "Layout do Arquivo - Cotações Históricas": 26 fields that tile bytes 1..245
exactly (checked in `check_layout`). The real files are B3's and are not committed; `generate` writes a
file of the same shape with random quotes, a 00 header and a 99 trailer.
"""

from __future__ import annotations

import random
from pathlib import Path

import fixedwidth as fw

LINE_LENGTH = 245

# (name, 1-based first byte, length, type, scale)
FIELDS = [
    ("tipreg", 1, 2, fw.String, 0),
    ("data_pregao", 3, 8, fw.Date, 0),
    ("codbdi", 11, 2, fw.String, 0),
    ("codneg", 13, 12, fw.String, 0),
    ("tpmerc", 25, 3, fw.Int64, 0),
    ("nomres", 28, 12, fw.String, 0),
    ("especi", 40, 10, fw.String, 0),
    ("prazot", 50, 3, fw.String, 0),
    ("modref", 53, 4, fw.String, 0),
    ("preabe", 57, 13, fw.Float64, 2),
    ("premax", 70, 13, fw.Float64, 2),
    ("premin", 83, 13, fw.Float64, 2),
    ("premed", 96, 13, fw.Float64, 2),
    ("preult", 109, 13, fw.Float64, 2),
    ("preofc", 122, 13, fw.Float64, 2),
    ("preofv", 135, 13, fw.Float64, 2),
    ("totneg", 148, 5, fw.Int64, 0),
    ("quatot", 153, 18, fw.Int64, 0),
    ("voltot", 171, 18, fw.Float64, 2),
    ("preexe", 189, 13, fw.Float64, 2),
    ("indopc", 202, 1, fw.Int64, 0),
    ("datven", 203, 8, fw.Date, 0),
    ("fatcot", 211, 7, fw.Int64, 0),
    ("ptoexe", 218, 13, fw.Float64, 6),
    ("codisi", 231, 12, fw.String, 0),
    ("dismes", 243, 3, fw.Int64, 0),
]


def check_layout() -> None:
    position = 1
    for name, first, length, _, _ in FIELDS:
        assert first == position, f"{name} starts at {first}, expected {position}"
        position += length
    assert position - 1 == LINE_LENGTH


def layout() -> fw.Layout:
    check_layout()
    return fw.Layout(
        [
            fw.Column(
                name, first - 1, length, kind,
                scale=scale,
                format="yyyyMMdd" if kind is fw.Date else None,
                padding="0" if kind in (fw.Int64, fw.Float64) else None,
            )
            for name, first, length, kind, scale in FIELDS
        ]
    )


def colspecs() -> list[tuple[int, int]]:
    return [(first - 1, first - 1 + length) for _, first, length, _, _ in FIELDS]


TICKERS = [f"{a}{b}{c}{d}{n}" for a in "ABCPV" for b in "AEIO" for c in "TRLN" for d in "SAEB" for n in (3, 4, 11)]


def _line(rng: random.Random, day: int) -> str:
    ticker = rng.choice(TICKERS)
    open_ = rng.randint(100, 9_999_999)
    prices = [open_ + rng.randint(-500, 500) for _ in range(6)]
    trades = rng.randint(1, 99_999)
    quantity = rng.randint(100, 10**12)
    return (
        "01"
        + f"202310{day:02d}"
        + "02"
        + f"{ticker:<12}"
        + "010"
        + f"{ticker[:4] + ' SA':<12}"
        + f"{'ON  NM':<10}"
        + "   "
        + "R$  "
        + "".join(f"{p:013d}" for p in [open_, *prices])
        + f"{trades:05d}"
        + f"{quantity:018d}"
        + f"{quantity * open_ // 100:018d}"
        + f"{0:013d}"
        + "0"
        + "99991231"
        + "0000001"
        + f"{0:013d}"
        + f"BR{ticker[:4]}ACNOR{rng.randint(0, 9)}"
        + f"{rng.randint(0, 999):03d}"
    )


def generate(path: Path, lines: int, seed: int = 2023) -> None:
    rng = random.Random(seed)
    header = f"{'00COTAHIST.2023BOVESPA 20231229':<{LINE_LENGTH}}"
    trailer = f"{'99COTAHIST.2023BOVESPA 20231229' + f'{lines + 2:011d}':<{LINE_LENGTH}}"
    with path.open("w", encoding="ascii", newline="\n") as file:
        file.write(header + "\n")
        for i in range(lines):
            line = _line(rng, 2 + i * 27 // max(1, lines))
            assert len(line) == LINE_LENGTH, len(line)
            file.write(line + "\n")
        file.write(trailer + "\n")
