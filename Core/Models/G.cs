using System.Text.Json;
using SouthernTravels.Core.Infrastructure;

namespace SouthernTravels.Core.Models;

/// <summary>Builders for grid cells and lines, mirroring the visual vocabulary of the design.</summary>
public static class G
{
    public const string Red = "var(--danger-ink)", Green = "var(--ok-ink)", Blue = "var(--calc-ink)", Mut = "var(--ink-faint)",
        Link = "var(--link)", Warn = "var(--warn-ink-deep)", Strong = "var(--ink-strong)", Ink = "var(--ink)";

    public static Col Th(string l, string al = "left", string? tip = null) => new() { L = l, Align = al, Tip = tip };

    /// <summary>Columns from strings ("Label") or tuples (("Label", "right")).</summary>
    public static List<Col> Cols(params object[] a) =>
        a.Select(x => x is ValueTuple<string, string, string> t3 ? Th(t3.Item1, t3.Item2, t3.Item3) : x is ValueTuple<string, string> t ? Th(t.Item1, t.Item2) : Th((string)x)).ToList();

    /// <summary>Marks the last column as holding actions (the card footer on compact screens).</summary>
    public static List<Col> ActionLast(List<Col> c) =>
        c.Select((x, i) => i < c.Count - 1 ? x : new Col { L = x.L, Align = x.Align, Tip = x.Tip, SortUrl = x.SortUrl, SortActive = x.SortActive, Action = true }).ToList();

    public static Cell C(params Line?[] lines) => new() { Lines = lines.OfType<Line>().ToList() };
    public static Cell Ca(string al, params Line?[] lines) => new() { Lines = lines.OfType<Line>().ToList(), Align = al };
    public static Cell Empty => new();

    public static string? TipJson(IEnumerable<TipLine>? lines) => lines == null ? null : JsonSerializer.Serialize(lines);

    public static Line T(string text, string? c = null, int w = 400, double s = 12, bool num = false, bool it = false,
        bool dot = false, IEnumerable<TipLine>? tip = null, string? x = null)
    {
        var st = $"color:{c ?? Ink};font-weight:{w};font-size:{Fmt.Css(s)}px;" +
                 (num ? "font-family:var(--font-num);font-variant-numeric:tabular-nums;white-space:nowrap;" : "white-space:normal;") +
                 (text.Length > 14 && !text.Contains(' ') ? "overflow-wrap:anywhere;" : "overflow-wrap:normal;word-break:normal;") +
                 (it ? "font-style:italic;" : "") +
                 (dot ? "border-bottom:1px dotted var(--ink-faint);cursor:help;" : "") + x;
        return new Line { Kind = LineKind.Text, Text = text, Style = st, Tip = TipJson(tip) };
    }

    public static Cell Txt(string al, string text, string? c = null, int w = 400, double s = 12, bool num = false, bool it = false) =>
        Ca(al, T(text, c, w, s, num, it));

