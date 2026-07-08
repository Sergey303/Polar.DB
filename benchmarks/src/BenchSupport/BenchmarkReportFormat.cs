using System.Globalization;

namespace PolarDbBenchmarks;

internal static class BenchmarkReportFormat
{
    private static readonly NumberFormatInfo NumberFormat = new()
    {
        NumberDecimalSeparator = ".",
        NumberGroupSeparator = " "
    };

    public static string Css() =>
        "body{font-family:Segoe UI,Arial,sans-serif;margin:32px}" +
        "table{border-collapse:collapse;margin:12px 0}td,th{border:1px solid #ddd;padding:6px 10px}" +
        "th{background:#f4f4f4}.ok{color:#177245}.warn{color:#9a6700}.win{background:#dff3df;font-weight:600}";

    public static string Cell(double value, double best, string suffix)
    {
        if (double.IsNaN(value)) return "<td>N/A</td>";
        if (IsBest(value, best)) return "<td class=\"win\">" + Number(value) + suffix + "</td>";
        if (best == 0.0) return "<td>" + Number(value) + suffix + "</td>";
        return "<td>" + Number(value) + suffix + " ×" + Number(value / best) + "</td>";
    }

    public static string HigherBetterCell(double value, double best, string suffix)
    {
        if (double.IsNaN(value)) return "<td>N/A</td>";
        if (IsBest(value, best)) return "<td class=\"win\">" + Number(value) + suffix + "</td>";
        return "<td>" + Number(value) + suffix + " ×" + Number(best / value) + "</td>";
    }

    public static string ByteCell(long value, long best)
    {
        if (best <= 0 || value <= best) return "<td class=\"win\">" + Bytes(value) + "</td>";
        return "<td>" + Bytes(value) + " ×" + Number((double)value / best) + "</td>";
    }

    public static string HigherBetterLongCell(long value, long best, string suffix)
    {
        if (value == best) return "<td class=\"win\">" + Long(value) + suffix + "</td>";
        return "<td>" + Long(value) + suffix + " ×" + Number((double)best / value) + "</td>";
    }

    public static string RatioCell(long value, long available)
    {
        if (available <= 0) return "<td>N/A</td>";
        var ratio = (double)value / available;
        var css = ratio >= 1.0 ? "warn" : "ok";
        return "<td><span class=\"" + css + "\">" + Number(ratio) + "× available</span></td>";
    }

    public static string Bytes(long value)
    {
        string[] units = { "B", "KiB", "MiB", "GiB", "TiB" };
        var size = (double)value;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return Number(size) + " " + units[unit];
    }

    public static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    public static string Number(double value) => value.ToString("#,0.###", NumberFormat);

    public static string Long(long value) => value.ToString("#,0", NumberFormat);

    public static string Unsigned(ulong value) => value.ToString("#,0", NumberFormat);

    private static bool IsBest(double value, double best) =>
        Math.Abs(value - best) <= Math.Max(0.000001, Math.Abs(best) * 0.000001);
}
