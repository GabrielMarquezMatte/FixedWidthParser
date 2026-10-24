from __future__ import annotations

import pytest

import fixedwidth as fw
from fixedwidth import _native


def test_abi_version_matches():
    assert _native.load_library().fw_abi_version() == _native.FW_ABI_VERSION


def test_overlapping_columns_raise():
    with pytest.raises(fw.FixedWidthError, match="Overlapping"):
        fw.Layout([fw.Column("a", 0, 5, fw.String), fw.Column("b", 3, 5, fw.String)])


def test_layout_validates_options():
    with pytest.raises(fw.FixedWidthError, match="scale"):
        fw.Layout([fw.Column("a", 0, 5, fw.Int64, scale=2)])
    with pytest.raises(ValueError, match="one ASCII character"):
        fw.Layout([fw.Column("a", 0, 5, fw.String, padding="ab")])


def test_layout_lists_its_columns():
    columns = [fw.Column("a", 0, 2, fw.String), fw.Column("b", 2, 3, fw.Int64)]

    assert fw.Layout(columns).columns == columns


def test_unknown_encoding_raises():
    with pytest.raises(ValueError, match="unknown encoding"):
        fw.read(b"ab\n", fw.Layout([fw.Column("a", 0, 2, fw.String)]), fw.ReadOptions(encoding="klingon"))
