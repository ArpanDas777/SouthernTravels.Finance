using Microsoft.AspNetCore.Http;

namespace SouthernTravels.Finance.Infrastructure;

/// <summary>
/// Report state carried in the query string: search, view tab, filters (f_*), date range, sort and page.
/// </summary>
public sealed class ReportQuery
{
    public string Path { get; init; } = "";
    public string Q { get; set; } = "";
    public string View { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Sort { get; set; } = "";
    public int Dir { get; set; } = 1;
    public int Page { get; set; } = 1;
    /// <summary>Filter panel open.</summary>
    public bool Fo { get; set; }
    public Dictionary<string, string> F { get; set; } = new();

    public static ReportQuery Parse(HttpRequest r)
    {
        string Get(string k) => r.Query[k].ToString();
        var q = new ReportQuery
        {
            Path = r.Path,
            Q = Get("q"),
            View = Get("view"),
            From = Get("from"),
            To = Get("to"),
            Sort = Get("sort"),
            Dir = Get("dir") == "-1" ? -1 : 1,
            Page = int.TryParse(Get("page"), out var p) && p > 0 ? p : 1,
            Fo = Get("fo") == "1",
        };
        foreach (var kv in r.Query.Where(kv => kv.Key.StartsWith("f_")))
            q.F[kv.Key[2..]] = kv.Value.ToString();
        return q;
    }

    public string Filter(string k) => F.TryGetValue(k, out var v) ? v : "";

    public ReportQuery Clone() => new()
    {
        Path = Path, Q = Q, View = View, From = From, To = To, Sort = Sort, Dir = Dir, Page = Page, Fo = Fo,
        F = new Dictionary<string, string>(F),
    };

    /// <summary>URL for this report with the given changes applied.</summary>
    public string Url(Action<ReportQuery> change)
    {
        var c = Clone();
        change(c);
        return c.ToUrl();
    }

    public string ToUrl()
    {
        var kv = new List<KeyValuePair<string, string?>>();
        void Add(string k, string v) { if (!string.IsNullOrEmpty(v)) kv.Add(new(k, v)); }
        Add("view", View);
        Add("q", Q);
        foreach (var f in F.Where(f => f.Value != "" && f.Value != "All")) Add("f_" + f.Key, f.Value);
        Add("from", From);
        Add("to", To);
        Add("sort", Sort);
        if (Sort != "" && Dir < 0) Add("dir", "-1");
        if (Page > 1) Add("page", Page.ToString());
        if (Fo) Add("fo", "1");
        return Path + QueryString.Create(kv);
    }
}
