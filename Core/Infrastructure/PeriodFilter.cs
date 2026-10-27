namespace SouthernTravels.Core.Infrastructure;

/// <summary>The period filter, shared by every area. It lives in a cookie; the picker offers Today, This Month and Custom (Clear = All time).</summary>
public static class PeriodFilter
{
    public const string PeriodCookie = "fin_period";
    public static readonly string[] PeriodKinds = ["All time", "Today", "This Month", "Custom"];

    /// <summary>The from/to dates a period kind stands for; null for "All time". This Month runs from the 1st to today.</summary>
    public static (string, string)? Range(string kind, string from, string to)
    {
        var today = Clock.Today;
        switch (kind)
        {
            case "Today": return (today, today);
            case "This Month": { var t = Fmt.D(today); return (Fmt.Ymd(new DateTime(t.Year, t.Month, 1)), today); }
            case "Custom": return (from != "" ? from : "0000-00-00", to != "" ? to : "9999-12-31");
            default: return null;
        }
    }

    /// <summary>The period filter stored in the request's cookie.</summary>
    public static (string Kind, string From, string To) ReadPeriod(HttpRequest r)
    {
        var parts = (r.Cookies[PeriodCookie] ?? "This Month||").Split('|');
        return (PeriodKinds.Contains(parts[0]) ? parts[0] : "This Month", parts.ElementAtOrDefault(1) ?? "", parts.ElementAtOrDefault(2) ?? "");
    }
}
