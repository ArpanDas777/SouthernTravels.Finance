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

    public ReportVm Advance()
    {
        var all = st.Svcs();
        List<AdvanceRequest> reqs;
        lock (st.Gate) reqs = st.Adv.ToList();
        // Only fully approved (L1 and L2) requests appear.
        var rows = reqs.Where(a => a is { L1: "A", L2: "A" } && PerOk(a.At))
            .Select(a => { var s = all.First(x => x.Id == a.Sid); return new AdvRow(a, s, Fmt.Add(s.Start, -3)); }).ToList();
        var defs = new List<FDef<AdvRow>>
        {
            new("ps", "Adv. Payment Status", r => r.A.Pay != null ? "Paid" : "Pending", ["Pending", "Paid"]),
            new("ou", "OPS User", r => r.A.By), new("st", "Service Type", r => r.S.V.Type),
        };
        var baseRows = rows;
        rows = ApplyF(rows, defs).Where(r => (q.From == "" || Fmt.Cmp(r.S.Start, q.From) >= 0) && (q.To == "" || Fmt.Cmp(r.S.Start, q.To) <= 0)
            && SearchOk(r.S.T.Mbk, r.S.T.Id, r.S.T.Pax, r.S.V.Name, r.S.Name)).ToList();
        if (q.Sort is "start" or "due")
            rows = rows.OrderBy(r => r, Comparer<AdvRow>.Create((x, y) =>
                (q.Sort == "due" ? Fmt.Cmp(x.Due, y.Due) : Fmt.Cmp(x.S.Start, y.S.Start)) * q.Dir)).ToList();
        var (pageRows, pager) = Paged(rows);

        int Sum(Func<AdvRow, int> f, Func<AdvRow, bool> p) => rows.Where(p).Sum(f);
        var kpis = new List<Kpi>
        {
            K("Approved Requests", rows.Count, Strong), K("Paid", rows.Count(r => r.A.Pay != null), Green),
            K("Payment Pending", rows.Count(r => r.A.Pay == null), Warn),
            K("Total Advance Paid", Fmt.Inr(Sum(r => r.A.Amt, r => r.A.Pay != null)), Green),
            K("Pending Payment Amt.", Fmt.Inr(Sum(r => r.A.Amt, r => r.A.Pay == null)), Blue),
            K("Outstanding Balance", Fmt.Inr(Sum(r => r.S.Bal, _ => true)), Red),
        };

        Col SortTh(Col th, string k) => new()
        {
            L = th.L + (q.Sort == k ? (q.Dir > 0 ? " ▲" : " ▼") : " ⇅"), Align = th.Align, SortActive = q.Sort == k,
            SortUrl = q.Url(c => { c.Sort = k; c.Dir = q.Sort == k && q.Dir > 0 ? -1 : 1; c.Page = 1; }),
        };
        var cols = ActionLast(Cols("", "MBK / Ticket", "Ticket Status", "Passenger / Pax", "Booking Exec / Branch", "Tour Period", "Vendor / Service Type / Service", "Service Start", "Due Date", ("Service Amt. / Balance / Prev. Adv. Paid", "right"), ("Adv. Requested", "right"), "Requested By", "Approvals", "Adv. Payment", "Action")
            .Select(c => c.L == "Service Start" ? SortTh(c, "start") : c.L == "Due Date" ? SortTh(c, "due") : c).ToList());

        static Fact F(string l, string v, string? c = null, bool b = false) => new() { L = l, V = v, Color = c, Bold = b, Num = true };

        var gridRows = pageRows.Select(r =>
        {
            var (a, s) = (r.A, r.S);
            var t = s.T;
            var tsv = all.Where(x => x.Ti == s.Ti).ToList();
            var ts = tsv.Select(x => x.Start).OrderBy(x => x, StringComparer.Ordinal).First();
            var te = tsv.Select(x => x.End).OrderBy(x => x, StringComparer.Ordinal).Last();
            var bal = t.Grand - t.Paid;
            var payCell = a.Pay != null ? C(Chip("Paid", "ok"), Sub(Fmt.Fdt(a.Pay.Date)))
                : C(Chip("Pending", "warn"));
            var actCell = a.Pay != null ? C(Btn("View Payment Details", Pop("advview", ("sid", a.Sid))), LinkBtn("Print Advice", AdviceUrl(a.Sid)))
                : C(Btn("Update Payment", Pop("advpay", ("sid", a.Sid)), "primary"), LinkBtn("Print Advice", AdviceUrl(a.Sid)));
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
            Title = "Advance Report", Pill = true, Kpis = kpis, Query = q,
            Desc = "Service-wise advance payment tracking — shows advance requests raised by OPS that are fully approved (L1 and L2), with their payment status. Once a payment is recorded the entry stays and is marked Paid.",
            Qph = "Search MBK, Ticket No., Passenger, Vendor, Service...", Filters = MkFilters(baseRows, defs), FCount = FCount(defs, true),
            DateLabel = "Tour Start Date", Count = rows.Count + " requests", Cols = cols, Rows = gridRows, EmptyText = "No advance requests found.", Pager = pager, ClearAllUrl = ClearAll,
        };
    }

}
