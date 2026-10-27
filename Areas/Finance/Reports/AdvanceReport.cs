using SouthernTravels.Areas.Finance.Data;
using SouthernTravels.Areas.Finance.Models;
using SouthernTravels.Core.Infrastructure;
using SouthernTravels.Core.Models;
using static SouthernTravels.Core.Models.G;
using static SouthernTravels.Areas.Finance.Models.FinanceG;

namespace SouthernTravels.Areas.Finance.Reports;

/// <summary>Advance Report.</summary>
public sealed partial class FinanceReports
{
    sealed record AdvRow(AdvanceRequest A, Svc S, string Due);

    /// <summary>The approver's status of an advance: Pending (L1 approved, L2 not yet decided), Approved (L2), Rejected, or Awaiting L1.</summary>
    /// <summary>Hover text of a rejection: who rejected at which level, the reason (or "-") and when.</summary>
    static List<TipLine> RejTip(AdvanceRequest a) =>
        [new("Rejected by " + (a.Rej?.By ?? (a.L1 == "X" ? "L1" : a.L2By ?? "")) + " (" + (a.Rej?.Lvl ?? (a.L1 == "X" ? "L1" : "L2")) + ")", "", b: true), new("Reason: " + (string.IsNullOrWhiteSpace(a.Rej?.Reason) ? "-" : a.Rej!.Reason)), new(Fmt.Fdt(a.Rej?.At ?? a.L2At))];

    static string AdvAp(AdvanceRequest a) => a.L1 == "X" || a.L2 == "X" ? "Rejected" : a.L2 == "A" ? "Approved" : a.L1 == "A" ? "Pending" : "Awaiting L1";

