using FixedWidthParser;

namespace Benchmarks
{
    /// <summary>The fixed-width input every read benchmark parses: <paramref name="count"/> <see cref="SampleModel"/> lines.</summary>
    internal static class SampleData
    {
        public static SampleModel[] Models(int count)
        {
            var models = new SampleModel[count];
            for (int i = 0; i < count; i++)
            {
                models[i] = new SampleModel { Name = "Name" + (i % 100), Age = 20 + (i % 50), Salary = 1000.0 + i };
            }
            return models;
        }

        public static byte[] Bytes(int count)
        {
            using var ms = new MemoryStream();
            FixedWidth.WriteMany(ms, Models(count).AsSpan(), FixedWidthLayout.FromAttributes<SampleModel>());
            return ms.ToArray();
        }
    }
}
