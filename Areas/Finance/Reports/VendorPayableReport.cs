using SouthernTravels.Areas.Finance.Data;
using SouthernTravels.Areas.Finance.Models;
using SouthernTravels.Core.Infrastructure;
using SouthernTravels.Core.Models;
using static SouthernTravels.Core.Models.G;
using static SouthernTravels.Areas.Finance.Models.FinanceG;

namespace SouthernTravels.Areas.Finance.Reports;

/// <summary>Vendor Payable Report.</summary>
public sealed partial class FinanceReports
{
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
            new("bs", "Bill Status", s => BillTxt[s.BStat], ["Pending", "Uploaded", "Disputed"]),
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
            if (s.BStat == "N")
                return s.Eligible
                    ? C(Btn("Upload", Pop("raise", ("ids", s.Id)), "primary"))
                    : C(T("Available after " + Fmt.Fd(s.End), Mut, it: true));
            return C(Btn("View Bill Details", Pop("viewbill", ("id", s.Id))), Btn("Reupload Bill", Pop("raise", ("ids", s.Id), ("re", 1))));
        }
        Cell Appr(Svc s) => s.Bill != null ? C(Lv("L1", s.Bill.L1.St, ApTip(s.Bill.L1)), Lv("L2", s.Bill.L2.St, ApTip(s.Bill.L2))) : Txt("left", "-", Mut);
        Cell Chk(Svc s) => s.Eligible ? C(FinanceG.Chk(s, "vp", s.Neg, Fmt.Fd(s.Start) + " – " + Fmt.Fd(s.End))) : Empty;
        List<Cell> Money(Svc s) =>
        [
            Ca("right", T("Sell " + Fmt.Inr(s.Sell), num: true), T("Contr " + Fmt.Inr(s.Contr), num: true), T("Neg " + Fmt.Inr(s.Neg), Blue, 600, num: true)),
            ProfitCell(s, 600),
            Txt("right", s.AdvPaid > 0 ? Fmt.Inr(s.AdvPaid) : "-", s.AdvPaid > 0 ? Green : Mut, num: true),
            Txt("right", s.Bal > 0 ? Fmt.Inr(s.Bal) : "Nil", s.Bal > 0 ? Red : Green, 600, num: true),
            C(Chip(BillTxt[s.BStat], BillTone[s.BStat])), Appr(s),
            Txt("right", s.Bill != null ? Fmt.Inr(s.Bill.Inv) : "-", s.Bill != null ? null : Mut, num: true), Variation(s), Act(s),
        ];
        object[] mHead = [("Selling / Contracted / Negotiated", "right"), ("Profit / % on CP · SP", "right"), ("Advance Paid", "right"), ("Balance to Pay", "right"), "Bill Status", "Approval (L1/L2)", ("Bill Amount", "right"), ("Variation", "right"), "Actions"];
        static int Pend(List<Svc> ss) => ss.Count(s => s.BStat != "R");
        static Cell BpCell(int n) => Ca("center", Chip(n.ToString(), n > 0 ? "danger" : "ok"));
        static Cell ConfCell(Svc s) => C(T(s.Name), Sub(s.Conf != "" ? s.Conf : "Pending"));

        List<Col> cols; List<Row> gridRows; string count; PagerVm pager;
        if (view == "vendor")
        {
            var gs = rows.GroupBy(s => s.Vi).OrderBy(g => g.Key).Select(g => g.ToList()).ToList();
            count = gs.Count + " vendors";
            var nh = Cols([.. new object[] { "", "Ticket / File No. / MBK", "Name of Service / Conf. No.", "Service Dates" }, .. mHead]);
            cols = Cols("", "Vendor ID", "Vendor Name", "Service Type(s)", "City/Cities", ("Total Services", "center"), ("Total Duration", "right"), ("Bills Uploaded", "center"), ("Bills Pending", "center"), ("Total Profit Earned", "right"), ("Total Business", "right"));
            var (pgs, pg) = Paged(gs); pager = pg;
            gridRows = pgs.Select(ss =>
            {
                var v = ss[0].V; var prof = ss.Sum(s => s.Profit);
                return new Row
                {
                    Key = "v" + ss[0].Vi,
                    Cells =
                    [
                        Txt("left", v.Id, Link, 700), Txt("left", v.Name, Strong, 600), new Cell { Lines = ss.Select(s => s.V.Type).Distinct().Select(Pill).ToList() },
                        Txt("left", v.City, Mut), Txt("center", ss.Count.ToString(), w: 600, num: true), Txt("right", ss.Sum(s => s.Dur) + "d", num: true), Ca("center", Chip(ss.Count(s => s.BStat == "R").ToString(), "ok")), BpCell(Pend(ss)),
                        Txt("right", Fmt.Inr(prof), prof < 0 ? Red : Green, 600, num: true), Txt("right", Fmt.Inr(ss.Sum(s => s.Neg)), Link, 800, num: true),
                    ],
                    Detail = new Detail
                    {
                        Cols = nh,
                        Rows = ss.Select(List<Cell> (s) => [Chk(s), C(T(s.T.Id, Link, 700), Sub("File " + s.T.File), Sub("MBK " + s.T.Mbk)), ConfCell(s), Dates(s), .. Money(s)]).ToList(),
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
            var (pgs, pg) = Paged(gs); pager = pg;
            gridRows = pgs.Select(ss =>
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
            Title = "Vendor Payable Report", Pill = true, Kpis = kpis, Query = q, Tabs = ViewTabs(view),
            Desc = "Consolidated view of amounts owed to each vendor — covering contracted rate, negotiated amount, variation, and net vendor payable per ticket and service, plus Finance’s bill entry, approval (L1/L2), and payment-action tracking per service.",
            Qph = "Search Vendor Name, Ticket No., MBK No., Passenger, File No...", Filters = MkFilters(baseRows, defs), FCount = FCount(defs, true),
            DateLabel = "Service Date Range (overlap)", Count = count, SelLabel = "Upload Bill", SelPopup = PopUrl("raise"),
            Cols = cols, Rows = gridRows, Pager = pager, EmptyText = view == "vendor" ? "No vendors found." : "No tickets found.", ClearAllUrl = ClearAll,
        };
    }
}
