using SouthernTravels.Areas.Finance.Models;
using SouthernTravels.Core.Infrastructure;
using SouthernTravels.Core.Models;
using static SouthernTravels.Core.Models.G;
using static SouthernTravels.Areas.Finance.Models.FinanceG;

namespace SouthernTravels.Areas.Finance.Reports;

/// <summary>Vendor Payable Report (Approval): the approver's (L2) view of the bills that L1 has approved.</summary>
public sealed partial class FinanceReports
{
    /// <summary>Pending (L2 not yet decided), Approved, Rejected or Disputed (L2 rejected it, or the bill itself is disputed).</summary>
    static string ApStatus(Svc s) =>
        s.Bill!.St == "D" ? "Disputed" : s.Bill.L2.St == "A" ? "Approved" : s.Bill.L2.St == "X" ? (s.Bill.L2.Kind == "Disputed" ? "Disputed" : "Rejected") : "Pending";

    static string ApGroup(string st) => st is "Pending" or "Approved" ? st : "Rejected / Disputed";
    /// <summary>Pending first, then approved, then rejected / disputed.</summary>
    static int ApRank(string st) => st switch { "Pending" => 0, "Approved" => 1, _ => 2 };

    static Cell ApChip(Svc s)
    {
        var st = ApStatus(s);
        var l2 = s.Bill!.L2;
        List<TipLine> tip = st switch
        {
            "Pending" => [new("Waiting for L2 approval")],
            "Approved" => [new(l2.By ?? "", "", b: true), new(Fmt.Fdt(l2.At))],
            _ when l2.St == "X" => [new((l2.Kind ?? "Rejected") + " by " + (l2.By ?? ""), "", b: true), new(l2.Reason ?? "")],
            _ => [new("Bill disputed"), new(s.Bill.Mr)],
        };
        return Ca("center", Chip(st, st == "Pending" ? "warn" : st == "Approved" ? "ok" : "danger", tip));
    }

