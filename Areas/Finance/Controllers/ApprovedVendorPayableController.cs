using Microsoft.AspNetCore.Mvc;
using SouthernTravels.Areas.Finance.Interfaces;
using SouthernTravels.Areas.Finance.Models;
using SouthernTravels.Areas.Finance.Models.Api;
using SouthernTravels.Core.Infrastructure;

namespace SouthernTravels.Areas.Finance.Controllers;

/// <summary>Approved Vendor Payable: billed services awaiting submission, the cart and the Payment Submission Advice.</summary>
[Route("Finance/ApprovedVendorPayable")]
public class ApprovedVendorPayableController(IFinanceService finance) : FinanceControllerBase(finance)
{
    [HttpGet("")]
    public IActionResult Index() => Report("ApprovedVendorPayable", "Bill Date", r => r.ApprovedVendorPayable());

    /* ---------- popups ---------- */

    [HttpGet("Popup")]
    public IActionResult Popup(string t, string? ids, int? vi)
    {
        var all = St.Svcs();
        switch (t)
        {
            case "vbills":
            {
                if (vi is not { } v || v < 0 || v >= St.V.Count) return NotFound();
                List<Svc> ss;
                lock (St.Gate) ss = St.AwaitingSubmission(all).Where(s => s.Vi == v).ToList();
                return View("Popups/VendorBills", new VendorBillsVm(St.V[v], ss));
            }
            case "advice": return View("Popups/Advice", BuildAdvice(all, (ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet()));
            default: return NotFound();
        }
    }

    AdviceVm BuildAdvice(List<Svc> all, HashSet<string> ids)
    {
        List<Svc> pool; int basketCount;
        lock (St.Gate) { pool = St.AwaitingSubmission(all); basketCount = St.Basket.Count; }
        var sel = pool.Where(s => ids.Contains(s.Id)).ToList();
        var vs = sel.GroupBy(s => s.Vi).ToList();
        var rows = vs.Select((g, i) => new AdviceRow(i + 1, St.V[g.Key].Name, St.V[g.Key].Id, g.Key, g.Select(s => s.Ti).Distinct().Count(), Fmt.Inr(g.Sum(s => s.Bill!.Rec)))).ToList();
        // Other billed services of the same vendors that are not in this advice.
        var pending = vs.Select(g =>
        {
            var rest = pool.Where(s => s.Vi == g.Key && !ids.Contains(s.Id)).ToList();
            return rest.Count > 0 ? $"{St.V[g.Key].Name}: {rest.Select(s => s.Ti).Distinct().Count()} ticket(s) pending ({Fmt.Inr(rest.Sum(s => s.Bill!.Rec))})" : null;
        }).OfType<string>().ToList();
        return new AdviceVm(sel.Select(s => s.Id).ToList(), "PSA-2610-" + (101 + basketCount), Fmt.Fdt(Clock.NowStamp()), rows,
            sel.Select(s => s.Ti).Distinct().Count() + " tickets · " + vs.Count + " vendors", Fmt.Inr(sel.Sum(s => s.Bill!.Rec)), pending, sel.Count, vs.Count);
    }

    /* ---------- actions ---------- */

    [HttpPost("Submit")]
    public async Task<IActionResult> Submit(string ids, string? returnUrl)
    {
        var idSet = (ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        // Planned payment mode per vendor, posted as mode_<vendor index> from the advice; RTGS when missing.
        var modes = new Dictionary<int, string>();
        foreach (var kv in Request.Form.Where(f => f.Key.StartsWith("mode_")))
            if (int.TryParse(kv.Key[5..], out var vi) && vi >= 0 && vi < St.V.Count && FinanceOptions.PlanModes.Contains(kv.Value.ToString())) modes[vi] = kv.Value.ToString();
        // Only services still awaiting submission are sent, each with its vendor's planned mode.
        List<Svc> sel;
        lock (St.Gate) sel = St.AwaitingSubmission(St.Svcs()).Where(s => idSet.Contains(s.Id)).ToList();
        if (sel.Count == 0) return Back(returnUrl, "Nothing sent — those services are no longer awaiting submission.");
        var res = await Finance.SubmitToPaymentRequestAsync(sel.Select(s => new PaymentSubmitItem
        {
            ServiceID = s.Id, VendorId = s.V.Id, PlannedPaymentMode = modes.GetValueOrDefault(s.Vi, "RTGS"),
        }).ToList());
        if (!res.Success) return Back(returnUrl, SaveFailed(res.Message, "Nothing sent."));
        var n = res.AffectedCount;
        return n == 0 ? Back(returnUrl, "Nothing sent — those services are no longer awaiting submission.")
                      : Back(returnUrl, n + " service(s) sent to Payment Request Report.");
    }
}
