namespace FixedWidthParser.Attributes
{
    /// <summary>
    /// Validates a model's column layout when the parser/writer is built: rejects negative
    /// <c>Start</c>, non-positive <c>Length</c> and overlapping columns — failing early and with a
    /// clear message, instead of producing obscure per-line errors at runtime.
    /// </summary>
    public static class ColumnLayoutValidator
    {
        public static void Validate(Span<(int Start, int Length, string Name)> columns, Type modelType)
        {
            foreach (var (Start, Length, Name) in columns)
            {
                if (Start < 0)
                {
                    throw new InvalidOperationException(
                        $"Column \"{Name}\" in {modelType.Name} has a negative Start ({Start}).");
                }
                if (Length < 1)
                {
                    throw new InvalidOperationException(
                        $"Column \"{Name}\" in {modelType.Name} has an invalid Length ({Length}); it must be >= 1.");
                }
            }

            if (columns.Length < 2)
            {
                return;
            }

            // Detect overlap by sorting on start and tracking the farthest end seen so far.
            columns.Sort(static (a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.Length.CompareTo(b.Length));
            var farthest = columns[0];
            int maxEnd = farthest.Start + farthest.Length;
            for (int i = 1; i < columns.Length; i++)
            {
                var current = columns[i];
                if (current.Start < maxEnd)
                {
                    throw new InvalidOperationException(
                        $"Overlapping columns in {modelType.Name}: \"{farthest.Name}\" [{farthest.Start}, {farthest.Start + farthest.Length}) " +
                        $"and \"{current.Name}\" [{current.Start}, {current.Start + current.Length}).");
                }
                int end = current.Start + current.Length;
                if (end > maxEnd)
                {
                    maxEnd = end;
                    farthest = current;
                }
            }
        }
    }
}
