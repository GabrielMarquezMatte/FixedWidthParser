using FixedWidthParser.Infrastructure;

namespace FixedWidthParser.Native.Layouts
{
    /// <summary>The per-column rules of <c>fw_layout_create</c>, and the defaults the zeroed fields stand for.</summary>
    internal static class NativeColumnRules
    {
        internal static string? Check(NativeColumnSpecRaw spec, string? format)
        {
            if (spec.Type is < NativeType.String or > NativeType.Timestamp)
            {
                return $"unknown type {spec.Type}.";
            }
            if (spec.Scale is < 0 or > NativeType.MaxScale)
            {
                return $"scale {spec.Scale} is outside 0..{NativeType.MaxScale}.";
            }
            if (spec.Scale != 0 && spec.Type is not (NativeType.Float64 or NativeType.Decimal))
            {
                return "scale applies only to FLOAT64 and DECIMAL columns.";
            }
            if (format is not null && (spec.Type is NativeType.String or NativeType.Decimal || spec.Scale != 0))
            {
                return "a format applies only to INT64, an unscaled FLOAT64 and date or time columns.";
            }
            return CheckOptions(spec);
        }

        private static string? CheckOptions(NativeColumnSpecRaw spec)
        {
            if (!NativeOptions.TryAscii(spec.TrimChar, ' ', out _) || !NativeOptions.TryAscii(spec.Padding, ' ', out _))
            {
                return "trim_char and padding must be ASCII characters.";
            }
            if (spec.TrimMode is < 0 or > 2 || spec.Alignment is < 0 or > 2 || spec.Overflow is < 0 or > 1)
            {
                return "unknown trim_mode, alignment or overflow.";
            }
            if (spec.Overflow == 1 && spec.Type != NativeType.String)
            {
                return "truncate overflow applies only to STRING columns.";
            }
            return null;
        }

        internal static NativeColumn Build(NativeColumnSpecRaw spec, string name, string? format)
        {
            NativeOptions.TryAscii(spec.TrimChar, ' ', out char trimChar);
            NativeOptions.TryAscii(spec.Padding, ' ', out char padding);
            return new NativeColumn
            {
                Name = name,
                Start = spec.Start,
                Length = spec.Length,
                Type = spec.Type,
                Nullable = spec.Nullable != 0,
                Scale = spec.Scale,
                Parse = new ColumnParseOptions(trimChar, TrimModeOf(spec.TrimMode), format),
                FormatOptions = new ColumnFormatOptions(AlignmentOf(spec.Alignment, spec.Type), padding, format,
                    spec.Overflow == 1 ? OverflowBehavior.Truncate : OverflowBehavior.Throw),
            };
        }

        private static TrimMode TrimModeOf(int value)
        {
            return value switch
            {
                1 => TrimMode.Trailing,
                2 => TrimMode.Leading,
                _ => TrimMode.Both,
            };
        }

        private static Alignment AlignmentOf(int value, int type)
        {
            return value switch
            {
                1 => Alignment.Left,
                2 => Alignment.Right,
                _ => type == NativeType.String ? Alignment.Left : Alignment.Right,
            };
        }
    }
}
