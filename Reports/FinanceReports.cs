using SouthernTravels.Finance.Data;
using SouthernTravels.Finance.Infrastructure;
using SouthernTravels.Finance.Models;
using static SouthernTravels.Finance.Models.G;

namespace SouthernTravels.Finance.Reports;

public sealed record FDef<TRow>(string K, string L, Func<TRow, string> Get, string[]? Opts = null);

/// <summary>Builds the five Finance reports from the store for the current query, user and period.</summary>
public sealed class FinanceReports(FinanceStore st, ReportQuery q, bool perm, (string From, string To)? period)
{
    static readonly Dictionary<string, string> BillTxt = new() { ["R"] = "Raised", ["N"] = "Not Raised", ["D"] = "Dispute" };
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

    (int Skip, PagerVm Pager) MkPager(int total, int per = 10)
    {
        int n = Math.Max(1, (total + per - 1) / per), pg = Math.Min(q.Page, n);
        PageBtn Mk(string label, int to, bool act, bool dis) => new(label, dis ? null : q.Url(c => c.Page = to), act);
        var pages = new List<PageBtn> { Mk("‹", pg - 1, false, pg == 1) };
        pages.AddRange(Enumerable.Range(1, n).Select(i => Mk(i.ToString(), i, i == pg, false)));
        pages.Add(Mk("›", pg + 1, false, pg == n));
        int a = total > 0 ? (pg - 1) * per + 1 : 0, b = Math.Min(total, pg * per);
        return ((pg - 1) * per, new PagerVm($"Showing {a}–{b} of {total}", pages));
    }

    List<TabVm> ViewTabs(string view) =>
    [
        new("Vendor Wise", q.Url(c => { c.View = "vendor"; c.Page = 1; }), view == "vendor"),
        new("Ticket Wise", q.Url(c => { c.View = "ticket"; c.Page = 1; }), view == "ticket"),
    ];

    string ClearAll => q.Url(c => { c.F.Clear(); c.Q = ""; c.From = ""; c.To = ""; c.Page = 1; });

    static Kpi K(string label, object value, string color) => new(label, Convert.ToString(value, Fmt.Inv) ?? "", color);

    static Cell Variation(Svc s) =>
        Txt("right", Fmt.Signed(s.Var), s.Var > 0 ? Green : s.Var < 0 ? Red : Mut, num: true);

    static Cell ProfitCell(Svc s, int w) => Ca("right",
        T(Fmt.Inr(s.Profit), s.Profit < 0 ? Red : Green, w, num: true),
        T(Fmt.Pct(s.Profit, s.Contr) + " CP · " + Fmt.Pct(s.Profit, s.Sell) + " SP", Mut, s: 10.5));

    static Cell Dates(Svc s) => C(T(Fmt.Fd(s.Start), num: true), T("to " + Fmt.Fd(s.End), Mut, s: 11, num: true));

    static Line Sub(string text, string? c = null) => T(text, c ?? Mut, s: 11);

    /// <summary>"Approved"/"Paid" chip only when all items qualify; otherwise "Pending". Tooltip shows the count.</summary>
    static Line AllOrPending(int done, int total, string doneLabel, string verb) =>
        done == total ? Chip(doneLabel, "ok") : Chip("Pending", "warn", [new TipLine($"{done} of {total} {verb}")]);

    static Panel Pan(string title, string strip, IEnumerable<Fact> facts) => new() { Title = title, Strip = strip, Facts = facts.ToList() };

    /* ---------- Report 1: Vendor Payable ---------- */

