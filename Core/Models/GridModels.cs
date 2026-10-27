using SouthernTravels.Core.Infrastructure;
namespace SouthernTravels.Core.Models;

/* ---------- report ---------- */

public sealed record Kpi(string Label, string Value, string Color);
public sealed record TabVm(string Label, string Url, bool Active);
public sealed record FilterVm(string Name, string Label, string Value, List<string> Opts);
/// <summary>Pager button; a null Url means disabled.</summary>
public sealed record PageBtn(string Label, string? Url, bool Active);
public sealed record PagerVm(string Label, List<PageBtn> Pages, List<PageBtn> Sizes);
public sealed record ToolBtn(string Label, string Pop, bool Primary = false);
/// <summary>Bulk Payment toolbar: <paramref name="ProceedPopup"/> takes the ticked ids; the history button opens <paramref name="HistoryPopup"/>.</summary>
public sealed record BulkBar(string ProceedPopup, string HistoryPopup, int HistoryCount, string StartLabel = "Bulk Payment", string HistoryLabel = "Bulk Payment History",
    string Group = "advbulk", bool ExpandAll = false);

public sealed class ReportVm
{
    public required string Title { get; set; }
    public required string Desc { get; init; }
    public bool Pill { get; init; }
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
    /// <summary>Extra toolbar buttons that open a pop-up (e.g. RTGS Advice History, Create RTGS Advice).</summary>
    public List<ToolBtn> Tools { get; init; } = [];
    /// <summary>True when ticked services collect in the cart panel (Approved Vendor Payable). Otherwise a bulk action button appears in the toolbar.</summary>
    public bool Cart { get; init; }
    /// <summary>Advance Report: toolbar for Bulk Payment (the bulk tick column, Proceed / Cancel and the history button); null elsewhere.</summary>
    public BulkBar? Bulk { get; init; }
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
    /// <summary>Holds action buttons: on phones this column becomes the card footer.</summary>
    public bool Action { get; init; }
    /// <summary>Tooltip on the column heading.</summary>
    public string? Tip { get; init; }
    /// <summary>Extra CSS class on the heading and its cells (e.g. "bulk-col": shown only in bulk mode).</summary>
    public string? Cls { get; init; }
}

public enum LineKind { Text, Dot, Chip, Pill, Lv, Btn, Chk, Select, Link }

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
    /// <summary>Secondary line and amount for a selection checkbox, listed in the compact cart.</summary>
    public string? Sub { get; init; }
    public int Amount { get; init; }
    /// <summary>Cart details of a selection checkbox: vendor index/ID, ticket MBK, service name, and the meta line.</summary>
    public int Vi { get; init; }
    public string? Vid { get; init; }
    /// <summary>Vendor legal name, shown under the vendor in the cart.</summary>
    public string? Legal { get; init; }
    public string? Mbk { get; init; }
    public string? Svc { get; init; }
    public string? Meta { get; init; }
    public string? DotColor { get; init; }
    /// <summary>Icon for a button (shown instead of the text on wide screens; the text is its tooltip).</summary>
    public string? Icon { get; init; }
    /// <summary>A button that POSTs to this URL and reloads (see ActBtn).</summary>
    public string? Act { get; init; }
    /// <summary>Select: the choices (Text is the chosen one, Pop the URL it posts to, Value the row key and Meta the label).</summary>
    public List<string>? Opts { get; init; }
}

public sealed class Cell
{
    public List<Line> Lines { get; init; } = [];
    public string Align { get; init; } = "left";
    public string? Cls { get; init; }
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
    /// <summary>Leading cells forming the card header on phones (selection tick + first two fields; 2 when there is no tick).</summary>
    public int CardHead { get; init; } = 3;
}

public sealed class Row
{
    public string Key { get; init; } = "";
    public List<Cell> Cells { get; init; } = [];
    public Detail? Detail { get; init; }
    /// <summary>Vendor id whose red dot is cleared when the row is first expanded; SeenArea is "pr" or "avp".</summary>
    public string? SeenVendor { get; init; }
    public string SeenArea { get; init; } = "pr";
    /// <summary>Leading cells (the chevron included) that form the card header on compact screens: chevron + first two fields.</summary>
    public int CardHead { get; init; } = 3;
}

