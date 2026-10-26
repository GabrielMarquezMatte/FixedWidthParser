# Writing

```csharp
var people = new[]
{
    new Person { Name = "John Doe", Age = 30, Salary = 60000 },
    new Person { Name = "Jane",     Age = 28, Salary = 55000 },
};

using var stream = File.Create("out.txt");
FixedWidth.WriteMany(stream, people.AsSpan(), layout);
FixedWidth.WriteMany(stream, generatedPeople);   // generated models need no layout
```

`Write`, `WriteMany` (span or enumerable) and their async versions write each record followed by `NewLine`, batched into one pooled buffer. `FixedWidthWriterOptions` sets the `FormatProvider`, the `Encoding` and the `NewLine` (default `Environment.NewLine`). `FixedWidth.TryFormat(in model, layout, Span<byte>, in FixedWidthContext, out int)`, or its generated overload without the layout, formats one line into your own buffer.

Column alignment, padding, format strings and overflow are set per column: see [Formatting Options](models.md#formatting-options).
