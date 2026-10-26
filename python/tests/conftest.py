from __future__ import annotations

import pytest

import fixedwidth as fw

# A COTAHIST-like file: a header, three quotes (one with a blank quantity), an invalid line, a trailer.
CONTENT = (
    "00COTAHIST.2023\n"
    "0120231024PETR4       PETROBRAS PN0000000123456000000000000001000\n"
    "0120231024VALE3       VALE ON     0000000067890                  \n"
    "0120231025ITUB4       ITAU UNI PN 0000000004321000000000000000050\n"
    "0120231025XXXXX       BROKEN      00000000ABCDE000000000000000001\n"
    "99TRAILER\n"
)


def layout() -> fw.Layout:
    return fw.Layout(
        [
            fw.Column("tipreg", 0, 2, fw.String),
            fw.Column("data", 2, 8, fw.Date, format="yyyyMMdd"),
            fw.Column("codneg", 10, 12, fw.String),
            fw.Column("nomres", 22, 12, fw.String),
            fw.Column("preabe", 34, 13, fw.Float64, scale=2, padding="0"),
            fw.Column("quatot", 47, 18, fw.Int64, nullable=True, padding="0"),
        ]
    )


@pytest.fixture
def cotahist(tmp_path):
    path = tmp_path / "cotahist.txt"
    path.write_bytes(CONTENT.encode("utf-8"))
    return path


@pytest.fixture
def options() -> fw.ReadOptions:
    return fw.ReadOptions(header_lines=1, skip_invalid=True, degree_of_parallelism=1)
