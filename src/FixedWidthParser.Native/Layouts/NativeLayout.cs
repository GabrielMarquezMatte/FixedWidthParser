using System.Text;
using FixedWidthParser.Attributes;
using FixedWidthParser.Infrastructure;

namespace FixedWidthParser.Native.Layouts
{
    /// <summary>One validated column of a <see cref="NativeLayout"/>.</summary>
    internal sealed class NativeColumn
    {
        internal required string Name { get; init; }
        internal int Start { get; init; }
        internal int Length { get; init; }
        internal int Type { get; init; }
        internal bool Nullable { get; init; }
        internal int Scale { get; init; }
        internal ColumnParseOptions Parse { get; init; }
        internal ColumnFormatOptions FormatOptions { get; init; }
    }

    /// <summary>
    /// The C counterpart of <c>FixedWidthLayout&lt;T&gt;</c>: columns described at run time by
    /// <c>fw_column_spec</c>s, validated once and shared, immutable, by every read and write.
    /// </summary>
    internal sealed class NativeLayout
    {
        private const int MaxColumns = 16_384;

        private NativeLayout(NativeColumn[] columns, int lineLength, byte filler)
        {
            Columns = columns;
            LineLength = lineLength;
            Filler = filler;
        }

        internal NativeColumn[] Columns { get; }
        internal int LineLength { get; }
        internal byte Filler { get; }

        /// <summary>The index of the first column named <paramref name="name"/>, or -1.</summary>
        internal int IndexOf(string? name)
        {
            return Array.FindIndex(Columns, c => string.Equals(c.Name, name, StringComparison.Ordinal));
        }

        internal static unsafe int TryCreate(ReadOnlySpan<NativeColumnSpecRaw> specs, NativeLayoutOptionsRaw* options, out NativeLayout? layout)
        {
            layout = null;
            int status = NativeOptions.TryRead(options, "fw_layout_options", out var settings);
            if (status != NativeStatus.Ok)
            {
                return status;
            }
            string? error = specs.Length is < 1 or > MaxColumns
                ? $"A layout needs at least one column and at most {MaxColumns}; got {specs.Length}."
                : null;
            var columns = new NativeColumn[specs.Length];
            for (int i = 0; i < specs.Length && error is null; i++)
            {
                error = TryColumn(specs[i], i, out columns[i]);
            }
            error ??= ValidatePositions(columns);
            if (error is not null)
            {
                return NativeApi.InvalidArgument(error);
            }
            return TryFinish(columns, settings, out layout);
        }

        private static int TryFinish(NativeColumn[] columns, NativeLayoutOptionsRaw settings, out NativeLayout? layout)
        {
            layout = null;
            int end = columns.Max(c => c.Start + c.Length);
            int lineLength = settings.LineLength == 0 ? end : settings.LineLength;
            if (lineLength < end)
            {
                return NativeApi.InvalidArgument($"line_length {settings.LineLength} is shorter than the columns, which end at {end}.");
            }
            if (!NativeOptions.TryAscii(settings.Filler, ' ', out char filler))
            {
                return NativeApi.InvalidArgument($"filler {settings.Filler} is not an ASCII character.");
            }
            layout = new NativeLayout(columns, lineLength, (byte)filler);
            return NativeStatus.Ok;
        }

        private static string? ValidatePositions(NativeColumn[] columns)
        {
            var positions = columns.Select(c => (c.Start, c.Length, c.Name)).ToArray();
            try
            {
                ColumnLayoutValidator.Validate(positions, "the layout");
                return null;
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }
        }

        private static unsafe string? TryColumn(NativeColumnSpecRaw spec, int index, out NativeColumn column)
        {
            column = null!;
            if (spec.StructSize != sizeof(NativeColumnSpecRaw))
            {
                return $"Column {index}: fw_column_spec struct_size is {spec.StructSize}; expected {sizeof(NativeColumnSpecRaw)}.";
            }
            if (spec.Name is null || spec.NameLen <= 0 || (spec.Format is null && spec.FormatLen != 0) || spec.FormatLen < 0)
            {
                return $"Column {index} has no name, or a name or format pointer that does not match its length.";
            }
            string name = Encoding.UTF8.GetString(spec.Name, spec.NameLen);
            string? format = spec.FormatLen == 0 ? null : Encoding.UTF8.GetString(spec.Format, spec.FormatLen);
            string? error = NativeColumnRules.Check(spec, format);
            if (error is not null)
            {
                return $"Column \"{name}\": {error}";
            }
            column = NativeColumnRules.Build(spec, name, format);
            return null;
        }
    }
}
