using AotSmoke;
using FixedWidthParser;

var layout = FixedWidthLayout.Build<Order>(b => b
    .Factory(() => new Order())
    .Column<int>("Id", 0, 6, (in Order o) => o.Id, (ref Order o, int v) => o.Id = v,
        new FixedWidthColumnOptions { Alignment = Alignment.Right, Padding = '0' })
    .Column("Name", 6, 20, (in Order o) => o.Name, (ref Order o, string v) => o.Name = v)
    .Column<decimal>("Amount", 26, 12, (in Order o) => o.Amount, (ref Order o, decimal v) => o.Amount = v)
    .DecodedColumn<DateOnly>("Date", 38, 8, (in Order o) => o.Date, (ref Order o, DateOnly v) => o.Date = v,
        new FixedWidthColumnOptions { Format = "yyyyMMdd" }));

var orders = Enumerable.Range(1, 1000)
    .Select(i => new Order { Id = i, Name = "Order " + i, Amount = i * 1.25m, Date = new DateOnly(2026, 1, 1).AddDays(i) })
    .ToList();

await using var stream = new MemoryStream();
await FixedWidth.WriteManyAsync(stream, orders, layout);
stream.Position = 0;
var read = await FixedWidth.ReadAsync(stream, layout, leaveOpen: true).ToListAsync();

if (!orders.SequenceEqual(read))
{
    await Console.Error.WriteLineAsync("Round trip mismatch.");
    return 1;
}
await Console.Out.WriteLineAsync($"Round-tripped {read.Count} orders.");
return 0;