    public ReportVm VendorPayable()
    {
        var all = st.Svcs();
        var view = q.View == "ticket" ? "ticket" : "vendor";
        var visT = Enumerable.Range(0, st.T.Count).Where(i =>
        {
            var ss = all.Where(s => s.Ti == i).ToList();
            return ss.Count > 0 && ss[0].T.Status != "Pending" && ss.All(s => s.Conf != "");
        }).ToHashSet();
        var rows = all.Where(s => s.Ops != 'N' && visT.Contains(s.Ti) && PerOk(s.Bill?.Date ?? s.End)).ToList();
        var defs = new List<FDef<Svc>>
        {
            new("type", "Service Type", s => s.V.Type), new("city", "Service City", s => s.V.City),
            new("bs", "Bill Status", s => BillTxt[s.BStat], ["Not Raised", "Raised", "Dispute"]),
            new("ops", "OPS Status", s => OpsTxt(s.Ops), ["New", "In Progress", "Completed"]),
            new("ex", "Booking Executive", s => s.T.Exec), new("br", "Booking Branch", s => s.T.Branch),
        };
        var baseRows = rows;
        rows = ApplyF(rows, defs).Where(s => (q.From == "" || Fmt.Cmp(s.End, q.From) >= 0) && (q.To == "" || Fmt.Cmp(s.Start, q.To) <= 0)
            && SearchOk(s.V.Name, s.V.Id, s.T.Id, s.T.Mbk, s.T.Pax, s.T.File)).ToList();

        var byType = rows.Where(s => s.Bal > 0).GroupBy(s => s.V.Type).ToDictionary(g => g.Key, g => g.Sum(s => s.Bal));
        var kpis = new List<Kpi>
        {
            K("Total Payable Amount", Fmt.Inr(rows.Sum(s => s.Neg)), Blue),
            K("Total No. Vendors", rows.Select(s => s.Vi).Distinct().Count(), Strong),
            K("Total Paid Till Now", Fmt.Inr(rows.Sum(s => s.AdvPaid)), Green),
            K("Balance Pending", Fmt.Inr(rows.Sum(s => s.Bal)), Red),
            K("Bill Pending", rows.Count(s => s.BStat == "N"), Warn),
        };
        kpis.AddRange(Types.Select(t => byType.TryGetValue(t, out var v) ? K(t + " Pending", Fmt.Inr(v), Red) : K(t + " Pending", "Nil", Green)));

        Cell Act(Svc s)
        {
            if (!perm) return Txt("left", "-", Mut);
            if (s.BStat == "N")
                return s.Eligible
                    ? C(Btn("Raise Bill", Pop("raise", ("ids", s.Id)), "primary"))
                    : C(T("Available after " + Fmt.Fd(s.End), Mut, it: true));
            return C(Btn("View Bill Details", Pop("viewbill", ("id", s.Id))), Btn("Reupload Bill", Pop("raise", ("ids", s.Id), ("re", 1))));
        }
        Cell Appr(Svc s) => s.Bill != null ? C(Lv("L1", s.Bill.L1.St, ApTip(s.Bill.L1)), Lv("L2", s.Bill.L2.St, ApTip(s.Bill.L2))) : Txt("left", "-", Mut);
        Cell Chk(Svc s) => perm && s.Eligible ? C(G.Chk(s.Id, "vp")) : Empty;
        List<Cell> Money(Svc s) =>
        [
            Txt("right", Fmt.Inr(s.Sell), num: true), Txt("right", Fmt.Inr(s.Contr), num: true), Txt("right", Fmt.Inr(s.Neg), Blue, num: true),
            Variation(s), ProfitCell(s, 600),
            Txt("right", s.AdvPaid > 0 ? Fmt.Inr(s.AdvPaid) : "-", s.AdvPaid > 0 ? Green : Mut, num: true),
            Txt("right", s.Bal > 0 ? Fmt.Inr(s.Bal) : "Nil", s.Bal > 0 ? Red : Green, 600, num: true),
            C(Chip(BillTxt[s.BStat], BillTone[s.BStat])), Appr(s), Act(s),
        ];
        object[] mHead = [("Selling Rate", "right"), ("Contracted Rate", "right"), ("Negotiated", "right"), ("Variation", "right"), ("Profit / % on CP · SP", "right"), ("Advance Paid", "right"), ("Balance to Pay", "right"), "Bill Status", "Approval (L1/L2)", "Actions"];
        static int Pend(List<Svc> ss) => ss.Count(s => s.BStat != "R");
        static Cell BpCell(int n) => Ca("center", Chip(n.ToString(), n > 0 ? "danger" : "ok"));
        static Cell ConfCell(Svc s) => C(T(s.Name), Sub(s.Conf != "" ? s.Conf : "Pending"));

        List<Col> cols; List<Row> gridRows; string count;
        if (view == "vendor")
        {
            var gs = rows.GroupBy(s => s.Vi).OrderBy(g => g.Key).Select(g => g.ToList()).ToList();
            count = gs.Count + " vendors";
            var nh = Cols([.. new object[] { "", "MBK / Ticket", "Name of Service / Conf. No.", "Service Dates" }, .. mHead]);
            cols = Cols("", "Vendor ID", "Vendor Name", "Service Type(s)", "City/Cities", ("Total Services", "center"), ("Total Duration", "right"), ("Bills Pending", "center"), ("Total Profit Earned", "right"), ("Total Business", "right"));
            gridRows = gs.Select(ss =>
            {
                var v = ss[0].V; var prof = ss.Sum(s => s.Profit);
                return new Row
                {
                    Key = "v" + ss[0].Vi,
                    Cells =
                    [
                        Txt("left", v.Id, Link, 700), Txt("left", v.Name, Strong, 600), new Cell { Lines = ss.Select(s => s.V.Type).Distinct().Select(Pill).ToList() },
                        Txt("left", v.City, Mut), Txt("center", ss.Count.ToString(), w: 600, num: true), Txt("right", ss.Sum(s => s.Dur) + "d", num: true), BpCell(Pend(ss)),
                        Txt("right", Fmt.Inr(prof), prof < 0 ? Red : Green, 600, num: true), Txt("right", Fmt.Inr(ss.Sum(s => s.Neg)), Link, 800, num: true),
                    ],
                    Detail = new Detail
                    {
                        Cols = nh,
                        Rows = ss.Select(List<Cell> (s) => [Chk(s), C(T(s.T.Mbk, Link, 700), Sub(s.T.Id)), ConfCell(s), Dates(s), .. Money(s)]).ToList(),
                    },
                };
            }).ToList();
        }
        else
        {
            var gs = rows.GroupBy(s => s.Ti).OrderBy(g => g.Key).Select(g => g.ToList()).ToList();
            count = gs.Count + " tickets";
            var nh = Cols([.. new object[] { "", "Vendor", "Service Type / City", "Name of Service / Conf. No.", "Service Dates" }, .. mHead]);
            cols = Cols("", "Ticket / MBK", "File No.", "Passenger", ("Pax", "center"), "Booking Exec / Branch", ("Base Price", "right"), ("Discount", "right"), ("GST", "right"), ("Grand Total", "right"), ("Balance", "right"), "Ticket Status", ("Bills Pending", "center"), ("Total Business", "right"));
            gridRows = gs.Select(ss =>
            {
                var t = ss[0].T; var bal = t.Grand - t.Paid;
                return new Row
                {
                    Key = "t" + ss[0].Ti,
                    Cells =
                    [
                        C(T(t.Id, Link, 700), Sub(t.Mbk)), Txt("left", t.File, num: true), Txt("left", t.Pax, Strong, 600), Txt("center", t.N.ToString(), num: true),
                        C(T(t.Exec), Sub(t.Branch)), Txt("right", Fmt.Inr(t.Base), num: true),
                        Txt("right", t.Disc > 0 ? "−" + Fmt.Inr(t.Disc) : "-", t.Disc > 0 ? Red : Mut, num: true), Txt("right", "+" + Fmt.Inr(t.Gst), Mut, num: true),
                        Txt("right", Fmt.Inr(t.Grand), w: 700, num: true), Txt("right", bal > 0 ? Fmt.Inr(bal) : "Nil", bal > 0 ? Red : Green, 600, num: true),
                        C(Chip(t.Status, ChipTone(t.Status))), BpCell(Pend(ss)), Txt("right", Fmt.Inr(ss.Sum(s => s.Neg)), Link, 800, num: true),
                    ],
                    Detail = new Detail
                    {
                        Cols = nh,
                        Rows = ss.Select(List<Cell> (s) => [Chk(s), C(T(s.V.Name, w: 600), T(s.V.Id, Link, 700, 11)), C(Pill(s.V.Type), Sub(s.V.City)), ConfCell(s), Dates(s), .. Money(s)]).ToList(),
                    },
                };
            }).ToList();
        }

        return new ReportVm
        {
            Title = "Vendor Payable Report", Pill = true, Perm = perm, KpiCols = 5, Kpis = kpis, Query = q, Tabs = ViewTabs(view),
            Desc = "Consolidated view of amounts owed to each vendor — covering contracted rate, negotiated amount, variation, and net vendor payable per ticket and service, plus Finance’s bill entry, approval (L1/L2), and payment-action tracking per service.",
            Qph = "Search Vendor Name, Ticket No., MBK No., Passenger, File No...", Filters = MkFilters(baseRows, defs), FCount = FCount(defs, true),
            DateLabel = "Service Date Range (overlap)", Count = count, SelLabel = perm ? "Raise Bill" : null, SelPopup = "raise",
            Cols = cols, Rows = gridRows, EmptyText = view == "vendor" ? "No vendors found." : "No tickets found.", ClearAllUrl = ClearAll,
        };
    }

