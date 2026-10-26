namespace AotSmoke
{
    internal sealed record class Order
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public decimal Amount { get; set; }
        public DateOnly Date { get; set; }
    }
}
