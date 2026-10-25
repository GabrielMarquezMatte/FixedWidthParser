# Models and layouts

A layout maps byte ranges of a line to the members of a type. It comes from `[FixedColumn]` attributes, from the source generator, or from a fluent description in code; every read and write goes through one.

## Defining a Model

An attribute model needs a public parameterless constructor and mapped fields or properties; `FixedWidthLayout.FromAttributes<Person>()` turns it into a layout, built once per type by reflection:

```csharp
using FixedWidthParser;

public readonly record struct Person
{
    public Person()
    {
        Name = string.Empty;
    }

    [FixedColumn(0, 10)] public string Name { get; init; }
    [FixedColumn(10, 5)] public int Age { get; init; }
    [FixedColumn(15, 10)] public double Salary { get; init; }
}
```

`start` is the 0-based offset and `length` is the column width, both **in bytes**. In UTF-8 a character such as `é` takes two bytes, so a layout that counts characters only matches a UTF-8 file whose text is ASCII; for accented text, read the file in the single-byte encoding it was written in (see [Encodings](#encodings)).

For source generation, make the model `partial` and implement the marker interface:

```csharp
using FixedWidthParser;

public readonly partial record struct GeneratedPerson : IFixedWidthModel<GeneratedPerson>
{
    [FixedColumn(0, 10)] public string Name { get; init; }
    [FixedColumn(10, 5)] public int Age { get; init; }
    [FixedColumn(15, 10)] public double Salary { get; init; }
}
```

Columns can be declared on a base class: its `[FixedColumn]` members are part of the layout, for
attribute and generated models alike. An override keeps the attribute of the member it overrides unless
it declares its own, and a `new` member replaces the one it hides.

## Layouts without attributes

For a type you cannot annotate, or a layout known only at run time, describe the columns in code. Each column binds through a getter (used to write) and a setter (used to read), so nothing is reflected over:

```csharp
using FixedWidthParser;

var layout = FixedWidthLayout.Build<Order>(b => b
    .Factory(() => new Order())
    .Column<int>("Id", 0, 6, (in Order o) => o.Id, (ref Order o, int v) => o.Id = v,
        new FixedWidthColumnOptions { Alignment = Alignment.Right, Padding = '0' })
    .Column("Name", 6, 20, (in Order o) => o.Name, (ref Order o, string v) => o.Name = v)
    .NullableColumn<decimal>("Discount", 26, 10, (in Order o) => o.Discount, (ref Order o, decimal? v) => o.Discount = v)
    .DecodedColumn<DateOnly>("Date", 36, 8, (in Order o) => o.Date, (ref Order o, DateOnly v) => o.Date = v,
        new FixedWidthColumnOptions { Format = "yyyyMMdd" })
    .ConvertedColumn("Total", 44, 12, (in Order o) => o.Total, (ref Order o, Money v) => o.Total = v, new MoneyConverter()));
```

| Method | Column type |
|---|---|
| `Column` | `string` |
| `Column<T>` | a type that parses from and formats to UTF-8: numbers, `Guid`, … |
| `DecodedColumn<T>` | a type that parses only from characters (decoded first) |
| `NullableColumn<T>` | `T?` of a `Column<T>` type; a blank column is `null` |
| `NullableDecodedColumn<T>` | `T?` of a `DecodedColumn<T>` type, such as `DateOnly?` |
| `ConvertedColumn<T>` | any type, through an `IFixedWidthConverter<T>` instance |
| `NullableConvertedColumn<T>` | `T?` of a value type, through an `IFixedWidthConverter<T>` instance |

`FixedWidthColumnOptions` carries the same settings as `[FixedColumn]`, with the same defaults (see [Formatting Options](#formatting-options)). A reference-type model needs `Factory` to be read; a value type without one starts from `default`. Pass `null` as the getter or the setter to build a layout that only reads or only writes; using it the other way throws `InvalidOperationException` before any line is touched. The column name appears in error messages.

## Formatting Options

Each column can be tuned through named attribute arguments:

```csharp
[FixedColumn(0, 8, Alignment = Alignment.Right, Padding = '0')] public int Id { get; init; }       // "00000042"
[FixedColumn(8, 10, Format = "F2")]                            public double Amount { get; init; } // "1234.50   "
[FixedColumn(18, 5, Overflow = OverflowBehavior.Truncate)]     public string Code { get; init; }
[FixedColumn(23, 5, TrimChar = '0', TrimMode = TrimMode.Leading)] public int Count { get; init; }
```

- **`Alignment`**: `Left` (default) or `Right`.
- **`Padding`**: fill byte when writing (default space; for example `'0'` for zero-padding). Must be ASCII.
- **`TrimChar`** / **`TrimMode`**: what to strip when parsing, and from which side. Must be ASCII.
- **`Format`**: format string for `ISpanFormattable` when writing, and the exact format for `DateTime`, `DateOnly`, `TimeOnly` and `DateTimeOffset` when parsing.
- **`Overflow`**: `Default`, `Truncate` or `Throw`. `Default` resolves per type: strings truncate, everything else throws. A truncated string keeps whole characters, so a multi-byte character is never split.
- **`Converter`**: a type implementing `IFixedWidthConverter<T>`, used instead of the built-in parsing and formatting.

## Column Types

`string`, `double` and `float`, enums, any `IUtf8SpanParsable<T>` (parsed straight from bytes), any other `ISpanParsable<T>` (decoded first), `T?` of any of them (a blank column is `null`), and anything with a `Converter`. Writing needs `string`, `ISpanFormattable` (enums included) or a converter.

An enum column reads a member's name in any case or its number (`2`, `02`), and rejects a number no member has and a combination of `[Flags]` members. It writes the name; `Format = "D"` writes the number, which `Padding = '0'` and `Alignment.Right` zero-pad. Fluent layouts add one with `EnumColumn` or `NullableEnumColumn`.

```csharp
public enum Side { Buy = 1, Sell = 2 }

[FixedColumn(0, 2, Format = "D", Padding = '0', Alignment = Alignment.Right)]
public Side Side { get; set; }   // "02" reads and writes as Side.Sell
```

## Encodings

Column positions are bytes, so the encoding only decides how text inside a column is read and written. UTF-8 files with ASCII text and single-byte encodings (Latin-1, Windows-1252, …) keep one byte per character. UTF-16 and EBCDIC are not supported.

```csharp
var options = new FixedWidthReaderOptions { Encoding = Encoding.Latin1 };
var records = FixedWidth.ReadFile<GeneratedPerson>("legacy.txt", options);
```

On .NET, Windows-1252 and other code pages need `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` first.

## Validation

Invalid layouts fail fast with an `InvalidOperationException` from `FixedWidthLayout.Build` or `FromAttributes`, or a generator diagnostic on generated models: negative `Start`, `Length < 1`, overlapping columns, a non-ASCII `Padding` or `TrimChar` (FWP012), an unsupported column type and a converter for the wrong type. Adjacent columns are valid. An attribute model that can be read but not written (or the reverse), such as one with a column type that only formats, gets a layout anyway; the side it cannot do throws on first use.