    /* ---------- Report 2: Advance ---------- */

    sealed record AdvRow(AdvanceRequest A, Svc S, string Ov, string Due);

    public ReportVm Advance()
    {
        var all = st.Svcs();
        List<AdvanceRequest> reqs;
        lock (st.Gate) reqs = st.Adv.ToList();
        var rows = reqs.Select(a =>
        {
            var s = all.First(x => x.Id == a.Sid);
            var ov = a.L1 == "X" || a.L2 == "X" ? "Rejected" : a.L1 == "A" && a.L2 == "A" ? "Approved" : "Requested";
            return new AdvRow(a, s, ov, Fmt.Add(s.Start, -3));
        }).Where(r => PerOk(r.A.At)).ToList();
        var defs = new List<FDef<AdvRow>>
        {
            new("ap", "Approval Status", r => r.Ov, ["Requested", "Approved", "Rejected"]),
            new("ps", "Adv. Payment Status", r => r.A.Pay != null ? "Paid" : "Pending", ["Pending", "Paid"]),
            new("ou", "OPS User", r => r.A.By), new("st", "Service Type", r => r.S.V.Type),
        };
        var baseRows = rows;
        rows = ApplyF(rows, defs).Where(r => (q.From == "" || Fmt.Cmp(r.S.Start, q.From) >= 0) && (q.To == "" || Fmt.Cmp(r.S.Start, q.To) <= 0)
            && SearchOk(r.S.T.Mbk, r.S.T.Id, r.S.T.Pax, r.S.V.Name, r.S.Name)).ToList();
        if (q.Sort is "start" or "due")
            rows = rows.OrderBy(r => r, Comparer<AdvRow>.Create((x, y) =>
                (q.Sort == "due" ? Fmt.Cmp(x.Due, y.Due) : Fmt.Cmp(x.S.Start, y.S.Start)) * q.Dir)).ToList();
        var (skip, pager) = MkPager(rows.Count);
        var page = rows.Skip(skip).Take(10).ToList();

        int Sum(Func<AdvRow, int> f, Func<AdvRow, bool> p) => rows.Where(p).Sum(f);
        var kpis = new List<Kpi>
        {
            K("Total Requests", rows.Count, Strong), K("Approved", rows.Count(r => r.Ov == "Approved"), Green),
            K("Pending Approval", rows.Count(r => r.Ov == "Requested"), Blue), K("Rejected", rows.Count(r => r.Ov == "Rejected"), Red),
            K("Total Advance Paid", Fmt.Inr(Sum(r => r.A.Amt, r => r.A.Pay != null)), Green),
            K("Pending Approval Amt.", Fmt.Inr(Sum(r => r.A.Amt, r => r.Ov == "Requested")), Blue),
            K("Outstanding Balance", Fmt.Inr(Sum(r => r.S.Bal, r => r.Ov == "Approved")), Red),
        };

        Col SortTh(Col th, string k) => new()
        {
            L = th.L + (q.Sort == k ? (q.Dir > 0 ? " ▲" : " ▼") : " ⇅"), Align = th.Align, SortActive = q.Sort == k,
            SortUrl = q.Url(c => { c.Sort = k; c.Dir = q.Sort == k && q.Dir > 0 ? -1 : 1; c.Page = 1; }),
        };
        var cols = Cols("", "MBK / Ticket", "Ticket Status", "Passenger / Pax", "Booking Exec / Branch", "Tour Period", "Vendor / Service Type / Service", "Service Start", "Due Date", ("Service Amt. / Balance / Prev. Adv. Paid", "right"), ("Adv. Requested", "right"), "Requested By", "Approvals", "Adv. Payment", "Action")
            .Select(c => c.L == "Service Start" ? SortTh(c, "start") : c.L == "Due Date" ? SortTh(c, "due") : c).ToList();

        static Fact F(string l, string v, string? c = null, bool b = false) => new() { L = l, V = v, Color = c, Bold = b, Num = true };

        var gridRows = page.Select(r =>
        {
            var (a, s) = (r.A, r.S);
            var t = s.T;
            var tsv = all.Where(x => x.Ti == s.Ti).ToList();
            var ts = tsv.Select(x => x.Start).OrderBy(x => x, StringComparer.Ordinal).First();
            var te = tsv.Select(x => x.End).OrderBy(x => x, StringComparer.Ordinal).Last();
            var bal = t.Grand - t.Paid;
            var payCell = a.Pay != null ? C(Chip("Paid", "ok"), Sub(Fmt.Fdt(a.Pay.Date)))
                : r.Ov == "Approved" ? C(Chip("Pending", "warn")) : Txt("left", "-", Mut);
            var actCell = !perm ? Txt("left", "-", Mut)
                : a.Pay != null ? C(Btn("View Payment Details", Pop("advview", ("sid", a.Sid))), LinkBtn("Print Advice", AdviceUrl(a.Sid)))
                : r.Ov == "Approved" ? C(Btn("Update Payment", Pop("advpay", ("sid", a.Sid)), "primary"), LinkBtn("Print Advice", AdviceUrl(a.Sid)))
                : C(T(r.Ov == "Rejected" ? "Rejected" : "Awaiting approval", Mut, it: true));
            Line Ap(string l, string c)
            {
                var (by, at) = l == "L1" ? ("Vijay Gupta", Fmt.Add(a.At, 1) + " 10:05") : ("Sowmya", Fmt.Add(a.At, 1) + " 16:40");
                return Lv(l, c, c == "A" ? [new TipLine(by, "", b: true), new TipLine(Fmt.Fdt(at))] : null);
            }
            var detail = new Detail
            {
                PanelCols = "1.3fr 1fr 1fr",
                Panels =
                [
                    Pan("Ticket financials", "var(--action)", [F("Base Price (incl. Margin)", Fmt.Inr(t.Base)), F("Discount", t.Disc > 0 ? "−" + Fmt.Inr(t.Disc) : "-", Red), F("GST", Fmt.Inr(t.Gst)), F("Grand Total", Fmt.Inr(t.Grand), null, true), F("Paid by Customer", Fmt.Inr(t.Paid), Green), F("Balance Pending", bal > 0 ? Fmt.Inr(bal) : "Nil", bal > 0 ? Red : Green, true)]),
                    Pan("Service details", "var(--calc-ink)", [F("Conf. Date", s.Conf != "" ? Fmt.Fd(Fmt.Add(s.Start, -20)) : "Pending", s.Conf != "" ? null : Warn), F("Conf. No.", s.Conf != "" ? s.Conf : "Pending"), F("Service Start", Fmt.Fd(s.Start)), F("Service Duration", s.Dur + "d")]),
                    Pan("Approval details", "var(--nav-bg)", [F("L1 Approver (Finance)", a.L1 == "A" ? "Vijay Gupta" : "-"), F("L1 Approved On", a.L1 == "A" ? Fmt.Fdt(a.At) : "-"), F("L2 Approver (Management)", a.L2 == "A" ? "Sowmya" : "-"), F("L2 Approved On", a.L2 == "A" ? Fmt.Fdt(Fmt.Add(a.At, 1) + " 16:40") : "-")]),
                ],
            };
            return new Row
            {
                Key = a.Sid, Detail = detail,
                Cells =
                [
                    C(T(t.Id, Link, 700), Sub(t.Mbk)), C(Chip(t.Status, ChipTone(t.Status))), C(T(t.Pax, w: 600), Sub(t.N + " pax")), C(T(t.Exec), Sub(t.Branch)),
                    C(T(Fmt.Fd(ts), num: true), Sub("to " + Fmt.Fd(te)), Sub(Fmt.Days(ts, te) + 1 + "d")), C(T(s.V.Name, w: 600), Pill(s.V.Type), Sub(s.Name)),
                    Txt("left", Fmt.Fd(s.Start), num: true), Txt("left", Fmt.Fd(r.Due), Warn, 700, num: true),
                    Ca("right", T(Fmt.Inr(s.Neg), w: 700, num: true), T("Bal. " + (s.Bal > 0 ? Fmt.Inr(s.Bal) : "Nil"), s.Bal > 0 ? Red : Green, s: 11, num: true), T("Prev. " + (s.Adv0 > 0 ? Fmt.Inr(s.Adv0) : "-"), s.Adv0 > 0 ? Green : Mut, s: 11, num: true)),
                    Ca("right", T(Fmt.Inr(a.Amt), Blue, 800, 14, num: true, x: "background:var(--info-bg);border:1px solid var(--info-rule);border-radius:6px;padding:2px 8px;"), T(Fmt.Fdt(a.At), Mut, s: 10.5)),
                    C(T(a.By), Sub(a.Br)), C(Ap("L1", a.L1), Ap("L2", a.L2)), payCell, actCell,
                ],
            };
        }).ToList();

        return new ReportVm
        {
            Title = "Advance Report", Pill = true, Perm = perm, Kpis = kpis, Query = q,
            Desc = "Service-wise advance payment tracking — shows all advance requests raised by OPS against vendor services, approval chain, and payment status. Only services where an advance has been formally requested appear here.",
            Qph = "Search MBK, Ticket No., Passenger, Vendor, Service...", Filters = MkFilters(baseRows, defs), FCount = FCount(defs, true),
            DateLabel = "Tour Start Date", Count = rows.Count + " requests", Cols = cols, Rows = gridRows, EmptyText = "No advance requests found.", Pager = pager, ClearAllUrl = ClearAll,
        };
    }

