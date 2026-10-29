from __future__ import annotations

import decimal
from decimal import Decimal

import pytest

import fixedwidth as fw
from fixedwidth.writer import _unscaled

MONEY = fw.Column("money", 0, 12, fw.Decimal, scale=2)


@pytest.mark.parametrize("precision", [3, 6, 28])
def test_scaling_ignores_the_decimal_context(precision):
    with decimal.localcontext() as context:
        context.prec = precision

        assert _unscaled(Decimal("12345.67"), MONEY) == 1234567
        assert _unscaled(Decimal("-92233720368547758.08"), MONEY) == -(2**63)
        with pytest.raises(fw.FixedWidthError, match="12345.678"):
            _unscaled(Decimal("12345.678"), MONEY)


def test_scaling_keeps_nulls_zero_and_int64_bounds():
    assert _unscaled(None, MONEY) == 0
    assert _unscaled(Decimal("0"), MONEY) == 0
    assert _unscaled(Decimal("92233720368547758.07"), MONEY) == 2**63 - 1
    with pytest.raises(fw.FixedWidthError, match="int64"):
        _unscaled(Decimal("92233720368547758.08"), MONEY)
