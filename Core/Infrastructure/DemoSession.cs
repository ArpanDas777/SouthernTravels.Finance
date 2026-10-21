namespace SouthernTravels.Core.Infrastructure;

/// <summary>The period filter, shared by every area. It lives in a cookie; there is no sign-in yet.</summary>
public static class DemoSession
{
    public const string PeriodCookie = "fin_period";
    public static readonly string[] PeriodKinds = ["All time", "Today", "This Week", "This Month", "This Quarter", "This Year", "Custom"];

    /// <summary>The from/to dates a period kind stands for; null for "All time".</summary>
    public static (string, string)? Range(string kind, string from, string to)
    {
        var t = Fmt.D(Clock.Today);
        var today = Clock.Today;
        switch (kind)
        {
            case "Today": return (today, today);
            case "This Week": { var dow = ((int)t.DayOfWeek + 6) % 7; return (Fmt.Add(today, -dow), Fmt.Add(today, 6 - dow)); }
            case "This Month": return (Fmt.Ymd(new DateTime(t.Year, t.Month, 1)), Fmt.Ymd(new DateTime(t.Year, t.Month, 1).AddMonths(1).AddDays(-1)));
            case "This Quarter": { var q = new DateTime(t.Year, (t.Month - 1) / 3 * 3 + 1, 1); return (Fmt.Ymd(q), Fmt.Ymd(q.AddMonths(3).AddDays(-1))); }
            case "This Year": return (t.Year + "-01-01", t.Year + "-12-31");
            case "Custom": return (from != "" ? from : "0000-00-00", to != "" ? to : "9999-12-31");
            default: return null;
        }
    }

    /// <summary>The period filter stored in the request's cookie.</summary>
    public static (string Kind, string From, string To) ReadPeriod(HttpRequest r)
    {
        var parts = (r.Cookies[PeriodCookie] ?? "All time||").Split('|');
        return (PeriodKinds.Contains(parts[0]) ? parts[0] : "All time", parts.ElementAtOrDefault(1) ?? "", parts.ElementAtOrDefault(2) ?? "");
    }
}