    /* ---------- Report 3: Approved Vendor Payable ---------- */

    public ReportVm ApprovedVendorPayable()
    {
        var all = st.Svcs();
        var view = q.View == "ticket" ? "ticket" : "vendor";
        List<Svc> rows;
        lock (st.Gate) rows = st.AwaitingSubmission(all).Where(s => PerOk(s.Bill!.Date)).ToList();
        var defs = new List<FDef<Svc>>
        {
            new("type", "Service Type", s => s.V.Type), new("bs", "Bill Status", s => BillTxt[s.Bill!.St], ["Raised", "Dispute"]),
            new("ps", "Payment Status", s => s.Bill!.Rec >= s.Neg ? "Paid" : "Pending", ["Pending", "Paid"]),
            new("ex", "Booking Executive", s => s.T.Exec), new("br", "Booking Branch", s => s.T.Branch),
        };
        var baseRows = rows;
        rows = ApplyF(rows, defs).Where(s => SearchOk(s.T.Id, s.V.Name, s.T.Pax)).ToList();
        var kpis = new List<Kpi>
        {
            K("Vendors", rows.Select(s => s.Vi).Distinct().Count(), Strong), K("Tickets Awaiting Submission", rows.Select(s => s.Ti).Distinct().Count(), Blue),
            K("Total Value", Fmt.Inr(rows.Sum(s => s.Neg)), Green), K("Disputed Bills", rows.Count(s => s.Bill!.St == "D"), Red),
        };
        Cell Chk(Svc s) => perm ? C(G.Chk(s.Id, "avp")) : Empty;
        static List<TipLine> GtTip(Ticket t) =>
            [new("Base Price", Fmt.Inr(t.Base)), new("Discount", "−" + Fmt.Inr(t.Disc), c: "red"), new("GST", "+" + Fmt.Inr(t.Gst), c: "blue"), new("Grand Total", Fmt.Inr(t.Grand), b: true, rule: true)];
        static Cell Aging(Svc s)
        {
            var d = Fmt.Days(s.Bill!.Date, FinanceStore.Today);
            return Txt("right", d + "d", d > 14 ? Red : d > 7 ? Warn : Mut, d > 7 ? 600 : 400, num: true);
        }
        static (string, string) PayTone(Svc s) => s.Bill!.Rec >= s.Neg ? ("Paid", "ok") : ("Pending", "warn");
        static Cell Period(Svc s) => C(T(s.Name), Sub((s.Conf != "" ? s.Conf : "Pending") + " · " + Fmt.Fd(s.Start) + " – " + Fmt.Fd(s.End)));
        List<Cell> Common(Svc s) =>
        [
            Txt("right", Fmt.Inr(s.Sell), num: true), Txt("right", Fmt.Inr(s.Contr), num: true), Txt("right", Fmt.Inr(s.Neg), Blue, num: true), Variation(s), ProfitCell(s, 700),
            C(Chip(s.Status, ChipTone(s.Status))),
            Ca("right", T(s.AdvPaid > 0 ? Fmt.Inr(s.AdvPaid) : "-", s.AdvPaid > 0 ? Green : Mut, num: true), T(s.Bal > 0 ? Fmt.Inr(s.Bal) : "Nil", s.Bal > 0 ? Red : Green, 600, num: true)),
            Aging(s), C(Btn("View Bill / Details", Pop("viewbill", ("id", s.Id)))),
        ];
        object[] cmnH = [("Selling", "right"), ("Contracted", "right"), ("Negotiated", "right"), ("Variation", "right"), ("Profit (SP-CP) / %", "right"), "Service Status", ("Adv. Paid / Balance", "right"), ("Aging", "right"), "View Bill / Details"];

        List<Col> cols; List<Row> gridRows; string count;
        if (view == "vendor")
        {
            var gs = rows.GroupBy(s => s.Vi).OrderBy(g => g.Key).Select(g => g.ToList()).ToList();
            count = gs.Count + " vendors";
            var nh = Cols([.. new object[] { "", "Ticket / MBK", ("Grand Total / Paid / Bal.", "right"), "Bill Date / No.", "Payment Status", "Service / Conf. No. / Period" }, .. cmnH]);
            cols = Cols("", "Vendor ID", "Vendor Name", "Service Type", "Service City", ("Total Business", "right"), ("Total Contracted", "right"), ("Total Negotiated", "right"), ("Total Profit", "right"), "View Bill / Details");
            gridRows = gs.Select(ss =>
            {
                var v = ss[0].V; int pr = ss.Sum(s => s.Profit), cp = ss.Sum(s => s.Contr), neg = ss.Sum(s => s.Neg);
                return new Row
                {
                    Key = "v" + ss[0].Vi,
                    Cells =
                    [
                        Txt("left", v.Id, Link, 700), Txt("left", v.Name, Strong, 600), C(Pill(v.Type)), Txt("left", v.City, Mut),
                        Txt("right", Fmt.Inr(neg), Link, 800, num: true), Txt("right", Fmt.Inr(cp), num: true), Txt("right", Fmt.Inr(neg), Blue, num: true),
                        Ca("right", T(Fmt.Inr(pr), pr < 0 ? Red : Green, 700, num: true), T(Fmt.Pct(pr, cp), Mut, s: 10.5)),
                        C(Btn("View Bill / Details", Pop("vbills", ("vi", ss[0].Vi)))),
                    ],
                    Detail = new Detail
                    {
                        Cols = nh,
                        Rows = ss.Select(List<Cell> (s) =>
                        {
                            var (ptxt, ptone) = PayTone(s); var t = s.T; var tb = t.Grand - t.Paid;
                            return
                            [
                                Chk(s), C(T(t.Id, Link, 700), Sub(t.Mbk), Chip(t.Status, ChipTone(t.Status))),
                                Ca("right", T(Fmt.Inr(t.Grand), num: true, dot: true, tip: GtTip(t)), T("Paid " + Fmt.Inr(t.Paid), Green, s: 11, num: true), T("Bal. " + (tb > 0 ? Fmt.Inr(tb) : "Nil"), tb > 0 ? Red : Green, s: 11, num: true)),
                                C(T(Fmt.Fd(s.Bill!.Date), num: true), Sub(s.Bill.No)), C(Chip(ptxt, ptone)), Period(s), .. Common(s),
                            ];
                        }).ToList(),
                    },
                };
            }).ToList();
        }
        else
        {
            var gs = rows.GroupBy(s => s.Ti).OrderBy(g => g.Key).Select(g => g.ToList()).ToList();
            count = gs.Count + " tickets";
            var nh = Cols([.. new object[] { "", "Vendor", "Service Type / City", "Service / Conf. No. / Period" }, .. cmnH]);
            cols = Cols("", "Ticket / MBK", ("Tour Base Price", "right"), ("Additional Profit", "right"), ("Discount", "right"), ("GST", "right"), ("Grand Total", "right"), ("Paid by Customer", "right"), ("Balance Pending", "right"), "Ticket Status", "Bill Date / No.", "Payment Status", ("Recommended Amt.", "right"));
            gridRows = gs.Select(ss =>
            {
                var t = ss[0].T;
                int pay = ss.Sum(s => s.Neg), rec = ss.Sum(s => s.Bill!.Rec), extra = t.Grand - pay, bal = t.Grand - t.Paid;
                var dates = ss.Select(s => s.Bill!.Date).OrderBy(x => x, StringComparer.Ordinal).ToList();
                var nos = ss.Select(s => s.Bill!.No).Distinct().ToList();
                var ps = rec >= pay ? "Full Payment" : rec > 0 ? "Part Payment" : "Pending";
                return new Row
                {
                    Key = "t" + ss[0].Ti,
                    Cells =
                    [
                        C(T(t.Id, Link, 700), Sub(t.Mbk)), Txt("right", Fmt.Inr(t.Base), num: true), Txt("right", Fmt.Inr(extra), extra < 0 ? Red : Green, 600, num: true),
                        Txt("right", t.Disc > 0 ? "−" + Fmt.Inr(t.Disc) : "-", t.Disc > 0 ? Red : Mut, num: true), Txt("right", "+" + Fmt.Inr(t.Gst), Mut, num: true),
                        Ca("right", T(Fmt.Inr(t.Grand), w: 700, num: true, dot: true, tip: GtTip(t))),
                        Txt("right", Fmt.Inr(t.Paid), Green, num: true), Txt("right", bal > 0 ? Fmt.Inr(bal) : "Nil", bal > 0 ? Red : Green, 600, num: true), C(Chip(t.Status, ChipTone(t.Status))),
                        C(T(Fmt.Fd(dates[0]), num: true), Sub(nos.Count > 1 ? nos.Count + " bills" : nos[0])), C(Chip(ps, ps == "Pending" ? "off" : ChipTone(ps))), Txt("right", Fmt.Inr(rec), w: 700, num: true),
                    ],
                    Detail = new Detail
                    {
                        Cols = nh,
                        Rows = ss.Select(List<Cell> (s) => [Chk(s), C(T(s.V.Name, w: 600), T(s.V.Id, Link, 700, 11)), C(Pill(s.V.Type), Sub(s.V.City)), Period(s), .. Common(s)]).ToList(),
                    },
                };
            }).ToList();
        }

        return new ReportVm
        {
            Title = "Approved Vendor Payable", Pill = true, Perm = perm, Kpis = kpis, Query = q, Tabs = ViewTabs(view),
            Desc = "Every service whose bill has been added in the Vendor Payable Report. Select tickets — from either view — and submit; a summary sheet lets you confirm before they move to the Payment Request Report.",
            Qph = "Search Ticket No., Vendor, Passenger...", Filters = MkFilters(baseRows, defs), FCount = FCount(defs, false), Count = count,
            SelLabel = perm ? "Submit" : null, SelPopup = "advice", Cols = cols, Rows = gridRows, ClearAllUrl = ClearAll,
            EmptyText = view == "vendor" ? "No billed vendor payables awaiting submission." : "No tickets awaiting submission.",
        };
    }

