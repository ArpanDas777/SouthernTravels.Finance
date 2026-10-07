using System.Text.Json;
using SouthernTravels.Finance.Infrastructure;

namespace SouthernTravels.Finance.Models;

/* ---------- layout ---------- */

public sealed class LayoutCtx
{
    public string User { get; init; } = "admin";
    public string UserName { get; init; } = "";
    public string UserRole { get; init; } = "";
    public bool Perm => User != "vijay";
    public string Page { get; set; } = "VendorPayable";
    public string PeriodKind { get; init; } = "All time";
    public string PeriodFrom { get; init; } = "";
    public string PeriodTo { get; init; } = "";
    public string? PeriodText { get; init; }
    public string PeriodField { get; set; } = "Bill Date";
    public string? Toast { get; set; }
}

/* ---------- report ---------- */

public sealed record Kpi(string Label, string Value, string Color);
public sealed record TabVm(string Label, string Url, bool Active);
public sealed record FilterVm(string Name, string Label, string Value, List<string> Opts);
/// <summary>Pager button; a null Url means disabled.</summary>
public sealed record PageBtn(string Label, string? Url, bool Active);
public sealed record PagerVm(string Label, List<PageBtn> Pages);

public sealed class ReportVm
{
    public required string Title { get; init; }
    public required string Desc { get; init; }
    public bool Pill { get; init; }
    public bool Perm { get; init; }
    public int? KpiCols { get; init; }
    public List<Kpi> Kpis { get; init; } = [];
    public List<TabVm>? Tabs { get; init; }
    public required ReportQuery Query { get; init; }
    public string Qph { get; init; } = "";
    public List<FilterVm>? Filters { get; init; }
    public int FCount { get; init; }
    public string? DateLabel { get; init; }
    public string Count { get; init; } = "";
    /// <summary>Bulk action label ("Raise Bill" / "Submit"); null hides it.</summary>
    public string? SelLabel { get; init; }
    public string? SelPopup { get; init; }
    public bool HasExport { get; init; } = true;
    public bool HasExpand { get; init; } = true;
    public List<Col> Cols { get; init; } = [];
    public List<Row> Rows { get; init; } = [];
    public string EmptyText { get; init; } = "";
    public PagerVm? Pager { get; init; }
    public string ClearAllUrl { get; init; } = "";
}

/* ---------- grid ---------- */

public sealed record TipLine(string l, string v = "", bool b = false, string? c = null, bool rule = false, bool wide = false);

public sealed class Col
{
    public string L { get; init; } = "";
    public string Align { get; init; } = "left";
    public string? SortUrl { get; init; }
    public bool SortActive { get; init; }
}

public enum LineKind { Text, Dot, Chip, Pill, Lv, Btn, Chk }

public sealed class Line
{
    public LineKind Kind { get; init; }
    public string Text { get; init; } = "";
    public string? Style { get; init; }
    public string? Tip { get; init; }
    public string? Tone { get; init; }
    public string? Lvl { get; init; }
    public string? Glyph { get; init; }
    public string? Variant { get; init; }
    public bool Disabled { get; init; }
    public string? Pop { get; init; }
    public string? ToastMsg { get; init; }
    public string? Href { get; init; }
    public string? Value { get; init; }
    public string? Group { get; init; }
    public string? DotColor { get; init; }
}

public sealed class Cell
{
    public List<Line> Lines { get; init; } = [];
    public string Align { get; init; } = "left";
}

public sealed class Fact
{
    public string L { get; init; } = "";
    public string V { get; init; } = "";
    public string? Color { get; init; }
    public bool Bold { get; init; }
    public bool Full { get; init; }
    public bool Num { get; init; }
}

public sealed class DocTile
{
    public string Name { get; init; } = "";
    public bool Received { get; init; }
    public string? Pop { get; init; }
}

public sealed class Panel
{
    public string Title { get; init; } = "";
    public string Strip { get; init; } = "var(--action)";
    public List<Fact> Facts { get; init; } = [];
    public List<DocTile>? Docs { get; init; }
}

public sealed class Detail
{
    public List<Col>? Cols { get; init; }
    public List<List<Cell>>? Rows { get; init; }
    public List<Panel>? Panels { get; init; }
    public string PanelCols { get; init; } = "1fr 1fr 1fr";
}

public sealed class Row
{
    public string Key { get; init; } = "";
    public List<Cell> Cells { get; init; } = [];
    public Detail? Detail { get; init; }
    /// <summary>Vendor id whose "new tickets" dot is cleared when the row is first expanded.</summary>
    public string? SeenVendor { get; init; }
}

/// <summary>Builders for grid cells and lines, mirroring the visual vocabulary of the design.</summary>
public static class G
{
    public const string Red = "var(--danger-ink)", Green = "var(--ok-ink)", Blue = "var(--calc-ink)", Mut = "var(--ink-faint)",
        Link = "var(--link)", Warn = "var(--warn-ink-deep)", Strong = "var(--ink-strong)", Ink = "var(--ink)";

