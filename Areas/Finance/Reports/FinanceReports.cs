using SouthernTravels.Areas.Finance.Data;
using SouthernTravels.Areas.Finance.Models;
using SouthernTravels.Core.Infrastructure;
using SouthernTravels.Core.Models;
using static SouthernTravels.Core.Models.G;
using static SouthernTravels.Areas.Finance.Models.FinanceG;

namespace SouthernTravels.Areas.Finance.Reports;

public sealed record FDef<TRow>(string K, string L, Func<TRow, string> Get, string[]? Opts = null);

/// <summary>Builds the five Finance reports from the store for the current query, user and period.</summary>
public sealed partial class FinanceReports(FinanceStore st, ReportQuery q, (string From, string To)? period, bool readOnly = false, bool approver = false)
{
    static readonly Dictionary<string, string> BillTxt = new() { ["R"] = "Uploaded", ["N"] = "Pending", ["D"] = "Disputed" };
    static readonly Dictionary<string, string> BillTone = new() { ["R"] = "ok", ["N"] = "off", ["D"] = "danger" };
    static string OpsTxt(char c) => c switch { 'N' => "New", 'I' => "In Progress", _ => "Completed" };
    static readonly string[] Types = ["Accommodation", "Transport", "Guide", "En-Route Meals", "Other"];

    /* ---------- shared helpers ---------- */

    bool PerOk(string d) => period is not { } r || (Fmt.Cmp(d[..10], r.From) >= 0 && Fmt.Cmp(d[..10], r.To) <= 0);

    bool SearchOk(params string[] parts)
    {
        var s = q.Q.Trim();
        return s == "" || string.Join(" ", parts).Contains(s, StringComparison.OrdinalIgnoreCase);
    }

    List<TRow> ApplyF<TRow>(IEnumerable<TRow> rows, List<FDef<TRow>> defs) =>
        rows.Where(x => defs.All(d => { var f = q.Filter(d.K); return f == "" || f == "All" || d.Get(x) == f; })).ToList();

    List<FilterVm> MkFilters<TRow>(List<TRow> rows, List<FDef<TRow>> defs) =>
        defs.Select(d => new FilterVm("f_" + d.K, d.L, q.Filter(d.K) is { Length: > 0 } v ? v : "All",
            ["All", .. d.Opts ?? rows.Select(d.Get).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray()]))
            .ToList();

    int FCount<TRow>(List<FDef<TRow>> defs, bool dates) =>
        defs.Count(d => q.Filter(d.K) is { Length: > 0 } f && f != "All") + (dates && (q.From != "" || q.To != "") ? 1 : 0);

    /// <summary>Page of <paramref name="items"/> for the query's page and size, with the pager (page buttons and the 10/25/50/100 size choice).</summary>
    (List<T> Page, PagerVm Pager) Paged<T>(List<T> items)
    {
        int per = q.Size, total = items.Count, n = Math.Max(1, (total + per - 1) / per), pg = Math.Min(q.Page, n);
        PageBtn Mk(string label, int to, bool act, bool dis) => new(label, dis ? null : q.Url(c => c.Page = to), act);
        var pages = new List<PageBtn> { Mk("‹", pg - 1, false, pg == 1) };
        pages.AddRange(Enumerable.Range(1, n).Select(i => Mk(i.ToString(), i, i == pg, false)));
        pages.Add(Mk("›", pg + 1, false, pg == n));
        var sizes = ReportQuery.PageSizes.Select(z => new PageBtn(z.ToString(), q.Url(c => { c.Size = z; c.Page = 1; }), z == per)).ToList();
        int a = total > 0 ? (pg - 1) * per + 1 : 0, b = Math.Min(total, pg * per);
        return (items.Skip((pg - 1) * per).Take(per).ToList(), new PagerVm($"Showing {a}–{b} of {total}", pages, sizes));
    }

    List<TabVm> ViewTabs(string view) =>
    [
        new("Vendor Wise", q.Url(c => { c.View = "vendor"; c.Page = 1; }), view == "vendor"),
        new("Ticket Wise", q.Url(c => { c.View = "ticket"; c.Page = 1; }), view == "ticket"),
    ];

    string ClearAll => q.Url(c => { c.F.Clear(); c.Q = ""; c.From = ""; c.To = ""; c.Page = 1; });

    static Kpi K(string label, object value, string color) => new(label, Convert.ToString(value, Fmt.Inv) ?? "", color);

    static Cell Dates(Svc s) => C(T(Fmt.Fd(s.Start), num: true), T("to " + Fmt.Fd(s.End), Mut, s: 11, num: true));

    static Line Sub(string text, string? c = null) => T(text, c ?? Mut, s: 11);

