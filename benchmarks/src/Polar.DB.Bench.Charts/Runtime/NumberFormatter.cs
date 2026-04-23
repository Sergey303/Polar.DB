using System;
using System.Globalization;
using System.Net;

namespace Polar.DB.Bench.Charts.Runtime;

/// <summary>
/// Number formatting for experiment HTML pages.
/// Scientific notation for large/small values, binary units for bytes,
/// secondary human-readable units (seconds, microseconds) for milliseconds.
/// All formatting rules live in one place.
/// </summary>
internal static class NumberFormatter
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>
    /// Formats a general integer count (e.g. record count) with scientific display.
    /// </summary>
    public static string FormatGeneralNumber(long value)
    {
        var asDouble = (double)value;
        return "<span title=\"raw: " + HtmlEncode(value.ToString(Invariant)) + "\">" +
               FormatScientificWithUnit(asDouble, unit: string.Empty, includeSecondary: false) + "</span>";
    }

    /// <summary>
    /// Formats milliseconds with secondary human-readable unit (seconds or microseconds).
    /// Returns "n/a" span for null.
    /// </summary>
    public static string FormatMilliseconds(double? value)
    {
        if (!value.HasValue)
        {
            return "<span class=\"muted\">n/a</span>";
        }

        var raw = value.Value;
        var main = FormatScientificWithUnit(raw, "ms", includeSecondary: false);
        var abs = Math.Abs(raw);
        if (abs >= 1000.0)
        {
            var seconds = raw / 1000.0d;
            var secondsText = seconds.ToString("0.###", Invariant) + " s";
            return "<span title=\"raw: " + HtmlEncode(raw.ToString("0.###############", Invariant)) + " ms\">" +
                   main + " (" + HtmlEncode(secondsText) + ")</span>";
        }

        if (abs > 0 && abs < 1.0)
        {
            var microsText = (raw * 1000.0d).ToString("0.###", Invariant) + " us";
            return "<span title=\"raw: " + HtmlEncode(raw.ToString("0.###############", Invariant)) + " ms\">" +
                   main + " (" + HtmlEncode(microsText) + ")</span>";
        }

        return "<span title=\"raw: " + HtmlEncode(raw.ToString("0.###############", Invariant)) + " ms\">" +
               main + "</span>";
    }

    /// <summary>
    /// Formats bytes with binary unit (KiB, MiB, GiB) as secondary display.
    /// Returns "n/a" span for null.
    /// </summary>
    public static string FormatBytes(double? value)
    {
        if (!value.HasValue)
        {
            return "<span class=\"muted\">n/a</span>";
        }

        var raw = value.Value;
        var main = FormatScientificWithUnit(raw, "B", includeSecondary: false);
        var binary = FormatBinaryBytes(raw);
        return "<span title=\"raw: " + HtmlEncode(raw.ToString("0.###############", Invariant)) + " B\">" +
               main + " (" + HtmlEncode(binary) + ")</span>";
    }

    /// <summary>
    /// Formats a value in scientific notation with an optional unit suffix.
    /// Uses plain notation for values between 0.01 and 999.999.
    /// </summary>
    public static string FormatScientificWithUnit(double value, string unit, bool includeSecondary)
    {
        var abs = Math.Abs(value);
        var useScientific = abs >= 1000.0 || (abs > 0 && abs < 0.01);
        if (!useScientific)
        {
            var plain = value.ToString("0.###", Invariant);
            return string.IsNullOrWhiteSpace(unit) ? HtmlEncode(plain) : HtmlEncode(plain + " " + unit);
        }

        var exponent = (int)Math.Floor(Math.Log10(abs));
        var mantissa = value / Math.Pow(10, exponent);
        var mantissaText = mantissa.ToString("0.###", Invariant);
        var scientific = HtmlEncode(mantissaText) + " &times; 10<sup>" + exponent.ToString(Invariant) + "</sup>";
        if (!string.IsNullOrWhiteSpace(unit))
        {
            scientific += " " + HtmlEncode(unit);
        }

        return scientific;
    }

    /// <summary>
    /// Formats bytes as human-readable binary unit string (e.g. "150.6 MiB").
    /// </summary>
    public static string FormatBinaryBytes(double bytes)
    {
        var abs = Math.Abs(bytes);
        var units = new[] { "B", "KiB", "MiB", "GiB", "TiB", "PiB" };
        var unitIndex = 0;
        var scaled = abs;
        while (scaled >= 1024.0 && unitIndex < units.Length - 1)
        {
            scaled /= 1024.0;
            unitIndex++;
        }

        var signedScaled = bytes < 0 ? -scaled : scaled;
        var format = unitIndex == 0 ? "0" : "0.###";
        return signedScaled.ToString(format, Invariant) + " " + units[unitIndex];
    }

    /// <summary>
    /// Formats axis tick values for milliseconds charts.
    /// </summary>
    public static string FormatMillisecondsAxis(double value)
    {
        return FormatAxisValue(value, "ms");
    }

    /// <summary>
    /// Formats axis tick values for bytes charts.
    /// </summary>
    public static string FormatBytesAxis(double value)
    {
        return FormatAxisValue(value, "B");
    }

    /// <summary>
    /// Formats axis tick value with scientific notation for large numbers.
    /// </summary>
    public static string FormatAxisValue(double value, string unit)
    {
        var abs = Math.Abs(value);
        var formatted = abs >= 1000.0
            ? value.ToString("0.###e+0", Invariant)
            : value.ToString("0.###", Invariant);
        return formatted + " " + unit;
    }

    /// <summary>
    /// Strips HTML tags from a formatted value for use in plain-text contexts.
    /// </summary>
    public static string StripHtml(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value
            .Replace("<sup>", "^", StringComparison.OrdinalIgnoreCase)
            .Replace("</sup>", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("<span class=\"muted\">", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("<span>", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("</span>", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// HTML-encodes a string value.
    /// </summary>
    public static string HtmlEncode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}
