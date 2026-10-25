namespace FixedWidthParser.Native
{
    internal static class NativeStatus
    {
        internal const int Ok = 0;
        internal const int Eof = -1;
        internal const int BufferTooSmall = -2;
        internal const int InvalidHandle = -3;
        internal const int InvalidArgument = -4;
        internal const int Error = -5;
        internal const int ParseError = -6;

        internal const int AbiVersion = 1;
    }
}
