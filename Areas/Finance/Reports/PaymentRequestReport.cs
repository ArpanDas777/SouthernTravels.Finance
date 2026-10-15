using SouthernTravels.Areas.Finance.Data;
using SouthernTravels.Areas.Finance.Models;
using SouthernTravels.Core.Infrastructure;
using SouthernTravels.Core.Models;
using static SouthernTravels.Core.Models.G;
using static SouthernTravels.Areas.Finance.Models.FinanceG;

namespace SouthernTravels.Areas.Finance.Reports;

/// <summary>Payment Request report.</summary>
public sealed partial class FinanceReports
{
    sealed record PrRow(BasketItem B, Svc S);

    public ReportVm PaymentRequest()
    {
        var all = st.Svcs();
        List<BasketItem> basket; Dictionary<string, bool> dots; int rtgsCount;
        lock (st.Gate) { basket = st.Basket.ToList(); dots = new(st.Dots); rtgsCount = st.RtgsLog.Count; }
        var rows = basket.Select(b => new PrRow(b, all.First(x => x.Id == b.Sid)))
            .Where(r => PerOk(r.S.Bill?.Date ?? r.S.End)).ToList();
        var defs = new List<FDef<PrRow>>
        {
            new("ps", "Payment Status", r => r.B.Paid ? "Paid" : "Pending", ["Pending", "Paid"]),
            new("pm", "Payment Mode", r => r.B.Pm ?? "RTGS", FinanceOptions.PlanModes),
            new("rg", "RTGS Advice", r => r.B.RtgsNo != null ? "Created" : "Not created", ["Created", "Not created"]),
            new("type", "Service Type", r => r.S.V.Type), new("vendor", "Vendor", r => r.S.V.Name),
        };
        var baseRows = rows;
        rows = ApplyF(rows, defs).Where(r => SearchOk(r.S.T.Id, r.S.V.Name, r.S.Name)).ToList();
        var gs = rows.GroupBy(r => r.S.Vi).Select(g => (Vi: g.Key, Bs: g.ToList(), Dot: dots.GetValueOrDefault(st.V[g.Key].Id)))
            .OrderByDescending(g => g.Dot).ThenBy(g => st.V[g.Vi].Name, StringComparer.CurrentCulture).ToList();
        var paid = rows.Count(r => r.B.Paid);
        var kpis = new List<Kpi>
        {
            K("Vendors", gs.Count, Strong), K("In Basket", rows.Count, Strong), K("Paid", paid, Green), K("Pending", rows.Count - paid, Warn),
            K("Basket Total", Fmt.Inr(rows.Sum(r => r.S.Bill?.Rec ?? 0)), Blue),
        };
        var nh = Cols("Ticket Info", "Vendor", "Service Type / City", "Service / Conf. No.", "Service Dates", ("Vendor Payable", "right"), "Payment Mode", "Actual Payment Action", "Mgmt. Approval", "Actions");
        var gridRows = gs.Select(g =>
        {
            var v = st.V[g.Vi]; var tot = g.Bs.Sum(r => r.S.Bill?.Rec ?? 0);
            var dot = g.Dot ? Ca("center", Dot([new TipLine("New ticket(s) added")])) : Empty;
            return new Row
            {
                Key = "v" + g.Vi, SeenVendor = g.Dot ? v.Id : null, SeenArea = "pr", CardHead = 4,
                Cells =
                [
                    dot, Txt("left", v.Id, Link, 700), Txt("left", v.Name, Strong, 600), Txt("center", g.Bs.Select(r => r.S.Ti).Distinct().Count().ToString(), w: 600, num: true), Txt("right", Fmt.Inr(tot), Link, 800, num: true),
                    // A vendor is Approved / Paid only once every ticket in its drill-down is; otherwise Pending.
                    C(AllOrPending(g.Bs.Count(r => r.B.Mgmt == "Approved"), g.Bs.Count, "Approved", "approved")),
                    C(AllOrPending(g.Bs.Count(r => r.B.Paid), g.Bs.Count, "Paid", "paid")),
                ],
                Detail = new Detail
                {
                    Cols = nh, CardHead = 2,
                    Rows = g.Bs.Select(List<Cell> (r) =>
                    {
                        var (b, s) = (r.B, r.S);
                        return
                        [
                            C(T(s.T.Id, Link, 700), Sub(s.T.Mbk)), C(T(v.Name, w: 600), T(v.Id, Link, 700, 11)), C(Pill(v.Type), Sub(v.City)),
                            C(T(s.Name), Sub(s.Conf != "" ? s.Conf : "Pending")), Dates(s), Txt("right", Fmt.Inr(s.Bill?.Rec ?? 0), w: 700, num: true),
                            // The mode chosen at submission, plus the RTGS advice number once one covers this entry.
                            C(Chip(b.Pm ?? "RTGS", "info"), b.RtgsNo != null ? Sub(b.RtgsNo) : null),
                            C(Chip(b.Paid ? "Paid" : "Pending", b.Paid ? "ok" : "warn")),
                            C(Lv("Mgmt", b.Paid ? "A" : "Q", b.Paid ? [new TipLine(b.By ?? "Mounika", "", b: true), new TipLine(Fmt.Fdt(b.At))] : null)),
                            !perm ? Txt("left", "-", Mut)
                                  : C(Btn("Print", Pop("prAdvice", ("sid", s.Id))), b.Paid ? null : Btn("Update", Pop("prpay", ("sid", s.Id)), "primary")),
                        ];
                    }).ToList(),
                },
            };
        }).ToList();

        List<ToolBtn> tools = [new("RTGS Advice History (" + rtgsCount + ")", Pop("rtgsHist"))];
        if (perm) tools.Add(new("Create RTGS Advice", Pop("rtgsSel"), Primary: true));
        return new ReportVm
        {
            Title = "Payment Request Report", Pill = true, Perm = perm, Kpis = kpis, Query = q,
            Desc = "The payment basket, grouped by vendor and ticket — generated once Finance confirms submission from Approved Vendor Payable, for actual payment execution. A vendor with newly added tickets is marked with a red dot and moved to the top until viewed.",
            Qph = "Search Ticket No., Vendor, Service...", Filters = MkFilters(baseRows, defs), FCount = FCount(defs, false), Count = gs.Count + " vendors", Tools = tools,
            Cols = Cols("", "", "Vendor ID", "Vendor Name", ("Tickets", "center"), ("Total Value", "right"), "Approval Status", "Payment Status"), Rows = gridRows, EmptyText = "No payment requests found.", ClearAllUrl = ClearAll,
        };
    }

}
