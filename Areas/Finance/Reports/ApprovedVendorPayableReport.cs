using SouthernTravels.Areas.Finance.Data;
using SouthernTravels.Areas.Finance.Models;
using SouthernTravels.Core.Infrastructure;
using SouthernTravels.Core.Models;
using static SouthernTravels.Core.Models.G;
using static SouthernTravels.Areas.Finance.Models.FinanceG;

namespace SouthernTravels.Areas.Finance.Reports;

/// <summary>Approved Vendor Payable report (shown as Fund Management).</summary>
public sealed partial class FinanceReports
{
    /// <summary>Pending (not yet submitted), Sent for payment (in the payment basket, unpaid) or Paid.</summary>
    static string PayStatus(Svc s) => s.Bk == null ? "Pending" : s.Bk.Paid ? "Paid" : "Sent for payment";
    static string PayStatusTone(string s) => s switch { "Paid" => "ok", "Sent for payment" => "info", _ => "warn" };

    public ReportVm ApprovedVendorPayable()
    {
        var all = st.Svcs();
        var view = q.View == "ticket" ? "ticket" : "vendor";
        // Uploaded (not disputed) bills approved at both L1 and L2, on confirmed, operated services. Filtering is by bill: a ticket with one
        // disputed bill still lists its other approved bills. Services already in the payment basket stay listed ("Sent for payment" / "Paid")
        // but cannot be ticked.
        var pool = all.Where(s => s.BStat == "R" && s.Ops != 'N' && s.T.Status != "Pending" && PerOk(s.Bill!.Date)).ToList();
        var defs = new List<FDef<Svc>>
        {
            new("type", "Service Type", s => s.V.Type),
            new("ps", "Payment Status", PayStatus, ["Pending", "Sent for payment", "Paid"]),
            new("ex", "Booking Executive", s => s.T.Exec), new("br", "Booking Branch", s => s.T.Branch),
        };
        var rows = ApplyF(pool, defs).Where(s => SearchOk(s.T.Id, s.V.Name, s.V.LegalName, s.T.Pax)).ToList();
        // Money cards read "₹amount (count)" on the bill amount.
        string Money(IEnumerable<Svc> ss) { var l = ss.ToList(); return Fmt.Inr(l.Sum(s => s.Bill!.Rec)) + " (" + l.Count + ")"; }
        var kpis = new List<Kpi>
        {
            K("Total Vendors", rows.Select(s => s.Vi).Distinct().Count(), Strong), K("Total Bills", Money(rows), Blue),
            K("Sent for Payment", Money(rows.Where(s => s.Bk is { Paid: false })), Blue), K("Paid", Money(rows.Where(s => s.Bk is { Paid: true })), Green),
            K("Pending", Money(rows.Where(s => s.Bk == null)), Warn),
        };

        // Ageing sort: oldest-first / newest-first by each group's bill dates.
        var age = q.Filter("age");
        string Oldest(IEnumerable<Svc> ss) => ss.Select(s => s.Bill!.Date).DefaultIfEmpty("9999").Min()!;
        string Newest(IEnumerable<Svc> ss) => ss.Select(s => s.Bill!.Date).DefaultIfEmpty("0000").Max()!;
        IEnumerable<IGrouping<int, Svc>> Sorted(IEnumerable<IGrouping<int, Svc>> gs) => age switch
        {
            "Old to new" => gs.OrderBy(g => Oldest(g), StringComparer.Ordinal).ThenBy(g => g.Key),
            "New to old" => gs.OrderByDescending(g => Newest(g), StringComparer.Ordinal).ThenBy(g => g.Key),
            _ => gs.OrderBy(g => g.Key),
        };

        Cell Chk(Svc s) => s.Bk == null && !readOnly ? C(FinanceG.Chk(s, "avp", s.Bill!.Rec, "Bill " + s.Bill.No + " · " + Fmt.Fd(s.Bill.Date))) : Empty;
        static List<TipLine> GtTip(Ticket t) =>
            [new("Base Price", Fmt.Inr(t.Base)), new("Discount", "−" + Fmt.Inr(t.Disc), c: "red"), new("GST", "+" + Fmt.Inr(t.Gst), c: "blue"), new("Grand Total", Fmt.Inr(t.Grand), b: true, rule: true)];
        static Cell Aging(Svc s)
        {
            var d = Math.Max(0, Fmt.Days(s.Bill!.Date, Clock.Today));
            return Txt("right", d + "d", d > 14 ? Red : d > 7 ? Warn : Mut, d > 7 ? 600 : 400, num: true);
        }
        static Cell PriceInfo(Ticket t)
        {
            var tb = t.Grand - t.Paid;
            return Ca("right", T(Fmt.Inr(t.Grand), num: true, dot: true, tip: GtTip(t)), T("Paid " + Fmt.Inr(t.Paid), Green, s: 11, num: true), T("Bal. " + (tb > 0 ? Fmt.Inr(tb) : "Nil"), tb > 0 ? Red : Green, s: 11, num: true));
        }
        // Columns after the vendor / ticket cells, the same in both views.
        List<Cell> Common(Svc s) =>
        [
            SvcCell(s), Txt("left", Fmt.Fd(s.Start), num: true), Txt("left", Fmt.Fd(s.End), num: true), Txt("center", s.DurText, Mut, s: 11),
            // Bill amount on top (highlighted), then the bill date and number.
            Ca("right", T(Fmt.Inr(s.Bill!.Inv), x: "color:var(--calc-ink);font-weight:800;font-size:13px;", num: true), T(Fmt.Fd(s.Bill.Date), num: true), Sub(s.Bill.No)),
            Ca("right", T("Sell " + Fmt.Inr(s.Sell), num: true),
                T("Neg. " + Fmt.Inr(s.Neg), x: "display:inline-block;padding:1px 6px;border-radius:5px;border:1px solid var(--info-rule);background:var(--info-bg);color:var(--calc-ink);font-weight:800;font-size:13px;", num: true)),
            BillVariance(s), Txt("right", Fmt.Inr(s.Profit), s.Profit < 0 ? Red : Green, 700, num: true),
            Ca("right", T("Adv " + (s.AdvPaid > 0 ? Fmt.Inr(s.AdvPaid) : "-"), s.AdvPaid > 0 ? Green : Mut, num: true), T("Bal " + (s.Bal > 0 ? Fmt.Inr(s.Bal) : "Nil"), s.Bal > 0 ? Red : Green, 600, num: true)),
            Aging(s), C(Chip(PayStatus(s), PayStatusTone(PayStatus(s)))),
            Ca("center", Btn("View Bill", Pop("viewbill", ("id", s.Id))), Btn("View Docs", Pop("avpdocs", ("id", s.Id)))),
        ];
        object[] cmnH = ["Service / Conf. No.", "Start Date", "End Date", ("Duration", "center"), ("Bill Date / No. / Amount", "right"), ("Service Price Info", "right"), ("Variance", "right"),
            ("Profit", "right"), ("Adv. Paid / Balance", "right"), ("Aging", "right"), "Payment Status", ("Bill", "center")];

        List<Col> cols; List<Row> gridRows; string count; PagerVm pager;
        if (view == "vendor")
        {
            // Tickets that still have operated services without a bill, per vendor (not limited by the period).
            var unbilled = all.Where(s => s.Bill == null && s.Ops != 'N' && s.T.Status != "Pending")
                .GroupBy(s => s.Vi).ToDictionary(g => g.Key, g => g.Select(s => s.Ti).Distinct().Count());
            var groups = Sorted(rows.GroupBy(s => s.Vi)).ToDictionary(g => g.Key, g => g.ToList());
            var order = groups.Keys.ToList();
            // Vendors with nothing billed but unbilled tickets still appear (unless a filter narrows the list).
            var filtered = q.F.Any(f => f.Value is not ("" or "All"));
            if (!filtered)
                foreach (var vi in unbilled.Keys.Where(v => !groups.ContainsKey(v) && SearchOk(st.V[v].Name, st.V[v].Id)))
                { groups[vi] = []; order.Add(vi); }
            List<int> newB; lock (st.Gate) newB = order.Where(vi => st.NewBills.GetValueOrDefault(st.V[vi].Id)).ToList();
            order = [.. order.Where(newB.Contains), .. order.Where(v => !newB.Contains(v))];   // vendors with the red dot first
            count = order.Count + " vendors";
            var nh = Cols([.. new object[] { "", "Ticket Info", ("Ticket Details", "right"), ("Service Type / City", "center") }, .. cmnH]);
            cols = ActionLast(Cols("", "", "Vendor ID", "Vendor Name", ("Total Business", "right"), ("Total Contracted", "right"), ("Total Negotiated", "right"), ("Total Profit", "right"), ("Total Unbilled Tickets", "center"), "Bill"));
            var (pOrder, pg) = Paged(order); pager = pg;
            gridRows = pOrder.Select(vi =>
            {
                var ss = groups[vi]; var v = st.V[vi];
                int pr = ss.Sum(s => s.Profit), cp = ss.Sum(s => s.Contr), neg = ss.Sum(s => s.Neg), ub = unbilled.GetValueOrDefault(vi);
                var dot = newB.Contains(vi) ? Ca("center", Dot([new TipLine("New bill(s) added")])) : Empty;
                return new Row
                {
                    Key = "v" + vi, SeenVendor = newB.Contains(vi) ? v.Id : null, SeenArea = "avp", CardHead = 4,
                    Cells =
                    [
                        dot, Txt("left", v.Id, Link, 700), VendorCell(v),
                        Txt("right", ss.Count > 0 ? Fmt.Inr(neg) : "-", Link, 800, num: true), Txt("right", ss.Count > 0 ? Fmt.Inr(cp) : "-", num: true), Txt("right", ss.Count > 0 ? Fmt.Inr(neg) : "-", Blue, num: true),
                        ss.Count > 0 ? Ca("right", T(Fmt.Inr(pr), pr < 0 ? Red : Green, 700, num: true), T(Fmt.Pct(pr, cp), Mut, s: 10.5)) : Txt("right", "-", Mut),
                        Ca("center", Chip(ub.ToString(), ub > 0 ? "warn" : "ok")),
                        C(Btn("View", Pop("vbills", ("vi", vi)), dis: ss.Count == 0)),
                    ],
                    Detail = ss.Count == 0 ? null : new Detail
                    {
                        Cols = nh,
                        Rows = ss.OrderBy(s => s.Ti).Select(List<Cell> (s) => [Chk(s), TicketInfo(s.T), PriceInfo(s.T), Ca("center", Pill(s.V.Type), Sub(s.V.City)), .. Common(s)]).ToList(),
                    },
                };
            }).ToList();
        }
        else
        {
            var gs = Sorted(rows.GroupBy(s => s.Ti)).ToList();
            count = gs.Count + " tickets";
            var nh = Cols([.. new object[] { "", "Vendor", ("Service Type / City", "center") }, .. cmnH]);
            cols = Cols("", "Ticket Info", ("Tour Base Price", "right"), ("Discount", "right"), ("GST", "right"), ("Grand Total", "right"), ("Paid / Balance Pending", "right"), ("Ticket Status", "center"), ("Status", "center"));
            var (pgs, pg) = Paged(gs); pager = pg;
            gridRows = pgs.Select(g =>
            {
                var ss = g.ToList(); var t = ss[0].T; var bal = t.Grand - t.Paid;
                // Paid only once every service on the ticket is paid.
                var paid = ss.All(s => s.Bk is { Paid: true });
                return new Row
                {
                    Key = "t" + g.Key,
                    Cells =
                    [
                        TicketInfo(t, false), Txt("right", Fmt.Inr(t.Base), num: true),
                        Txt("right", t.Disc > 0 ? "−" + Fmt.Inr(t.Disc) : "-", t.Disc > 0 ? Red : Mut, num: true), Txt("right", "+" + Fmt.Inr(t.Gst), Mut, num: true),
                        Ca("right", T(Fmt.Inr(t.Grand), w: 700, num: true, dot: true, tip: GtTip(t))),
                        Ca("right", T(Fmt.Inr(t.Paid), Green, num: true), T(bal > 0 ? Fmt.Inr(bal) : "Nil", bal > 0 ? Red : Green, 600, 11, num: true)),
                        Ca("center", StatusText(t.Status)), Ca("center", Chip(paid ? "Paid" : "Pending", paid ? "ok" : "warn")),
                    ],
                    Detail = new Detail
                    {
                        Cols = nh,
                        Rows = ss.Select(List<Cell> (s) => [Chk(s), VendorCell(s.V, true), Ca("center", Pill(s.V.Type), Sub(s.V.City)), .. Common(s)]).ToList(),
                    },
                };
            }).ToList();
        }

        var filters = MkFilters(pool, defs);
        filters.Add(new FilterVm("f_age", "Ageing sort", age is "" ? "All" : age, ["All", "Old to new", "New to old"]));
        return new ReportVm
        {
            Title = "Fund Management (Approved Vendor Payable)", Pill = true, Kpis = kpis, Query = q, Tabs = ViewTabs(view),
            Desc = "Every service whose bill is uploaded and approved at L1 and L2. Tick services — from either view — and submit; an advice sheet lets you confirm before they move to the Payment Request Report.",
            Qph = "Search Ticket No., Vendor, Passenger...", Filters = filters, FCount = FCount(defs, false) + (age is "" or "All" ? 0 : 1), Count = count,
            SelLabel = readOnly ? null : "Submit", SelPopup = PopUrl("advice"), Cart = true, Cols = cols, Rows = gridRows, Pager = pager, ClearAllUrl = ClearAll,
            EmptyText = view == "vendor" ? "No approved vendor payables awaiting submission." : "No tickets awaiting submission.",
        };
    }

}
