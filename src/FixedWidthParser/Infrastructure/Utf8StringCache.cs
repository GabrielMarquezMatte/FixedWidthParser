using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Infrastructure
{
    /// <summary>
    /// A lock-free cache of UTF-8 columns in front of a <see cref="StringPool"/>, one per thread, because the
    /// pool's own lookup is slow (docs/reader-performance.md).
    /// Fixed-size and 4-way set-associative, as in ExcelReader's <c>Utf8StringCache</c>: a full set evicts its
    /// oldest entry, and every hit is re-verified against the bytes, so a collision costs a lookup in the pool,
    /// never a wrong string. A miss goes to the pool, so every string returned was handed out by it.
    /// </summary>
    internal sealed class Utf8StringCache
    {
        private const int SetCount = 1024;
        private const int Ways = 4;
        private const int MaxKeyLength = 64;

        [ThreadStatic]
        private static Utf8StringCache? t_current;

        private readonly StringPool _pool;
        private readonly string?[] _values = new string[SetCount * Ways];
        private readonly int[] _hashes = new int[SetCount * Ways];

        private Utf8StringCache(StringPool pool)
        {
            _pool = pool;
        }

        /// <summary>The string <paramref name="pool"/> holds for <paramref name="column"/>, decoded with <paramref name="encoding"/>.</summary>
        internal static string GetOrAdd(StringPool pool, ReadOnlySpan<byte> column, Encoding encoding)
        {
            if (column.Length > MaxKeyLength || encoding.CodePage != Encoding.UTF8.CodePage)
            {
                return pool.GetOrAdd(column, encoding);
            }
            var cache = t_current;
            if (cache is null || !ReferenceEquals(cache._pool, pool))
            {
                // ponytail: one pool per thread; a thread alternating between pools rebuilds its cache each switch.
                t_current = cache = new Utf8StringCache(pool);
            }
            return cache.GetOrAdd(column, encoding);
        }

        private string GetOrAdd(ReadOnlySpan<byte> utf8, Encoding encoding)
        {
            if (utf8.IsEmpty)
            {
                return string.Empty;
            }
            int hash = ComputeHash(utf8);
            int start = (hash & (SetCount - 1)) * Ways;
            for (int i = start; i < start + Ways; i++)
            {
                string? cached = _values[i];
                if (cached is null)
                {
                    break;
                }
                if (_hashes[i] == hash && Matches(cached, utf8))
                {
                    return cached;
                }
            }
            // Decoded with the context's encoding, so a strict decoder still rejects invalid bytes.
            string value = _pool.GetOrAdd(utf8, encoding);
            Array.Copy(_values, start, _values, start + 1, Ways - 1);
            Array.Copy(_hashes, start, _hashes, start + 1, Ways - 1);
            _values[start] = value;
            _hashes[start] = hash;
            return value;
        }

        [SkipLocalsInit]
        private static bool Matches(string cached, ReadOnlySpan<byte> utf8)
        {
            if (cached.Length > utf8.Length)
            {
                return false;
            }
            if (cached.Length == utf8.Length && Ascii.Equals(cached, utf8))
            {
                return true;
            }
            Span<byte> buffer = stackalloc byte[MaxKeyLength];
            return Encoding.UTF8.TryGetBytes(cached, buffer, out int written)
                && buffer[..written].SequenceEqual(utf8);
        }

        private static int ComputeHash(ReadOnlySpan<byte> utf8)
        {
            HashCode hash = default;
            hash.AddBytes(utf8);
            return hash.ToHashCode();
        }
    }
}