    public ReportVm VendorPayableApproval()
    {
        var view = q.View == "ticket" ? "ticket" : "vendor";
        // Only bills L1 has approved reach the approver.
        var rows = st.Svcs().Where(s => s.Bill is { L1.St: "A" } && PerOk(s.Bill.Date)).ToList();
        var defs = new List<FDef<Svc>>
        {
            new("type", "Service Type", s => s.V.Type),
            new("as", "Approval Status", s => ApGroup(ApStatus(s)), ["Pending", "Approved", "Rejected / Disputed"]),
            new("ex", "Booking Executive", s => s.T.Exec), new("br", "Booking Branch", s => s.T.Branch),
        };
        var baseRows = rows;
        rows = ApplyF(rows, defs).Where(s => SearchOk(s.V.Name, s.V.LegalName, s.V.Id, s.T.Id, s.T.Mbk, s.T.Pax, s.T.File)).ToList();

        var pendingRows = rows.Where(s => ApStatus(s) == "Pending").ToList();
        var kpis = new List<Kpi>
        {
            K("Total No. Vendors", rows.Select(s => s.Vi).Distinct().Count(), Strong), K("Total Approvals", rows.Count, Blue),
            K("Pending Approvals", pendingRows.Count, Warn), K("Approvals Done", rows.Count(s => ApStatus(s) == "Approved"), Green),
            K("Pending Approval Amount", Fmt.Inr(pendingRows.Sum(s => s.Bill!.Inv)), Warn), K("Bills With Variance", rows.Count(s => s.Bill!.Inv != s.Neg), Red),
        };

        // Pending groups first.
        List<List<Svc>> Ordered(IEnumerable<IGrouping<int, Svc>> g) => g.Select(x => x.OrderBy(s => ApRank(ApStatus(s))).ThenBy(s => s.Ti).ToList())
            .OrderBy(ss => ss.Min(s => ApRank(ApStatus(s)))).ThenBy(ss => ss[0].Vi).ThenBy(ss => ss[0].Ti).ToList();
        var gs = Ordered(view == "vendor" ? rows.GroupBy(s => s.Vi) : rows.GroupBy(s => s.Ti));
        var (pgs, pager) = Paged(gs);

        // The bill rows of a group; the first cell is the ticket (vendor view) or the vendor (ticket view).
        var nh = Cols(view == "vendor" ? "Ticket Info" : "Vendor", ("Service Type / City", "center"), "Service / Details / Conf. No.", ("Bill Date / No. / Amount", "right"), ("Variance", "right"),
            ("Approval Status", "center"), ("Approval Levels", "center"), ("Actions", "center"));
        List<List<Cell>> BillRows(List<Svc> ss) => ss.Select(List<Cell> (s) =>
        {
            var b = s.Bill!;
            var pending = ApStatus(s) == "Pending";
            return
            [
                view == "vendor" ? TicketInfo(s.T) : VendorCell(s.V, true), Ca("center", Pill(s.V.Type), Sub(s.V.City)), SvcCell(s),
                Ca("right", T(Fmt.Inr(b.Inv), x: "color:var(--calc-ink);font-weight:800;font-size:13px;", num: true), T(Fmt.Fd(b.Date), num: true), Sub(b.No)),
                BillVariance(s), ApChip(s),
                Ca("center", Lv("L1", b.L1.St, ApTip(b.L1)), Lv("L2", b.L2.St, ApTip(b.L2)), Lv("L3", "-")),
                new Cell { Align = "center", Cls = "act-grid", Lines = new Line?[] { Btn("View Bill", Pop("viewbill", ("id", s.Id))), Btn("View Docs", Pop("avpdocs", ("id", s.Id))), Btn("History", Pop("aprhist", ("id", s.Id))),
                    pending && !readOnly ? ActBtn("Approve", "/Finance/VendorPayable/Approve?sid=" + s.Id, "primary") : null,
                    pending && !readOnly ? Btn("Reject", Pop("rejapv", ("id", s.Id)), "soft") : null }.OfType<Line>().ToList() },
            ];
        }).ToList();
        static Cell Counts(List<Svc> ss)
        {
            int pend = ss.Count(s => ApStatus(s) == "Pending"), done = ss.Count(s => ApStatus(s) == "Approved");
            return Ca("left", T("Total " + ss.Count, num: true), T("Pending " + pend, pend > 0 ? Warn : Mut, 600, 11, num: true), T("Done " + done, Green, s: 11, num: true));
        }

        List<Col> cols;
        List<Row> gridRows;
        if (view == "vendor")
        {
            cols = Cols("", "Vendor ID", "Vendor Name", ("Total Tickets", "center"), "Approvals (Total / Pending / Done)", ("Total Business", "right"));
            gridRows = pgs.Select(ss => new Row
            {
                Key = "v" + ss[0].Vi,
                Cells = [Txt("left", ss[0].V.Id, Link, 700), VendorCell(ss[0].V), Txt("center", ss.Select(s => s.Ti).Distinct().Count().ToString(), w: 600, num: true), Counts(ss),
                    Txt("right", Fmt.Inr(ss.Sum(s => s.Bill!.Rec)), Link, 800, num: true)],
                Detail = new Detail { Cols = nh, CardHead = 2, Rows = BillRows(ss) },
            }).ToList();
        }
        else
        {
            cols = Cols("", "Ticket Info", "Passenger Info", "Booking Executive", "Approvals (Total / Pending / Done)", ("Ticket Status", "center"), ("Total Business", "right"));
            gridRows = pgs.Select(ss =>
            {
                var t = ss[0].T;
                return new Row
                {
                    Key = "t" + ss[0].Ti,
                    Cells = [TicketInfo(t, false), C(T(t.Pax, Strong, 600), Sub(t.PaxInfo)), C(T(t.Exec), Sub(t.BranchCode != "" ? t.BranchCode : t.Branch)), Counts(ss),
                        Ca("center", StatusText(t.Status)), Txt("right", Fmt.Inr(ss.Sum(s => s.Bill!.Rec)), Link, 800, num: true)],
                    Detail = new Detail { Cols = nh, CardHead = 2, Rows = BillRows(ss) },
                };
            }).ToList();
        }

        return new ReportVm
        {
            Title = "Vendor Payable Report", Pill = !readOnly, Kpis = kpis, Query = q, Tabs = ViewTabs(view),
            Desc = "Bills already approved by L1 that are waiting for your L2 approval — review the bill and documents, then approve or reject." + (readOnly ? " You have read-only access." : ""),
            Qph = "Search Vendor Name, Legal Name, Ticket No., MBK No., Passenger, File No...", Filters = MkFilters(baseRows, defs), FCount = FCount(defs, false),
            Count = gs.Count + (view == "vendor" ? " vendors" : " tickets"), Cols = cols,
            Rows = gridRows, Pager = pager, EmptyText = "No bills waiting for approval.", ClearAllUrl = ClearAll,
        };
    }
}