    /* ---------- Report 4: Payment Request ---------- */

    public ReportVm PaymentRequest()
    {
        var all = st.Svcs();
        List<BasketItem> basket; Dictionary<string, bool> dots;
        lock (st.Gate) { basket = st.Basket.ToList(); dots = new(st.Dots); }
        var rows = basket.Select(b => (B: b, S: all.First(x => x.Id == b.Sid)))
            .Where(r => PerOk(r.S.Bill?.Date ?? r.S.End) && SearchOk(r.S.T.Id, r.S.V.Name, r.S.Name)).ToList();
        var gs = rows.GroupBy(r => r.S.Vi).Select(g => (Vi: g.Key, Bs: g.ToList(), Dot: dots.GetValueOrDefault(st.V[g.Key].Id)))
            .OrderByDescending(g => g.Dot).ThenBy(g => st.V[g.Vi].Name, StringComparer.CurrentCulture).ToList();
        var paid = rows.Count(r => r.B.Paid);
        var kpis = new List<Kpi>
        {
            K("Vendors", gs.Count, Strong), K("In Basket", rows.Count, Strong), K("Paid", paid, Green), K("Pending", rows.Count - paid, Warn),
            K("Basket Total", Fmt.Inr(rows.Sum(r => r.S.Bill?.Rec ?? 0)), Blue),
        };
        var nh = Cols("Ticket / MBK", "Vendor", "Service Type / City", "Service / Conf. No.", "Service Dates", ("Vendor Payable", "right"), "Actual Payment Action", "Mgmt. Approval", "Actions");
        var gridRows = gs.Select(g =>
        {
            var v = st.V[g.Vi]; var tot = g.Bs.Sum(r => r.S.Bill?.Rec ?? 0);
            var dot = g.Dot ? Ca("center", Dot([new TipLine("New ticket(s) added")])) : Empty;
            var unpaid = g.Bs.Where(r => !r.B.Paid).ToList();
            return new Row
            {
                Key = "v" + g.Vi, SeenVendor = g.Dot ? v.Id : null,
                Cells =
                [
                    dot, Txt("left", v.Id, Link, 700), Txt("left", v.Name, Strong, 600), Txt("center", g.Bs.Select(r => r.S.Ti).Distinct().Count().ToString(), w: 600, num: true), Txt("right", Fmt.Inr(tot), Link, 800, num: true),
                    // A vendor is Approved / Paid only once every ticket in its drill-down is; otherwise Pending.
                    C(AllOrPending(g.Bs.Count(r => r.B.Mgmt == "Approved"), g.Bs.Count, "Approved", "approved")),
                    C(AllOrPending(g.Bs.Count(r => r.B.Paid), g.Bs.Count, "Paid", "paid")),
                    unpaid.Count > 0
                        ? C(LinkBtn("Download Advice", PaymentAdviceUrl(g.Vi, unpaid.Select(r => r.S.Id))))
                        : C(Btn("Download Advice", dis: true)),
                ],
                Detail = new Detail
                {
                    Cols = nh,
                    Rows = g.Bs.Select(List<Cell> (r) =>
                    {
                        var (b, s) = r;
                        return
                        [
                            C(T(s.T.Id, Link, 700), Sub(s.T.Mbk)), C(T(v.Name, w: 600), T(v.Id, Link, 700, 11)), C(Pill(v.Type), Sub(v.City)),
                            C(T(s.Name), Sub(s.Conf != "" ? s.Conf : "Pending")), Dates(s), Txt("right", Fmt.Inr(s.Bill?.Rec ?? 0), w: 700, num: true),
                            C(Chip(b.Paid ? "Paid" : "Pending", b.Paid ? "ok" : "warn")),
                            C(Lv("Mgmt", b.Paid ? "A" : "Q", b.Paid ? [new TipLine(b.By ?? "Mounika", "", b: true), new TipLine(Fmt.Fdt(b.At))] : null)),
                            C(
                                // Same advice as the vendor row, limited to this ticket's unpaid services.
                                b.Paid ? Btn("Download Advice", dis: true)
                                       : LinkBtn("Download Advice", PaymentAdviceUrl(g.Vi, g.Bs.Where(x => !x.B.Paid && x.S.Ti == s.Ti).Select(x => x.S.Id))),
                                perm && !b.Paid ? Btn("Update Payment Details", Pop("prpay", ("sid", s.Id)), "primary") : null),
                        ];
                    }).ToList(),
                },
            };
        }).ToList();

        return new ReportVm
        {
            Title = "Payment Request Report", Pill = true, Perm = perm, Kpis = kpis, Query = q,
            Desc = "The payment basket, grouped by vendor and ticket — generated once Finance confirms submission from Approved Vendor Payable, for actual payment execution. A vendor with newly added tickets is marked with a red dot and moved to the top until viewed.",
            Qph = "Search Ticket No., Vendor, Service...", Count = gs.Count + " vendors",
            Cols = Cols("", "", "Vendor ID", "Vendor Name", ("Tickets", "center"), ("Total Value", "right"), "Approval Status", "Payment Status", "Payment Advice"), Rows = gridRows, EmptyText = "No payment requests found.", ClearAllUrl = ClearAll,
        };
    }