    public ReportVm Advance()
    {
        var all = st.Svcs();
        List<AdvanceRequest> reqs; int bulkCount;
        lock (st.Gate) { reqs = st.Adv.ToList(); bulkCount = st.BulkLog.Count; }
        // Only fully approved (L1 and L2) requests appear.
        var rows = reqs.Where(a => (approver || a.Approved) && PerOk(a.At))
            .Select(a => { var s = all.First(x => x.Id == a.Sid); return new AdvRow(a, s, Fmt.Add(s.Start, -3)); }).ToList();
        var defs = new List<FDef<AdvRow>>
        {
            approver ? new("ps", "Approval Status", r => AdvAp(r.A), ["Pending", "Approved", "Rejected"])
                : new("ps", "Adv. Payment Status", r => r.A.Paid ? "Paid" : "Pending", ["Pending", "Paid"]),
            new("ou", "OPS User", r => r.A.By), new("st", "Service Type", r => r.S.V.Type),
        };
        var baseRows = rows;
        rows = ApplyF(rows, defs).Where(r => (q.From == "" || Fmt.Cmp(r.S.Start, q.From) >= 0) && (q.To == "" || Fmt.Cmp(r.S.Start, q.To) <= 0)
            && SearchOk(r.S.T.Mbk, r.S.T.Id, r.S.T.Pax, r.S.T.File, r.S.V.Name, r.S.V.LegalName, r.S.Name, r.S.Hotel)).ToList();
        if (q.Sort is "start" or "due")
            rows = rows.OrderBy(r => r, Comparer<AdvRow>.Create((x, y) =>
                (q.Sort == "due" ? Fmt.Cmp(x.Due, y.Due) : Fmt.Cmp(x.S.Start, y.S.Start)) * q.Dir)).ToList();
        var (pageRows, pager) = Paged(rows);

        int Sum(Func<AdvRow, int> f, Func<AdvRow, bool> p) => rows.Where(p).Sum(f);
        var kpis = approver ? new List<Kpi>
        {
            K("Total Requests", rows.Count, Strong), K("Pending My Approval (L2)", rows.Count(r => AdvAp(r.A) == "Pending"), Warn),
            K("Pending Approval Amount", Fmt.Inr(Sum(r => r.A.Amt, r => AdvAp(r.A) == "Pending")), Warn),
            K("Approved (L2)", rows.Count(r => AdvAp(r.A) == "Approved"), Green), K("Rejected", rows.Count(r => AdvAp(r.A) == "Rejected"), Red),
        } : new List<Kpi>
        {
            K("Total Requests", rows.Count, Strong), K("Completed Advances", rows.Count(r => r.A.Paid), Green),
            K("Pending Advances", rows.Count(r => !r.A.Paid), Warn),
            K("Total Advance Paid", Fmt.Inr(Sum(r => r.A.Amt, r => r.A.Paid)), Green),
            K("Outstanding Balance", Fmt.Inr(Sum(r => r.S.Bal, _ => true)), Red),
        };

        Col SortTh(Col th, string k) => new()
        {
            L = th.L + (q.Sort == k ? (q.Dir > 0 ? " ▲" : " ▼") : " ⇅"), Align = th.Align, SortActive = q.Sort == k,
            SortUrl = q.Url(c => { c.Sort = k; c.Dir = q.Sort == k && q.Dir > 0 ? -1 : 1; c.Page = 1; }),
        };
        // The second column holds the Bulk Payment ticks and is only visible in bulk mode.
        var cols = ActionLast(Cols("", "", "Ticket Info", "Passenger Info", "Tour Period", "Service Details", "Service Start", "Due Date", ("Service Price Info", "right"),
                ("Adv. Requested", "center"), "Requested By", ("Approvals", "center"), ("Payment Status", "center"), ("Action", "center"))
            .Select(c => c.L == "Service Start" ? SortTh(c, "start") : c.L == "Due Date" ? SortTh(c, "due") : c).ToList());
        cols[1] = new Col { L = "", Cls = "bulk-col" };
        // The approver has no bulk ticks and no payment status.
        if (approver) cols = cols.Where((c, i) => i != 1 && c.L != "Payment Status").ToList();

        static Fact F(string l, string v, string? c = null, bool b = false) => new() { L = l, V = v, Color = c, Bold = b, Num = true };
        List<Cell> Pick(List<Cell> c) => approver ? [.. c.Where((_, i) => i != 0 && i != c.Count - 2)] : c;

        var gridRows = pageRows.Select(r =>
        {
            var (a, s) = (r.A, r.S);
            var t = s.T;
            var tsv = all.Where(x => x.Ti == s.Ti).ToList();
            var ts = tsv.Select(x => x.Start).OrderBy(x => x, StringComparer.Ordinal).First();
            var te = tsv.Select(x => x.End).OrderBy(x => x, StringComparer.Ordinal).Last();
            var bal = t.Grand - t.Paid;
            // Two steps: payment details (no UTR) first, then the UTR, which makes the advance Paid.
            var payCell = a.Paid ? Ca("center", Chip("Paid", "ok"), Sub(Fmt.Fdt(a.Pay!.Date)))
                : a.Pay != null ? Ca("center", Chip("Pending", "warn"), Sub("Details added · UTR awaited"))
                : Ca("center", Chip("Pending", "warn"));
            var print = a.Pay != null;
            Cell actCell = readOnly ? (a.Paid ? Ca("center", Btn("View Payment Details", Pop("advview", ("sid", a.Sid)))) : Empty) : a.Paid ? Ca("center", Btn("View Payment Details", Pop("advview", ("sid", a.Sid))), LinkBtn("Print Advice", AdviceUrl(a.Sid)))
                : Ca("center", Btn(print ? "Update Payment Details" : "Add Payment Details", Pop("advpay", ("sid", a.Sid)), "primary"),
                    LinkBtn("Print Advice", AdviceUrl(a.Sid), dis: !print), Btn("Print Cheque", toast: "Cheque printing is not available yet.", dis: !print));
            // Approver (L2): approve or reject once L1 has approved; otherwise show where the request stands.
            if (approver)
                actCell = a.L1 == "X" || a.L2 == "X" ? Ca("center", Chip("Rejected", "danger", RejTip(a)))
                    : a.L2 == "A" ? Txt("center", "Approved", Green, 600)
                    : a.L1 != "A" ? Txt("center", "Awaiting L1", Mut)
                    : readOnly ? Txt("center", "Awaiting L2", Warn)
                    : Ca("center", ActBtn("Approve", "/Finance/Advance/Approve?sid=" + a.Sid, "primary"), Btn("Reject", Pop("advrej", ("sid", a.Sid)), "soft"));
            Line Ap(string l, string c)
            {
                var (by, at) = l == "L1" ? ("Vijay Gupta", Fmt.Add(a.At, 1) + " 10:05") : (a.L2By ?? "Sowmya", a.L2At ?? Fmt.Add(a.At, 1) + " 16:40");
                // The ✕ of the level that was rejected carries who, why and when.
                if (c == "X" && a.Rej?.Lvl == l) return Lv(l, c, RejTip(a));
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
            // Bulk mode ticks only advances that have no payment details yet.
            var bulkCell = new Cell { Cls = "bulk-col", Lines = !readOnly && a.Pay == null ? [FinanceG.Chk(s, "advbulk", a.Amt, "Advance " + Fmt.Inr(a.Amt))] : [] };
            return new Row
            {
                Key = a.Sid, Detail = detail,
                // The approver has no bulk tick (first cell) and no payment status (second-last).
                Cells = Pick([
                    bulkCell,
                    TicketInfo(t), C(T(t.Pax, w: 600), Sub(t.PaxInfo)),
                    C(T(Fmt.Fd(ts), num: true), Sub("to " + Fmt.Fd(te)), Sub(Fmt.Days(ts, te) + 1 + "d")),
                    C([.. VendorLines(s.V), Pill(s.V.Type), .. SvcLines(s, false)]),
                    Txt("left", Fmt.Fd(s.Start), num: true), Txt("left", Fmt.Fd(r.Due), Warn, 700, num: true),
                    Ca("right", T("Payable " + Fmt.Inr(s.Neg), w: 700, num: true), T("Balance " + (s.Bal > 0 ? Fmt.Inr(s.Bal) : "Nil"), s.Bal > 0 ? Red : Green, s: 11, num: true),
                        T("Adv. Paid " + (s.AdvPaid > 0 ? Fmt.Inr(s.AdvPaid) : "-"), s.AdvPaid > 0 ? Green : Mut, s: 11, num: true)),
                    Ca("center", T(Fmt.Inr(a.Amt), Blue, 800, 14, num: true, x: "background:var(--info-bg);border:1px solid var(--info-rule);border-radius:6px;padding:2px 8px;"), T(Fmt.Fdt(a.At), Mut, s: 10.5)),
                    C(T(a.By), Sub(a.Br)), Ca("center", Ap("L1", a.L1), Ap("L2", a.L2)), payCell, actCell,
                ]),
            };
        }).ToList();

        return new ReportVm
        {
            Title = "Advance Report", Pill = true, Kpis = kpis, Query = q,
            Desc = approver
                ? "Advance requests raised by OPS that need your approval (L2). Review the request, then approve or reject — L1 must have approved first."
                : "Service-wise advance payment tracking — shows advance requests raised by OPS that are fully approved (L1 and L2). Payment is recorded in two steps: add the payment details (then the advice can be printed), and later the UTR, which marks the advance Paid. Several advances can be paid together with Bulk Payment.",
            Qph = "Search MBK, Ticket No., Passenger, Vendor, Service...", Filters = MkFilters(baseRows, defs), FCount = FCount(defs, true),
            DateLabel = "Tour Start Date", Count = rows.Count + " requests", Cols = cols, Rows = gridRows, EmptyText = "No advance requests found.", Pager = pager, ClearAllUrl = ClearAll,
            Bulk = readOnly || approver ? null : new BulkBar(PopUrl("bulkPay"), PopUrl("bulkHist"), bulkCount),
        };
    }

}
