namespace Benchmarks.Attributes
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class FixedColumnAttribute(int start, int length) : Attribute
    {
        public int Start { get; } = start;
        public int Length { get; } = length;
    }
}