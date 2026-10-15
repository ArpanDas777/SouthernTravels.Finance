using System.Text.Json;
using SouthernTravels.Core.Infrastructure;

namespace SouthernTravels.Core.Models;

/// <summary>Builders for grid cells and lines, mirroring the visual vocabulary of the design.</summary>
public static class G
{
    public const string Red = "var(--danger-ink)", Green = "var(--ok-ink)", Blue = "var(--calc-ink)", Mut = "var(--ink-faint)",
        Link = "var(--link)", Warn = "var(--warn-ink-deep)", Strong = "var(--ink-strong)", Ink = "var(--ink)";

    public static Col Th(string l, string al = "left") => new() { L = l, Align = al };

    /// <summary>Columns from strings ("Label") or tuples (("Label", "right")).</summary>
    public static List<Col> Cols(params object[] a) =>
        a.Select(x => x is ValueTuple<string, string> t ? Th(t.Item1, t.Item2) : Th((string)x)).ToList();

    /// <summary>Marks the last column as holding actions (the card footer on compact screens).</summary>
    public static List<Col> ActionLast(List<Col> c) =>
        c.Select((x, i) => i < c.Count - 1 ? x : new Col { L = x.L, Align = x.Align, SortUrl = x.SortUrl, SortActive = x.SortActive, Action = true }).ToList();

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

    public static Line Dot(IEnumerable<TipLine> tip) => new() { Kind = LineKind.Dot, Tip = TipJson(tip) };

}
