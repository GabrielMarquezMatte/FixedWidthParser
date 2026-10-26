using System.Runtime.InteropServices;
using System.Text;
using FixedWidthParser.Native;
using FixedWidthParser.Native.Layouts;

namespace FixedWidthParser.Tests.Native
{
    /// <summary>One column as a C caller would describe it; unset fields stay zero, the C defaults.</summary>
    internal sealed record Col(string Name, int Start, int Length, int Type)
    {
        public bool Nullable { get; init; }
        public int Scale { get; init; }
        public string? Format { get; init; }
        public int TrimChar { get; init; }
        public int TrimMode { get; init; }
        public int Alignment { get; init; }
        public int Padding { get; init; }
        public int Overflow { get; init; }
        public int Dictionary { get; init; }
        public int StructSize { get; init; } = Marshal.SizeOf<NativeColumnSpecRaw>();
    }

    /// <summary>Builds <see cref="NativeLayout"/>s from <see cref="Col"/>s through the same entry point <c>fw_layout_create</c> uses.</summary>
    internal static unsafe class NativeTestLayouts
    {
        internal const int TString = 0;
        internal const int TInt64 = 1;
        internal const int TFloat64 = 2;
        internal const int TDecimal = 3;
        internal const int TDate = 4;
        internal const int TTime = 5;
        internal const int TTimestamp = 6;

        internal static int Create(out NativeLayout? layout, params Col[] columns)
        {
            return Create(out layout, null, columns);
        }

        internal static int Create(out NativeLayout? layout, NativeLayoutOptionsRaw? options, params Col[] columns)
        {
            var allocations = new List<IntPtr>();
            try
            {
                var specs = columns.Select(c => ToRaw(c, allocations)).ToArray();
                if (options is { } value)
                {
                    return NativeLayout.TryCreate(specs, &value, out layout);
                }
                return NativeLayout.TryCreate(specs, null, out layout);
            }
            finally
            {
                allocations.ForEach(Marshal.FreeHGlobal);
            }
        }

        internal static NativeLayout Build(params Col[] columns)
        {
            int status = Create(out var layout, columns);
            Assert.True(status == NativeStatus.Ok, $"status {status}: {LastError()}");
            return layout!;
        }

        internal static NativeLayout Build(NativeLayoutOptionsRaw options, params Col[] columns)
        {
            int status = Create(out var layout, options, columns);
            Assert.True(status == NativeStatus.Ok, $"status {status}: {LastError()}");
            return layout!;
        }

        internal static NativeLayoutOptionsRaw LayoutOptions(int lineLength = 0, int filler = 0)
        {
            return new NativeLayoutOptionsRaw { StructSize = sizeof(NativeLayoutOptionsRaw), LineLength = lineLength, Filler = filler };
        }

        internal static string LastError()
        {
            var buffer = new byte[4096];
            NativeApi.LastError(buffer, out int length);
            return Encoding.UTF8.GetString(buffer, 0, length);
        }

        private static NativeColumnSpecRaw ToRaw(Col column, List<IntPtr> allocations)
        {
            var (name, nameLength) = Utf8(column.Name, allocations);
            var (format, formatLength) = Utf8(column.Format, allocations);
            return new NativeColumnSpecRaw
            {
                StructSize = column.StructSize,
                Name = (byte*)name,
                NameLen = nameLength,
                Start = column.Start,
                Length = column.Length,
                Type = column.Type,
                Nullable = column.Nullable ? 1 : 0,
                Scale = column.Scale,
                Format = (byte*)format,
                FormatLen = formatLength,
                TrimChar = column.TrimChar,
                TrimMode = column.TrimMode,
                Alignment = column.Alignment,
                Padding = column.Padding,
                Overflow = column.Overflow,
                Dictionary = column.Dictionary,
            };
        }

        private static (IntPtr Pointer, int Length) Utf8(string? text, List<IntPtr> allocations)
        {
            if (text is null)
            {
                return (IntPtr.Zero, 0);
            }
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            IntPtr memory = Marshal.AllocHGlobal(Math.Max(1, bytes.Length));
            allocations.Add(memory);
            Marshal.Copy(bytes, 0, memory, bytes.Length);
            return (memory, bytes.Length);
        }
    }
}
