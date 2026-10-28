using Microsoft.AspNetCore.Mvc;
using SouthernTravels.Areas.Finance.Interfaces;
using SouthernTravels.Areas.Finance.Models;
using SouthernTravels.Core.Infrastructure;

namespace SouthernTravels.Areas.Finance.Controllers;

/// <summary>Vendor Payable Report: bills per vendor service, plus Raise Bill / Raise Dispute / Reupload and Bill Details.</summary>
[Route("Finance/VendorPayable")]
public class VendorPayableController(IFinanceService finance) : FinanceControllerBase(finance)
{
    [HttpGet("")]
    public IActionResult Index() => Report("VendorPayable", "Bill Date", r => Ctx.Approver ? r.VendorPayableApproval() : r.VendorPayable());

    /* ---------- popups (loaded into the modal by site.js) ---------- */

    [HttpGet("Popup")]
    public IActionResult Popup(string t, string? ids, int re, string? id)
    {
        var all = St.Svcs();
        switch (t)
        {
            case "raise":
            {
                var set = (ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
                var ss = set.Select(x => all.FirstOrDefault(s => s.Id == x)).OfType<Svc>()
                    .Where(s => re == 1 ? s.Bill != null : s.Eligible).ToList();
                return ss.Count == 0 ? NotFound() : View("Popups/Raise", new RaiseVm(ss, re == 1));
            }
            case "rejapv" or "aprhist":
            {
                var s = all.FirstOrDefault(x => x.Id == id);
                return s?.Bill == null ? NotFound() : View(t == "rejapv" ? "Popups/RejectApproval" : "Popups/ApprovalHistory", s);
            }
            case "viewbill":
            {
                var s = all.FirstOrDefault(x => x.Id == id);
                return s?.Bill == null ? NotFound() : View("Popups/ViewBill", s);
            }
            default: return NotFound();
        }
    }

    /* ---------- actions ---------- */

    [HttpPost("RaiseBill")]
    public async Task<IActionResult> RaiseBill(RaiseForm form)
    {
        var all = St.Svcs();
        var items = form.Items.Where(i => !string.IsNullOrEmpty(i.Sid)).ToList();
        // Kind: "R" submits every service, "D" disputes every service, "D:<sid>" disputes just that one.
        if (form.Kind.StartsWith("D:")) items = items.Where(i => i.Sid == form.Kind[2..]).ToList();
        var kind = form.Kind.StartsWith("D") ? "D" : "R";
        int Payable(string sid) => all.FirstOrDefault(s => s.Id == sid)?.Neg ?? 0;
        // A dispute needs the file and an invoice above the vendor payable; a bill also needs its date and number, and (for a first upload) an invoice not above the payable.
        bool Ok(RaiseItem i) => i.File != null && i.Inv > 0 && (kind == "D"
            ? i.Inv > Payable(i.Sid)
            : !string.IsNullOrWhiteSpace(i.Date) && !string.IsNullOrWhiteSpace(i.No) && (form.Reupload == 1 || i.Inv <= Payable(i.Sid)));
        if (items.Count == 0 || !items.All(Ok)) return Back(form.ReturnUrl, kind == "D" ? "Dispute not raised — choose the bill and enter an amount above the vendor payable." : "Bill not saved — fill every required field.");
        var stamp = Clock.NowStamp();
        // The uploaded files are not stored yet; only their names are kept until a document store is connected.
        var res = await Finance.RaiseBillsAsync(items.Select(i =>
        {
            int inv = (int)i.Inv!.Value, pay = Payable(i.Sid);
            // A reupload that still exceeds the payable stays a dispute.
            var st = kind == "D" || inv > pay ? "D" : "R";
            return (i.Sid, new Bill
            {
                St = st, Date = string.IsNullOrWhiteSpace(i.Date) ? Clock.Today : i.Date, No = i.No?.Trim() ?? "", Doc = Path.GetFileName(i.File!.FileName),
                Extra = i.Extra != null ? Path.GetFileName(i.Extra.FileName) : "", Inv = inv, Rec = Math.Min(inv, pay), Mr = inv > pay ? i.Mr?.Trim() ?? "" : "",
                Upl = stamp, L1 = new Approval { St = "Q" }, L2 = new Approval { St = "-" },
            });
        }).ToList());
        if (!res.Success) return Back(form.ReturnUrl, SaveFailed(res.Message, "Bill not saved."));
        return Back(form.ReturnUrl, kind == "R" ? $"Bill submitted for {items.Count} service(s)." : $"Dispute raised for {items.Count} service(s).");
    }

    /* ---------- approver (L2) actions ---------- */

    /// <summary>Approves a bill that L1 has approved.</summary>
    [HttpPost("Approve")]
    public async Task<IActionResult> Approve(string sid, string? returnUrl)
    {
        if (ApproverOnly(returnUrl) is { } denied) return denied;
        var res = await Finance.DecideBillAsync(sid, true, Ctx.UserName);
        var s = St.Svcs().FirstOrDefault(x => x.Id == sid);
        return Back(returnUrl, res.Success ? $"L2 approved — {s?.T.Id} · {s?.Bill?.No}." : SaveFailed(res.Message, "Bill not approved."));
    }

    /// <summary>Rejects a bill, or marks it as disputed; the reason is required.</summary>
    [HttpPost("RejectBill")]
    public async Task<IActionResult> RejectBill(string sid, string? kind, string? reason, string? returnUrl)
    {
        if (ApproverOnly(returnUrl) is { } denied) return denied;
        if (kind is not ("Rejected" or "Disputed") || string.IsNullOrWhiteSpace(reason)) return Back(returnUrl, "Not saved — choose Rejected or Disputed and give a reason.");
        var res = await Finance.DecideBillAsync(sid, false, Ctx.UserName, kind, reason.Trim());
        var s = St.Svcs().FirstOrDefault(x => x.Id == sid);
        return Back(returnUrl, res.Success ? $"L2 rejected / disputed — {s?.T.Id} · {s?.Bill?.No}." : SaveFailed(res.Message, "Bill not saved."));
    }
}
