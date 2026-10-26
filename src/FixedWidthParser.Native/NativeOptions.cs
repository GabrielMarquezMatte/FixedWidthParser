namespace FixedWidthParser.Native
{
    /// <summary>
    /// Reads a caller's options struct under the <c>struct_size</c> contract: <see langword="null"/> means all
    /// defaults (the zeroed struct), and any size other than the current one is refused.
    /// </summary>
    internal static unsafe class NativeOptions
    {
        internal static int TryRead<T>(T* options, string name, out T value) where T : unmanaged
        {
            value = default;
            if (options is null)
            {
                return NativeStatus.Ok;
            }
            int size = *(int*)options;
            if (size != sizeof(T))
            {
                return NativeApi.InvalidArgument($"{name} struct_size is {size}; expected {sizeof(T)}.");
            }
            value = *options;
            return NativeStatus.Ok;
        }

        /// <summary>An ASCII byte option where 0 means <paramref name="fallback"/>; <see langword="false"/> for anything outside ASCII.</summary>
        internal static bool TryAscii(int value, char fallback, out char result)
        {
            result = value == 0 ? fallback : (char)value;
            return value is >= 0 and <= 127;
        }
    }
}
