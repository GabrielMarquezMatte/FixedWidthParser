"""Table: a columnar read whose numeric columns are NumPy views over the native buffers."""

from __future__ import annotations

import ctypes
from collections.abc import Callable
from decimal import Decimal
from typing import Any

from fixedwidth import _native
from fixedwidth.types import ColumnType

_ELEMENTS = {
    ColumnType.INT64: (ctypes.c_int64, "int64"),
    ColumnType.FLOAT64: (ctypes.c_double, "float64"),
    ColumnType.DECIMAL: (ctypes.c_int64, "int64"),
    ColumnType.DATE: (ctypes.c_int32, "int32"),
    ColumnType.TIME: (ctypes.c_int64, "int64"),
    ColumnType.TIMESTAMP: (ctypes.c_int64, "int64"),
}


class _Owner:
    """Owns one native fw_table; frees it once nothing (the Table or any view) references it."""

    def __init__(self, table: _native.Table) -> None:
        self.table = table
        self._on_free: Callable[[bool], object] | None = None

    def view(self, address: int, element: type, count: int) -> ctypes.Array:
        buffer = (element * count).from_address(address)
        buffer._owner = self  # type: ignore[attr-defined]  # a view keeps the native memory alive
        return buffer

    def __del__(self) -> None:
        _native.load_library().fw_free_table(ctypes.byref(self.table))
        if self._on_free is not None:
            self._on_free(True)


class Table:
    """One read: `table[name]` is a NumPy view for numeric and date/time columns (as stored: int64,
    float64, DECIMAL's unscaled int64, DATE's int32 days, TIME/TIMESTAMP's int64 microseconds) and a
    list of `str` for strings. Null rows are marked by `validity(name)`. Views stay valid after the
    Table is gone; the native memory is freed when the last one is."""

    def __init__(
        self, owner: _Owner, names: list[str], lines_read: int = 0, lines_skipped: int = 0, dictionaries: frozenset[str] = frozenset()
    ) -> None:
        self._owner = owner
        self._names = names
        self._dictionaries = dictionaries
        self.lines_read = lines_read
        self.lines_skipped = lines_skipped

    @property
    def num_rows(self) -> int:
        return int(self._owner.table.row_count)

    @property
    def column_names(self) -> list[str]:
        return list(self._names)

    def __len__(self) -> int:
        return self.num_rows

    def _column(self, name: str) -> _native.Column:
        try:
            return self._owner.table.columns[self._names.index(name)]
        except ValueError:
            raise KeyError(name) from None

    def __getitem__(self, name: str) -> Any:
        column = self._column(name)
        kind = ColumnType(column.type)
        if kind is ColumnType.STRING:
            return _strings(column)
        import numpy

        element, dtype = _ELEMENTS[kind]
        if column.length == 0:
            return numpy.empty(0, dtype=dtype)
        return numpy.frombuffer(self._owner.view(column.values, element, column.length), dtype=dtype)

    def validity(self, name: str) -> Any:
        """A NumPy bool array, True where the row holds a value, or None when the column has no nulls."""
        column = self._column(name)
        if not column.validity:
            return None
        import numpy

        bits = numpy.frombuffer(ctypes.string_at(column.validity, (column.length + 7) // 8), dtype=numpy.uint8)
        return numpy.unpackbits(bits, bitorder="little")[: column.length].astype(bool)

    def to_arrow(self) -> Any:
        """The same columns as a `pyarrow.Table`, sharing the native buffers. Requires pyarrow."""
        import pyarrow

        arrays = [self._arrow_column(name, pyarrow) for name in self._names]
        return pyarrow.Table.from_arrays(arrays, names=self._names)

    def _arrow_column(self, name: str, pa: Any) -> Any:
        column = self._column(name)
        kind = ColumnType(column.type)
        n = int(column.length)
        validity = pa.foreign_buffer(column.validity, (n + 7) // 8, base=self._owner) if column.validity else None
        if kind is ColumnType.DECIMAL:
            # ponytail: decimals go through Python objects; fw.read_arrow() hands them over natively.
            mask = self.validity(name)
            values = self[name]
            decimals = [None if mask is not None and not mask[i] else Decimal(int(v)).scaleb(-column.scale) for i, v in enumerate(values)]
            return pa.array(decimals, type=pa.decimal128(18, column.scale))
        if kind is ColumnType.STRING:
            offsets = pa.foreign_buffer(column.values, (n + 1) * 4, base=self._owner)
            data = pa.foreign_buffer(column.data, column.data_len, base=self._owner) if column.data else pa.py_buffer(b"")
            strings = pa.Array.from_buffers(pa.string(), n, [validity, offsets, data])
            return strings.dictionary_encode() if name in self._dictionaries else strings
        arrow_type = {
            ColumnType.INT64: pa.int64(),
            ColumnType.FLOAT64: pa.float64(),
            ColumnType.DATE: pa.date32(),
            ColumnType.TIME: pa.time64("us"),
            ColumnType.TIMESTAMP: pa.timestamp("us"),
        }[kind]
        width = 4 if kind is ColumnType.DATE else 8
        values = pa.foreign_buffer(column.values, n * width, base=self._owner) if n else pa.py_buffer(b"")
        return pa.Array.from_buffers(arrow_type, n, [validity, values])

    def __repr__(self) -> str:
        return f"Table({self.num_rows} rows: {', '.join(self._names)})"


def _strings(column: _native.Column) -> list[str]:
    n = int(column.length)
    if n == 0:
        return []
    offsets = (ctypes.c_int32 * (n + 1)).from_address(column.values)
    data = ctypes.string_at(column.data, column.data_len) if column.data else b""
    return [data[offsets[i] : offsets[i + 1]].decode("utf-8") for i in range(n)]
