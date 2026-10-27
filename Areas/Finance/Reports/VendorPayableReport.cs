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
        // Every operated service of a confirmed ticket; there is no approval filter (approval is shown per row).
        var rows = all.Where(s => s.Ops != 'N' && visT.Contains(s.Ti) && PerOk(s.Bill?.Date ?? s.End)).ToList();
        var defs = new List<FDef<Svc>>
        {
            new("type", "Service Type", s => s.V.Type), new("city", "Service City", s => s.V.City),
            new("ts", "Ticket Status", s => s.T.Status, ["Confirmed", "Pending", "Cancelled"]),
            new("bs", "Bill Status", s => BillTxt[s.BStat], ["Pending", "Uploaded", "Disputed"]),
            new("ex", "Booking Executive", s => s.T.Exec), new("br", "Booking Branch", s => s.T.Branch),
        };
        var baseRows = rows;
        rows = ApplyF(rows, defs).Where(s => (q.From == "" || Fmt.Cmp(s.End, q.From) >= 0) && (q.To == "" || Fmt.Cmp(s.Start, q.To) <= 0)
            && SearchOk(s.V.Name, s.V.LegalName, s.V.Id, s.T.Id, s.T.Mbk, s.T.Pax, s.T.File)).ToList();

        var kpis = new List<Kpi>
        {
            K("Total Payable Amount", Fmt.Inr(rows.Sum(s => s.Neg)), Blue),
            K("Total No. Vendors", rows.Select(s => s.Vi).Distinct().Count(), Strong),
            K("Total Paid Till Now", Fmt.Inr(rows.Sum(s => s.AdvPaid)), Green),
            K("Balance Pending", Fmt.Inr(rows.Sum(s => s.Bal)), Red),
            K("Bill Pending", rows.Count(s => s.BStat == "N"), Warn),
            K("Disputed Tickets", rows.Where(s => s.BStat == "D").Select(s => s.Ti).Distinct().Count(), Red),
        };

        // Upload for every service that has no bill; once a bill exists it can be viewed or reuploaded.
        Cell Act(Svc s) => readOnly ? (s.Bill == null ? Empty : Ca("center", Btn("View Bill Details", Pop("viewbill", ("id", s.Id))))) : s.Bill == null
            ? Ca("center", Btn("Upload", Pop("raise", ("ids", s.Id)), "primary"))
            : Ca("center", Btn("View Bill Details", Pop("viewbill", ("id", s.Id))), Btn("Reupload Bill", Pop("raise", ("ids", s.Id), ("re", 1))));
        static Cell Appr(Svc s) => s.Bill != null
            ? Ca("center", Lv("L1", s.Bill.L1.St, ApTip(s.Bill.L1)), Lv("L2", s.Bill.L2.St, ApTip(s.Bill.L2)))
            : Txt("center", "-", Mut);
        Cell Chk(Svc s) => s.Eligible && !readOnly ? C(FinanceG.Chk(s, "vp", s.Neg, Fmt.Fd(s.Start) + " – " + Fmt.Fd(s.End))) : Empty;
        // Columns after the service columns, the same in both views.
        List<Cell> Money(Svc s) =>
        [
            PriceInfo3(s),
            Txt("right", s.Bill != null ? Fmt.Inr(s.Bill.Inv) : "-", s.Bill != null ? null : Mut, num: true), BillVariance(s),
            AdvPaidCell(s), BalCell(s), PayStatusCell(s),
            Ca("center", Chip(BillTxt[s.BStat], BillTone[s.BStat])), Appr(s), Act(s),
        ];
        object[] mHead = [("Price Info", "right"), ("Amount as per Bill", "right"), ("Variance", "right"), ("Advance Paid", "right"), ("Balance Pending", "right"),
            ("Payment Status", "center"), ("Bill Status", "center"), ("Approval", "center"), ("Actions", "center")];
        object[] svcHead = ["Service / Details / Conf. No.", ("Voucher No.", "center"), ("Service Dates", "center")];
        static int Pend(List<Svc> ss) => ss.Count(s => s.BStat != "R");
        static Cell BpCell(int n) => Ca("center", Chip(n.ToString(), n > 0 ? "danger" : "ok"));

        List<Col> cols; List<Row> gridRows; string count; PagerVm pager;
        if (view == "vendor")
        {
            var gs = rows.GroupBy(s => s.Vi).OrderBy(g => g.Key).Select(g => g.ToList()).ToList();
            count = gs.Count + " vendors";
            var nh = Cols([.. new object[] { "", "Ticket Info", ("Service Type / City", "center") }, .. svcHead, .. mHead]);
            cols = Cols("", "Vendor ID", "Vendor Name", ("Total Services", "center"), ("Total Duration", "right"), ("Bills Pending", "center"), ("Total Business", "right"), ("Amount Paid", "right"), ("Balance Pending", "right"));
            var (pgs, pg) = Paged(gs); pager = pg;
            gridRows = pgs.Select(ss =>
            {
                var v = ss[0].V; var bal = ss.Sum(s => s.Bal);
                return new Row
                {
                    Key = "v" + ss[0].Vi,
                    Cells =
                    [
                        Txt("left", v.Id, Link, 700), VendorCell(v), Txt("center", ss.Count.ToString(), w: 600, num: true), Txt("right", ss.Sum(s => s.Dur) + "d", num: true),
                        BpCell(Pend(ss)), Txt("right", Fmt.Inr(ss.Sum(s => s.Neg)), Link, 800, num: true),
                        Txt("right", Fmt.Inr(ss.Sum(s => s.AdvPaid)), Green, num: true), Txt("right", bal > 0 ? Fmt.Inr(bal) : "Nil", bal > 0 ? Red : Green, 600, num: true),
                    ],
                    Detail = new Detail
                    {
                        Cols = nh,
                        Rows = ss.Select(List<Cell> (s) =>
                            [Chk(s), TicketInfo(s.T), Ca("center", Pill(s.V.Type), Sub(s.V.City)), SvcCell(s), VoucherCell(s), DatesCentered(s), .. Money(s)]).ToList(),
                    },
                };
            }).ToList();
        }
        else
        {
            var gs = rows.GroupBy(s => s.Ti).OrderBy(g => g.Key).Select(g => g.ToList()).ToList();
            count = gs.Count + " tickets";
            var nh = Cols([.. new object[] { "", "Vendor", ("Service Type / City", "center") }, .. svcHead, .. mHead]);
            cols = Cols("", "Ticket Info", "Passenger Info", "Booking Executive", ("Base Price", "right", "Base price + Additional profit"), ("Discount", "right"), ("GST", "right"),
                ("Grand Total", "right"), ("Balance", "right"), ("Ticket Status", "center"), ("Bills Pending", "center"), ("Total Business", "right"));
            var (pgs, pg) = Paged(gs); pager = pg;
            gridRows = pgs.Select(ss =>
            {
                var t = ss[0].T; var bal = t.Grand - t.Paid;
                return new Row
                {
                    Key = "t" + ss[0].Ti,
                    Cells =
                    [
                        TicketInfo(t, false), C(T(t.Pax, Strong, 600), Sub(t.PaxInfo)), C(T(t.Exec), Sub(t.BranchCode != "" ? t.BranchCode : t.Branch)),
                        Txt("right", Fmt.Inr(t.Base), num: true),
                        Txt("right", t.Disc > 0 ? "−" + Fmt.Inr(t.Disc) : "-", t.Disc > 0 ? Red : Mut, num: true), Txt("right", "+" + Fmt.Inr(t.Gst), Mut, num: true),
                        Txt("right", Fmt.Inr(t.Grand), w: 700, num: true), Txt("right", bal > 0 ? Fmt.Inr(bal) : "Nil", bal > 0 ? Red : Green, 600, num: true),
                        Ca("center", StatusText(t.Status)), BpCell(Pend(ss)), Txt("right", Fmt.Inr(ss.Sum(s => s.Neg)), Link, 800, num: true),
                    ],
                    Detail = new Detail
                    {
                        Cols = nh,
                        Rows = ss.Select(List<Cell> (s) =>
                            [Chk(s), VendorCell(s.V, true), Ca("center", Pill(s.V.Type), Sub(s.V.City)), SvcCell(s), VoucherCell(s), DatesCentered(s), .. Money(s)]).ToList(),
                    },
                };
            }).ToList();
        }

        return new ReportVm
        {
            Title = "Vendor Payable Report", Pill = true, Kpis = kpis, Query = q, Tabs = ViewTabs(view),
            Desc = "Consolidated view of amounts owed to each vendor — covering contracted rate, negotiated amount, variance against the bill, and net vendor payable per ticket and service, plus Finance’s bill entry, approval (L1/L2), and payment-action tracking per service.",
            Qph = "Search Vendor Name, Legal Name, Ticket No., MBK No., Passenger, File No...", Filters = MkFilters(baseRows, defs), FCount = FCount(defs, true),
            DateLabel = "Service Date Range (overlap)", Count = count, SelLabel = readOnly ? null : "Upload Bill", SelPopup = PopUrl("raise"),
            Cols = cols, Rows = gridRows, Pager = pager, EmptyText = view == "vendor" ? "No vendors found." : "No tickets found.", ClearAllUrl = ClearAll,
        };
    }
}