    /* ---------- Report 5: Vendor KYC ---------- */

    sealed record KycRow(Kyc K, Vendor V, int N, int P, string Es);

    public ReportVm VendorKyc()
    {
        string[] dn = ["PAN Copy", "GST Reg. Cert.", "Address Proof", "Cancelled Cheque", "Cert. of Incorp."];
        var rows = st.K.Select(k =>
        {
            var n = k.Docs.Count(x => x); var p = n * 20;
            return new KycRow(k, st.V[k.Vi], n, p, p == 100 ? "Empanelled" : p >= 60 ? "Pending Verification" : "Expired");
        }).Where(r => PerOk(r.K.Created)).ToList();
        var defs = new List<FDef<KycRow>>
        {
            new("es", "Empanelment Status", r => r.Es, ["Empanelled", "Pending Verification", "Expired"]),
            new("type", "Service Type", r => r.V.Type), new("state", "State", r => r.V.State),
            new("act", "Active Status", r => r.K.Active ? "Active" : "Inactive", ["Active", "Inactive"]),
        };
        var baseRows = rows;
        rows = ApplyF(rows, defs).Where(r => SearchOk(r.V.Id, r.V.Name, r.V.City)).ToList();
        var (skip, pager) = MkPager(rows.Count);
        var kpis = new List<Kpi>
        {
            K("Total Vendors", rows.Count, Strong), K("Empanelled", rows.Count(r => r.Es == "Empanelled"), Green),
            K("Pending Verification", rows.Count(r => r.Es == "Pending Verification"), Warn), K("Expired", rows.Count(r => r.Es == "Expired"), Red),
            K("Active Vendors", rows.Count(r => r.K.Active), Blue),
        };
        static Fact F(string l, string v, bool full = false) => new() { L = l, V = v, Full = full };

        var gridRows = rows.Skip(skip).Take(10).Select(r =>
        {
            var (k, v) = (r.K, r.V);
            var detail = new Detail
            {
                PanelCols = "1fr 1fr 1.2fr",
                Panels =
                [
                    Pan("Contact & business", "var(--action)", [F("Form No.", k.Form), F("Address", k.Addr), F("Contact 1", k.Contact), F("Mobile", k.Mobile), F("Email", k.Email, true), F("PAN No.", k.Pan), F("GST No.", k.Gstn)]),
                    Pan("Bank & payment terms", "var(--calc-ink)", [F("Bank Name", k.Bank), F("Account No.", k.Acc), F("IFSC Code", k.Ifsc), F("TDS % / Terms", k.Terms), F("Contracted By", k.By), F("Approved By", k.Appr != "" ? k.Appr : "-"), F("Date of Creation", Fmt.Fd(k.Created), true)]),
                    new Panel { Title = "KYC documents", Strip = "var(--nav-bg)", Docs = dn.Select((n, j) => new DocTile { Name = n, Received = k.Docs[j], Pop = k.Docs[j] ? Pop("doc", ("k", k.Vi), ("n", n)) : null }).ToList() },
                ],
            };
            return new Row
            {
                Key = v.Id, Detail = detail,
                Cells =
                [
                    Txt("left", v.Id, Link, 700), Txt("left", v.Name, Strong, 600), C(Pill(v.Type)), Txt("left", v.City, Mut), Txt("left", v.State, Mut),
                    C(Chip(k.Active ? "Active" : "Inactive", k.Active ? "ok" : "off")), C(Chip(r.Es, r.Es == "Empanelled" ? "ok" : r.Es == "Expired" ? "danger" : "warn")),
                    Txt("center", r.P + "%", r.P == 100 ? Green : Warn, 700, num: true), Txt("center", r.N + " / 5", num: true), Txt("left", Fmt.Fd(k.Expiry), num: true),
                ],
            };
        }).ToList();

        return new ReportVm
        {
            Title = "Vendor KYC Report", Perm = perm, Kpis = kpis, Query = q,
            Desc = "Centralised KYC and vendor-onboarding register. Top line shows empanelment status and completeness; expand a vendor for contact, bank and document detail side by side. Contact, bank and document values are sample data — no KYC source is connected yet.",
            Qph = "Search Vendor Code, Name, City...", Filters = MkFilters(baseRows, defs), FCount = FCount(defs, false), Count = rows.Count + " vendors",
            HasExport = false, HasExpand = false,
            Cols = Cols("", "Vendor Code", "Vendor Name", "Type", "City", "State", "Active", "Empanelment Status", ("KYC %", "center"), ("Docs Received", "center"), "Contract Expiry"),
            Rows = gridRows, EmptyText = "No vendors found.", Pager = pager, ClearAllUrl = ClearAll,
        };
    }
}
