using System.Collections.Concurrent;

namespace FixedWidthParser.Processors
{
    /// <summary>
    /// Thread-safe, type-keyed store of value-parser delegates shared by
    /// <see cref="ColumnParserRegistry"/> and <see cref="Utf8ColumnParserRegistry"/>. Holds each parser
    /// boxed as <see cref="Delegate"/>, keyed by the value <see cref="Type"/>; each public registry adds
    /// its strongly-typed <c>Register</c>/<c>Unregister</c>/<c>Get</c> surface on top of one of these.
    /// </summary>
    internal sealed class DelegateRegistry
    {
        private readonly ConcurrentDictionary<Type, Delegate> _parsers = new();

        public void Set(Type valueType, Delegate parser)
        {
            _parsers[valueType] = parser;
        }

        public bool Remove(Type valueType)
        {
            return _parsers.TryRemove(valueType, out _);
        }

        public Delegate? Get(Type valueType)
        {
            return _parsers.TryGetValue(valueType, out var parser) ? parser : null;
        }
    }
}
