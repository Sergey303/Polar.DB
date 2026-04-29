using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Polar.DB.Bench.Engine.PolarDb;

/// <summary>
/// Lookup helpers for the current in-repository Polar.DB adapter.
/// Old NuGet adapters must not reference this file.
/// </summary>
internal static class PolarDbCurrentLookup
{
    public static IReadOnlyList<object> FindAll(USequence sequence, object key)
    {
        if (sequence == null) throw new ArgumentNullException(nameof(sequence));

        var comparableKey = ToSupportedKey(key);
        return sequence.GetAllByKey(comparableKey).ToArray();
    }

    public static object FindExactlyOne(USequence sequence, object key)
    {
        if (sequence == null) throw new ArgumentNullException(nameof(sequence));

        var comparableKey = ToSupportedKey(key);
        return sequence.GetExactlyOneByKey(comparableKey);
    }

    public static IComparable ToSupportedKey(object key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        return key switch
        {
            int value => value,
            long value => value,
            Guid value => value,
            string value when Guid.TryParse(value, out var parsed) => parsed,
            string value when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            string value when long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => throw new NotSupportedException(
                $"Polar.DB current adapter lookup supports only int, long and Guid keys. Actual key type: {key.GetType().FullName}.")
        };
    }

    public static int HashSupportedKey(IComparable key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        return key switch
        {
            int value => value,
            long value => HashInt64(value),
            Guid value => HashGuid(value),
            _ => throw new NotSupportedException(
                $"Polar.DB current adapter lookup supports only int, long and Guid keys. Actual key type: {key.GetType().FullName}.")
        };
    }

    private static int HashInt64(long value)
    {
        unchecked
        {
            return ((int)value) ^ (int)(value >> 32);
        }
    }

    private static int HashGuid(Guid value)
    {
        var bytes = value.ToByteArray();
        unchecked
        {
            var hash = 17;
            for (var i = 0; i < bytes.Length; i++)
            {
                hash = (hash * 31) + bytes[i];
            }

            return hash;
        }
    }
}