    /// <summary>"Approved"/"Paid" chip only when all items qualify; otherwise "Pending". Tooltip shows the count.</summary>
    static Line AllOrPending(int done, int total, string doneLabel, string verb) =>
        done == total ? Chip(doneLabel, "ok") : Chip("Pending", "warn", [new TipLine($"{done} of {total} {verb}")]);

    static Panel Pan(string title, string strip, IEnumerable<Fact> facts) => new() { Title = title, Strip = strip, Facts = facts.ToList() };


    /* ---------- shared cells (vendor, service, ticket) ---------- */

    static string StatusColor(string status) => status switch { "Confirmed" or "Paid" => Green, "Cancelled" => Red, _ => Warn };

    /// <summary>Ticket status as bold coloured text (no pill).</summary>
    static Line StatusText(string status) => T(status, StatusColor(status), 700, 11);

    /// <summary>Vendor name with the legal name beneath it; neither wraps, so they never overlap the next column.</summary>
    static Line?[] VendorLines(Vendor v, bool id = false) =>
        [T(v.Name, Strong, 600, x: "white-space:nowrap;"), T(v.LegalName, Mut, s: 11, x: "white-space:nowrap;"), id ? T(v.Id, Link, 700, 11) : null];

    static Cell VendorCell(Vendor v, bool id = false) => C(VendorLines(v, id));

    /// <summary>
    /// Service name / details / confirmation no. Accommodation shows the hotel (bold), then "MEAL · Room category · nN" (hover for the full meal plan),
    /// then the occupancy; every other type shows the plain service name.
    /// </summary>
    static Line[] SvcLines(Svc s, bool conf = true)
    {
        var lines = new List<Line>();
        if (s.IsAcc)
        {
            lines.Add(T(s.Hotel, w: 700));
            lines.Add(T($"{s.Meal} · {s.RoomCat} · {s.Nights}N", Mut, s: 11, dot: true, tip: [new TipLine(s.MealFull, b: true), new TipLine("Room category: " + s.RoomCat)]));
            if (s.Occ != "") lines.Add(T(s.Occ, Mut, s: 11));
        }
        else lines.Add(T(s.Name));
        if (conf) lines.Add(Sub(s.Conf != "" ? s.Conf : "Pending"));
        return [.. lines];
    }

    static Cell SvcCell(Svc s, bool conf = true) => C(SvcLines(s, conf));

    /// <summary>Ticket no. (link), MBK no., file no. and, optionally, the ticket status as bold coloured text.</summary>
    static Cell TicketInfo(Ticket t, bool status = true) =>
        C(T(t.Id, Link, 700), Sub(t.Mbk), Sub(t.File), status ? StatusText(t.Status) : null);

    static Cell VoucherCell(Svc s) => Ca("center", Lk(s.Voucher, toast: "No voucher document is available yet."));

    static Cell DatesCentered(Svc s) => Ca("center", T(Fmt.Fd(s.Start), num: true), T("to " + Fmt.Fd(s.End), Mut, s: 11, num: true), T(s.DurText, Mut, s: 10.5));

    /// <summary>Sell / Cont. / Neg., the negotiated rate highlighted.</summary>
    static Cell PriceInfo3(Svc s) => Ca("right", T("Sell " + Fmt.Inr(s.Sell), num: true), T("Cont. " + Fmt.Inr(s.Contr), Mut, s: 11, num: true),
        T("Neg. " + Fmt.Inr(s.Neg), x: "display:inline-block;padding:1px 6px;border-radius:5px;border:1px solid var(--info-rule);background:var(--info-bg);color:var(--calc-ink);font-weight:800;font-size:13px;", num: true));

    /// <summary>Bill amount minus the negotiated amount: red above, green below, grey when equal or there is no bill.</summary>
    static Cell BillVariance(Svc s)
    {
        if (s.Bill == null) return Txt("right", "-", Mut, num: true);
        var d = s.Bill.Inv - s.Neg;
        return Txt("right", d == 0 ? "Nil" : Fmt.Signed(d), d > 0 ? Red : d < 0 ? Green : Mut, 600, num: true);
    }

    static Cell AdvPaidCell(Svc s) => s.AdvPaid > 0
        ? Ca("right", T(Fmt.Inr(s.AdvPaid), Green, num: true), T(s.AdvCount + (s.AdvCount == 1 ? " advance" : " advances"), Mut, s: 10.5))
        : Txt("right", "-", Mut, num: true);

    static Cell BalCell(Svc s) => Txt("right", s.Bal > 0 ? Fmt.Inr(s.Bal) : "Nil", s.Bal > 0 ? Red : Green, 600, num: true);

    static Cell PayStatusCell(Svc s) => C(s.Bk is { Paid: true } ? Chip("Paid", "ok") : Chip("Pending", "warn"));
}