    public static Line Chip(string text, string tone, IEnumerable<TipLine>? tip = null) =>
        new() { Kind = LineKind.Chip, Text = text, Tone = tone, Tip = TipJson(tip) };

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
            "X" => ("danger", "✕", "Rejected"),
            _ => ("off", "–", "Not reached"),
        };
        return new Line { Kind = LineKind.Lv, Lvl = lvl, Glyph = glyph, Text = lvl + " " + word, Tone = tone, Tip = TipJson(tip) };
    }

    static readonly Dictionary<string, string> BtnIcons = new()
    {
        ["Upload"] = "upload", ["Reupload Bill"] = "reupload", ["View Bill Details"] = "eye", ["View"] = "eye", ["View Payment Details"] = "eye",
        ["Print"] = "print", ["Print Advice"] = "print", ["Print Cheque"] = "cheque", ["Add Payment Details"] = "card", ["Update Payment Details"] = "card",
        ["Update"] = "card", ["View Bill"] = "eye", ["View Docs"] = "docs", ["History"] = "history", ["Approve"] = "check", ["Reject"] = "x",
    };

    public static Line Btn(string text, string? pop = null, string variant = "default", bool dis = false, string? toast = null) =>
        new() { Kind = LineKind.Btn, Text = text, Pop = pop, ToastMsg = toast, Variant = variant, Disabled = dis, Icon = BtnIcons.GetValueOrDefault(text) };

    /// <summary>A button-styled link that opens in a new tab (e.g. a printable document).</summary>
    public static Line LinkBtn(string text, string href, string variant = "default", bool dis = false) =>
        new() { Kind = LineKind.Btn, Text = text, Href = dis ? null : href, Variant = variant, Disabled = dis, Icon = BtnIcons.GetValueOrDefault(text) };

    /// <summary>A button that POSTs to <paramref name="act"/> (with the page's antiforgery token) and reloads; site.js handles it.</summary>
    public static Line ActBtn(string text, string act, string variant = "default") =>
        new() { Kind = LineKind.Btn, Text = text, Act = act, Variant = variant, Icon = BtnIcons.GetValueOrDefault(text) };

    /// <summary>An icon-only button (no visible text, no tooltip) that opens a pop-up or POSTs.</summary>
    public static Line IconBtn(string icon, string aria, string? pop = null, string? act = null, string variant = "default", bool dis = false) =>
        new() { Kind = LineKind.Btn, Text = "", Pop = pop, Act = act, Variant = variant, Icon = icon, Disabled = dis, Meta = aria };

    /// <summary>An underlined link inside a cell that opens a pop-up, or (with no pop-up) shows a toast.</summary>
    public static Line Lk(string text, string? pop = null, string? toast = null, double s = 12, int w = 400) =>
        new() { Kind = LineKind.Link, Text = text, Pop = pop, ToastMsg = toast, Style = $"font-size:{Fmt.Css(s)}px;font-weight:{w};" };

    /// <summary>The SVG paths (24×24, stroked) of a button icon.</summary>
    public static string IconPath(string name) => name switch
    {
        "upload" => "<path d=\"M12 16V4M7 9l5-5 5 5M4 20h16\"/>",
        "reupload" => "<path d=\"M4 12a8 8 0 0 1 14-5.3M20 4v4h-4M20 12a8 8 0 0 1-14 5.3M4 20v-4h4\"/>",
        "eye" => "<path d=\"M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12z\"/><circle cx=\"12\" cy=\"12\" r=\"3\"/>",
        "print" => "<path d=\"M7 9V3h10v6M7 17H4v-7h16v7h-3M7 14h10v7H7z\"/>",
        "cheque" => "<path d=\"M3 6h18v12H3zM6 10h6M6 14h4M15 14h3\"/>",
        "history" => "<path d=\"M3 12a9 9 0 1 0 3-6.7M3 4v5h5M12 7v5l3 2\"/>",
        "check" => "<path d=\"M20 6 9 17l-5-5\"/>",
        "x" => "<path d=\"M18 6 6 18M6 6l12 12\"/>",
        "docs" => "<path d=\"M6 3h9l4 4v14H6zM14 3v5h5M9 13h7M9 17h7\"/>",
        "card" => "<path d=\"M3 6h18v12H3zM3 10h18M7 15h3\"/>",
        _ => "<circle cx=\"12\" cy=\"12\" r=\"9\"/>",
    };

    /// <summary>A drop-down that posts the new value (as "mode", with the row key as "sid") as soon as it changes.</summary>
    public static Line Select(string chosen, IEnumerable<string> opts, string postUrl, string key, string label) =>
        new() { Kind = LineKind.Select, Text = chosen, Opts = opts.ToList(), Pop = postUrl, Value = key, Meta = label };

    public static Line Dot(IEnumerable<TipLine> tip) => new() { Kind = LineKind.Dot, Tip = TipJson(tip) };
}
