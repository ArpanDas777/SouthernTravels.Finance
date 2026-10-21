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

    /// <summary>Paid; Sent for payment (an RTGS advice has been created for it); otherwise Unpaid.</summary>
    static string PrStatus(BasketItem b) => b.Paid ? "Paid" : b.RtgsNo != null ? "Sent for payment" : "Unpaid";
    static string PrTone(string s) => s switch { "Paid" => "ok", "Sent for payment" => "info", _ => "warn" };

    /// <summary>Vendor-level payment status: Paid once every line is, Sent for payment when only RTGS-advised lines remain, otherwise Unpaid.</summary>
    static Line VendorPay(List<PrRow> bs)
    {
        int paid = bs.Count(r => r.B.Paid), sent = bs.Count(r => PrStatus(r.B) == "Sent for payment"), unpaid = bs.Count - paid - sent;
        var label = unpaid > 0 ? "Unpaid" : sent > 0 ? "Sent for payment" : "Paid";
        return Chip(label, PrTone(label), [new TipLine($"{paid} paid · {sent} sent for payment · {unpaid} unpaid")]);
    }

    /// <summary>
    /// The planned payment mode, chosen per vendor right on its line. Changing it re-plans every line that is still open
    /// (unpaid and not yet in an RTGS advice); with none open the mode is shown as a plain label.
    /// </summary>
    static Cell ModeCell(List<PrRow> bs)
    {
        var open = bs.Where(r => !r.B.Paid && r.B.RtgsNo == null).ToList();
        if (open.Count == 0) return C(Chip(bs.Select(r => r.B.Pm ?? "RTGS").Distinct().Count() == 1 ? bs[0].B.Pm ?? "RTGS" : "Mixed", "info"));
        var modes = open.Select(r => r.B.Pm ?? "RTGS").Distinct().ToList();
        return C(Select(modes.Count == 1 ? modes[0] : "Mixed", [.. (modes.Count == 1 ? [] : new[] { "Mixed" }), .. FinanceOptions.PlanModes],
            "/Finance/PaymentRequest/SetMode", open[0].S.Id, "Payment mode for " + open[0].S.V.Name));
    }

    public ReportVm PaymentRequest()
    {
        var all = st.Svcs();
        List<BasketItem> basket; Dictionary<string, bool> dots; int rtgsCount;
        lock (st.Gate) { basket = st.Basket.ToList(); dots = new(st.Dots); rtgsCount = st.RtgsLog.Count; }
        var rows = basket.Select(b => new PrRow(b, all.First(x => x.Id == b.Sid)))
            .Where(r => PerOk(r.S.Bill?.Date ?? r.S.End)).ToList();
        var defs = new List<FDef<PrRow>>
        {
            new("ps", "Payment Status", r => PrStatus(r.B), ["Unpaid", "Sent for payment", "Paid"]),
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
            K("Vendors", gs.Count, Strong), K("In Basket", rows.Count, Strong), K("Paid", paid, Green),
            K("Sent for Payment", rows.Count(r => PrStatus(r.B) == "Sent for payment"), Blue), K("Unpaid", rows.Count(r => PrStatus(r.B) == "Unpaid"), Warn),
            K("Basket Total", Fmt.Inr(rows.Sum(r => r.S.Bill?.Rec ?? 0)), Blue),
        };
        var nh = Cols("Ticket Info", "Vendor", "Service Type / City", "Service / Conf. No.", "Service Dates", ("Vendor Payable", "right"), "Payment Mode", "Actual Payment Action", "Mgmt. Approval", "Actions");
        var (pgs, pager) = Paged(gs);
        var gridRows = pgs.Select(g =>
        {
            var v = st.V[g.Vi]; var tot = g.Bs.Sum(r => r.S.Bill?.Rec ?? 0);
            var dot = g.Dot ? Ca("center", Dot([new TipLine("New ticket(s) added")])) : Empty;
            return new Row
            {
                Key = "v" + g.Vi, SeenVendor = g.Dot ? v.Id : null, SeenArea = "pr", CardHead = 4,
                Cells =
                [
                    dot, Txt("left", v.Id, Link, 700), Txt("left", v.Name, Strong, 600), Txt("center", g.Bs.Select(r => r.S.Ti).Distinct().Count().ToString(), w: 600, num: true), Txt("right", Fmt.Inr(tot), Link, 800, num: true), ModeCell(g.Bs),
                    // A vendor is Approved / Paid only once every ticket in its drill-down is; otherwise Pending.
                    C(AllOrPending(g.Bs.Count(r => r.B.Mgmt == "Approved"), g.Bs.Count, "Approved", "approved")),
                    C(VendorPay(g.Bs)),
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
                            C(Chip(PrStatus(b), PrTone(PrStatus(b)))),
                            C(Lv("Mgmt", b.Paid ? "A" : "Q", b.Paid ? [new TipLine(b.By ?? "Mounika", "", b: true), new TipLine(Fmt.Fdt(b.At))] : null)),
                            C(Btn("Print", Pop("prAdvice", ("sid", s.Id))), b.Paid ? null : Btn("Update", Pop("prpay", ("sid", s.Id)), "primary")),
                        ];
                    }).ToList(),
                },
            };
        }).ToList();

        List<ToolBtn> tools = [new("RTGS Advice History (" + rtgsCount + ")", Pop("rtgsHist"))];
        tools.Add(new("Create RTGS Advice", Pop("rtgsSel"), Primary: true));
        return new ReportVm
        {
            Title = "Payment Request Report", Pill = true, Kpis = kpis, Query = q,
            Desc = "The payment basket, grouped by vendor and ticket — generated once Finance confirms submission from Approved Vendor Payable, for actual payment execution. A vendor with newly added tickets is marked with a red dot and moved to the top until viewed.",
            Qph = "Search Ticket No., Vendor, Service...", Filters = MkFilters(baseRows, defs), FCount = FCount(defs, false), Count = gs.Count + " vendors", Tools = tools,
            Cols = Cols("", "", "Vendor ID", "Vendor Name", ("Tickets", "center"), ("Total Value", "right"), "Payment Mode", "Approval Status", "Payment Status"), Rows = gridRows, Pager = pager, EmptyText = "No payment requests found.", ClearAllUrl = ClearAll,
        };
    }

}
