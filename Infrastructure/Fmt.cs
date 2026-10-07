using System.Globalization;

namespace SouthernTravels.Finance.Infrastructure;

/// <summary>Formatting helpers shared by reports and views. Dates are ISO strings ("yyyy-MM-dd" or "yyyy-MM-dd HH:mm").</summary>
public static class Fmt
{
    public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static DateTime D(string s) => DateTime.ParseExact(s[..10], "yyyy-MM-dd", Inv);
    public static string Ymd(DateTime d) => d.ToString("yyyy-MM-dd", Inv);
    public static string Add(string s, int n) => Ymd(D(s).AddDays(n));
    public static int Days(string a, string b) => (int)Math.Round((D(b) - D(a)).TotalDays);
    public static int Round(double x) => (int)Math.Round(x, MidpointRounding.AwayFromZero);

    /// <summary>"18 Aug 26"</summary>
    public static string Fd(string? s) => string.IsNullOrEmpty(s) ? "-" : D(s).ToString("d MMM yy", Inv);

    /// <summary>"18-Aug-2026" (printed documents)</summary>
    public static string Dmy(string? s) => string.IsNullOrEmpty(s) ? "-" : D(s).ToString("dd-MMM-yyyy", Inv);

    /// <summary>"18-Aug-2026, 03:10 PM" (printed documents)</summary>
    public static string DmyTime(string s) =>
        s.Length > 10 ? DateTime.ParseExact(s[..16], "yyyy-MM-dd HH:mm", Inv).ToString("dd-MMM-yyyy, hh:mm tt", Inv) : Dmy(s);

    /// <summary>Indian grouping without the rupee sign: 1,06,500</summary>
    public static string Amt(double n) => Inr(n)[1..];

    /// <summary>"18 Aug 26, 15:10"</summary>
    public static string Fdt(string? s) =>
        string.IsNullOrEmpty(s) ? "-" : Fd(s) + (s.Length > 10 ? ", " + s.Substring(11, 5) : "");

    /// <summary>Rupees with Indian digit grouping: ₹1,23,456.</summary>
    public static string Inr(double n) => "₹" + Math.Round(n, MidpointRounding.AwayFromZero).ToString("N0", EnIn);

    /// <summary>en-IN groups digits 3;2 (lakh / crore).</summary>
    static readonly CultureInfo EnIn = CultureInfo.GetCultureInfo("en-IN");

    public static string Pct(double a, double b) => b != 0 ? (a / b * 100).ToString("0.0", Inv) + "%" : "-";

    public static string Signed(int n) => (n > 0 ? "+" : n < 0 ? "−" : "") + Inr(Math.Abs(n));

    public static string Css(double n) => n.ToString(Inv);

    public static int Cmp(string a, string b) => string.CompareOrdinal(a, b);
}
