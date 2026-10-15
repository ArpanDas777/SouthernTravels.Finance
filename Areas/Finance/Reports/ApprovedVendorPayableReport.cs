using SouthernTravels.Areas.Finance.Data;
using SouthernTravels.Areas.Finance.Models;
using SouthernTravels.Core.Infrastructure;
using SouthernTravels.Core.Models;
using static SouthernTravels.Core.Models.G;
using static SouthernTravels.Areas.Finance.Models.FinanceG;

namespace SouthernTravels.Areas.Finance.Reports;

/// <summary>Approved Vendor Payable report.</summary>
public sealed partial class FinanceReports
{
    /// <summary>Pending (not yet submitted), Sent for payment (in the basket, unpaid) or Paid.</summary>
    static string PayStatus(Svc s) => s.Bk == null ? "Pending" : s.Bk.Paid ? "Paid" : "Sent for payment";
    static string PayStatusTone(string s) => s switch { "Paid" => "ok", "Sent for payment" => "info", _ => "warn" };

    public ReportVm ApprovedVendorPayable()
    {
        var all = st.Svcs();
        var view = q.View == "ticket" ? "ticket" : "vendor";
        // Billed, operated services of confirmed tickets. Those already in the payment basket stay listed as "Sent for payment"
        // but cannot be ticked and are left out of the KPIs.
        var pool = all.Where(s => s.Bill != null && s.Ops != 'N' && s.T.Status != "Pending" && PerOk(s.Bill.Date)).ToList();
        var defs = new List<FDef<Svc>>
        {
            new("type", "Service Type", s => s.V.Type), new("bs", "Bill Status", s => BillTxt[s.Bill!.St], ["Raised", "Dispute"]),
            new("ps", "Payment Status", PayStatus, ["Pending", "Paid", "Sent for payment"]),
            new("ex", "Booking Executive", s => s.T.Exec), new("br", "Booking Branch", s => s.T.Branch),
        };
        var rows = ApplyF(pool, defs).Where(s => SearchOk(s.T.Id, s.V.Name, s.T.Pax)).ToList();
        var open = rows.Where(s => s.Bk == null).ToList();
        var kpis = new List<Kpi>
        {
            K("Vendors", open.Select(s => s.Vi).Distinct().Count(), Strong), K("Tickets Awaiting Submission", open.Select(s => s.Ti).Distinct().Count(), Blue),
            K("Total Value", Fmt.Inr(open.Sum(s => s.Neg)), Green), K("Disputed Bills", open.Count(s => s.Bill!.St == "D"), Red),
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

        Cell Chk(Svc s) => perm && s.Bk == null ? C(FinanceG.Chk(s, "avp", s.Bill!.Rec, "Bill " + s.Bill.No + " · " + Fmt.Fd(s.Bill.Date))) : Empty;
        static List<TipLine> GtTip(Ticket t) =>
            [new("Base Price", Fmt.Inr(t.Base)), new("Discount", "−" + Fmt.Inr(t.Disc), c: "red"), new("GST", "+" + Fmt.Inr(t.Gst), c: "blue"), new("Grand Total", Fmt.Inr(t.Grand), b: true, rule: true)];
        static Cell Aging(Svc s)
        {
            var d = Fmt.Days(s.Bill!.Date, Clock.Today);
            return Txt("right", d + "d", d > 14 ? Red : d > 7 ? Warn : Mut, d > 7 ? 600 : 400, num: true);
        }
        static Cell Period(Svc s) => C(T(s.Name), Sub((s.Conf != "" ? s.Conf : "Pending") + " · " + Fmt.Fd(s.Start) + " – " + Fmt.Fd(s.End)));
        static Cell TicketInfo(Svc s) => C(T(s.T.Id, Link, 700), Sub(s.T.Mbk), Chip(s.T.Status, ChipTone(s.T.Status)));
        static Cell PriceInfo(Ticket t)
        {
            var tb = t.Grand - t.Paid;
            return Ca("right", T(Fmt.Inr(t.Grand), num: true, dot: true, tip: GtTip(t)), T("Paid " + Fmt.Inr(t.Paid), Green, s: 11, num: true), T("Bal. " + (tb > 0 ? Fmt.Inr(tb) : "Nil"), tb > 0 ? Red : Green, s: 11, num: true));
        }
        List<Cell> Common(Svc s) =>
        [
            Ca("right", T("Sell " + Fmt.Inr(s.Sell), num: true), T("Cont. " + Fmt.Inr(s.Contr), Mut, s: 11, num: true), T("Neg. " + Fmt.Inr(s.Neg), Blue, 700, 11, num: true)),
            Variation(s), ProfitCell(s, 700), C(Chip(s.Status, ChipTone(s.Status))),
            Ca("right", T(s.AdvPaid > 0 ? Fmt.Inr(s.AdvPaid) : "-", s.AdvPaid > 0 ? Green : Mut, num: true), T(s.Bal > 0 ? Fmt.Inr(s.Bal) : "Nil", s.Bal > 0 ? Red : Green, 600, num: true)),
            Aging(s), C(Chip(PayStatus(s), PayStatusTone(PayStatus(s)))), C(Btn("View", Pop("viewbill", ("id", s.Id)))),
        ];
        object[] cmnH = [("Service Price Info", "right"), ("Variation", "right"), ("Profit (SP-CP) / %", "right"), "Service Status", ("Adv. Paid / Balance", "right"), ("Aging", "right"), "Payment Status", "Bill"];

        List<Col> cols; List<Row> gridRows; string count;
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
            var nh = Cols([.. new object[] { "", "Ticket Info", ("Price Info", "right"), "Bill Date / No.", "Service / Conf. No. / Period" }, .. cmnH]);
            cols = ActionLast(Cols("", "", "Vendor ID", "Vendor Name", "Service Type", "City", ("Total Business", "right"), ("Total Contracted", "right"), ("Total Negotiated", "right"), ("Total Profit", "right"), ("Total Unbilled Tickets", "center"), "Bill"));
            gridRows = order.Select(vi =>
            {
                var ss = groups[vi]; var v = st.V[vi];
                int pr = ss.Sum(s => s.Profit), cp = ss.Sum(s => s.Contr), neg = ss.Sum(s => s.Neg), ub = unbilled.GetValueOrDefault(vi);
                var dot = newB.Contains(vi) ? Ca("center", Dot([new TipLine("New bill(s) added")])) : Empty;
                return new Row
                {
                    Key = "v" + vi, SeenVendor = newB.Contains(vi) ? v.Id : null, SeenArea = "avp", CardHead = 4,
                    Cells =
                    [
                        dot, Txt("left", v.Id, Link, 700), Txt("left", v.Name, Strong, 600), C(Pill(v.Type)), Txt("left", v.City, Mut),
                        Txt("right", ss.Count > 0 ? Fmt.Inr(neg) : "-", Link, 800, num: true), Txt("right", ss.Count > 0 ? Fmt.Inr(cp) : "-", num: true), Txt("right", ss.Count > 0 ? Fmt.Inr(neg) : "-", Blue, num: true),
                        ss.Count > 0 ? Ca("right", T(Fmt.Inr(pr), pr < 0 ? Red : Green, 700, num: true), T(Fmt.Pct(pr, cp), Mut, s: 10.5)) : Txt("right", "-", Mut),
                        Ca("center", Chip(ub.ToString(), ub > 0 ? "warn" : "ok")),
                        C(Btn("View", Pop("vbills", ("vi", vi)), dis: ss.Count == 0)),
                    ],
                    Detail = ss.Count == 0 ? null : new Detail
                    {
                        Cols = nh,
                        Rows = ss.OrderBy(s => s.Ti).Select(List<Cell> (s) => [Chk(s), TicketInfo(s), PriceInfo(s.T), C(T(Fmt.Fd(s.Bill!.Date), num: true), Sub(s.Bill.No)), Period(s), .. Common(s)]).ToList(),
                    },
                };
            }).ToList();
        }
        else
        {
            var gs = Sorted(rows.GroupBy(s => s.Ti)).ToList();
            count = gs.Count + " tickets";
            var nh = Cols([.. new object[] { "", "Vendor", "Service Type / City", "Service / Conf. No. / Period" }, .. cmnH]);
            cols = Cols("", "Ticket / MBK", ("Tour Base Price", "right"), ("Additional Profit", "right"), ("Discount", "right"), ("GST", "right"), ("Grand Total", "right"), ("Paid by Customer", "right"), ("Balance Pending", "right"), "Ticket Status", "Bill Date / No.", "Payment Status", ("Recommended Amt.", "right"));
            gridRows = gs.Select(g =>
            {
                var ss = g.ToList(); var t = ss[0].T;
                int pay = ss.Sum(s => s.Neg), rec = ss.Sum(s => s.Bill!.Rec), extra = t.Grand - pay, bal = t.Grand - t.Paid;
                var dates = ss.Select(s => s.Bill!.Date).OrderBy(x => x, StringComparer.Ordinal).ToList();
                var nos = ss.Select(s => s.Bill!.No).Distinct().ToList();
                var ps = rec >= pay ? "Full Payment" : rec > 0 ? "Part Payment" : "Pending";
                return new Row
                {
                    Key = "t" + g.Key,
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

        var filters = MkFilters(pool, defs);
        filters.Add(new FilterVm("f_age", "Ageing sort", age is "" ? "All" : age, ["All", "Old to new", "New to old"]));
        return new ReportVm
        {
            Title = "Approved Vendor Payable", Pill = true, Perm = perm, Kpis = kpis, Query = q, Tabs = ViewTabs(view),
            Desc = "Every service whose bill has been added in the Vendor Payable Report. Tick services — from either view — and submit; an advice sheet lets you confirm before they move to the Payment Request Report.",
            Qph = "Search Ticket No., Vendor, Passenger...", Filters = filters, FCount = FCount(defs, false) + (age is "" or "All" ? 0 : 1), Count = count,
            SelLabel = perm ? "Submit" : null, SelPopup = PopUrl("advice"), Cart = true, Cols = cols, Rows = gridRows, ClearAllUrl = ClearAll,
            EmptyText = view == "vendor" ? "No billed vendor payables awaiting submission." : "No tickets awaiting submission.",
        };
    }

}