    public static Col Th(string l, string al = "left") => new() { L = l, Align = al };

    /// <summary>Columns from strings ("Label") or tuples (("Label", "right")).</summary>
    public static List<Col> Cols(params object[] a) =>
        a.Select(x => x is ValueTuple<string, string> t ? Th(t.Item1, t.Item2) : Th((string)x)).ToList();

    public static Cell C(params Line?[] lines) => new() { Lines = lines.OfType<Line>().ToList() };
    public static Cell Ca(string al, params Line?[] lines) => new() { Lines = lines.OfType<Line>().ToList(), Align = al };
    public static Cell Empty => new();

    public static string? TipJson(IEnumerable<TipLine>? lines) => lines == null ? null : JsonSerializer.Serialize(lines);

    public static Line T(string text, string? c = null, int w = 400, double s = 12, bool num = false, bool it = false,
        bool dot = false, IEnumerable<TipLine>? tip = null, string? x = null)
    {
        var st = $"color:{c ?? Ink};font-weight:{w};font-size:{Fmt.Css(s)}px;" +
                 (num ? "font-family:var(--font-num);font-variant-numeric:tabular-nums;white-space:nowrap;" : "white-space:normal;") +
                 "overflow-wrap:normal;word-break:normal;" +
                 (it ? "font-style:italic;" : "") +
                 (dot ? "border-bottom:1px dotted var(--ink-faint);cursor:help;" : "") + x;
        return new Line { Kind = LineKind.Text, Text = text, Style = st, Tip = TipJson(tip) };
    }

    public static Cell Txt(string al, string text, string? c = null, int w = 400, double s = 12, bool num = false, bool it = false) =>
        Ca(al, T(text, c, w, s, num, it));

    public static Line Chip(string text, string tone, IEnumerable<TipLine>? tip = null) =>
        new() { Kind = LineKind.Chip, Text = text, Tone = tone, Tip = TipJson(tip) };

    /// <summary>CSS suffix for a service type's colour on printed advice documents.</summary>
    public static string SvcKey(string type) => type switch
    {
        "Accommodation" => "accommodation", "Transport" => "transport", "Guide" => "guide", "En-Route Meals" => "enroute-meals", _ => "other",
    };

    public static Line Pill(string type)
    {
        var k = type switch { "Accommodation" => 5, "Transport" => 1, "Guide" => 2, "En-Route Meals" => 6, _ => 3 };
        return new Line { Kind = LineKind.Pill, Text = type, DotColor = "var(--chart-" + k + ")" };
    }

    /// <summary>Approval level badge (L1 / L2 / Mgmt) with status A / Q / X / -.</summary>
    public static Line Lv(string lvl, string st, IEnumerable<TipLine>? tip = null)
    {
        var (tone, glyph, word) = st switch
        {
            "A" => ("ok", "✓", "Approved"),
            "Q" => ("warn", "!", "Waiting"),
            "X" => ("danger", "!", "Rejected"),
            _ => ("off", "–", "Not reached"),
        };
        return new Line { Kind = LineKind.Lv, Lvl = lvl, Glyph = glyph, Text = lvl + " " + word, Tone = tone, Tip = TipJson(tip) };
    }

    public static Line Btn(string text, string? pop = null, string variant = "default", bool dis = false, string? toast = null) =>
        new() { Kind = LineKind.Btn, Text = text, Pop = pop, ToastMsg = toast, Variant = variant, Disabled = dis };

    /// <summary>A button-styled link that opens in a new tab (e.g. a printable document).</summary>
    public static Line LinkBtn(string text, string href, string variant = "default") =>
        new() { Kind = LineKind.Btn, Text = text, Href = href, Variant = variant };

    public static string AdviceUrl(string sid) => "/Finance/AdvanceAdvice?sid=" + Uri.EscapeDataString(sid);

    public static string PaymentAdviceUrl(int vi, IEnumerable<string> sids) =>
        "/Finance/PaymentAdvice?vi=" + vi + "&sids=" + Uri.EscapeDataString(string.Join(",", sids));

    public static Line Chk(string value, string group) => new() { Kind = LineKind.Chk, Value = value, Group = group };

    public static Line Dot(IEnumerable<TipLine> tip) => new() { Kind = LineKind.Dot, Tip = TipJson(tip) };

    public static string ChipTone(string s) => s switch
    {
        "Confirmed" or "Paid" or "Approved" or "Full Payment" => "ok",
        "Pending" or "Part Payment" => "warn",
        "Cancelled" => "danger",
        _ => "off",
    };

    public static List<TipLine>? ApTip(Approval? l) =>
        l is { St: "A" } ? [new(l.By ?? "", "", b: true), new(Fmt.Fdt(l.At))] : null;

    public static string Pop(string t, params (string k, object v)[] args) =>
        "/Finance/Popup?t=" + t + string.Concat(args.Select(a => "&" + a.k + "=" + Uri.EscapeDataString(Convert.ToString(a.v, Fmt.Inv) ?? "")));
}
