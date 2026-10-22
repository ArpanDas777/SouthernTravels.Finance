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
    public IActionResult Index() => Report("VendorPayable", "Bill Date", r => r.VendorPayable());

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
                var vendors = ss.Select(s => s.Vi).ToHashSet();
                var billHist = all.Where(s => s.Bill != null && vendors.Contains(s.Vi)).ToList();
                return ss.Count == 0 ? NotFound() : View("Popups/Raise", new RaiseVm(ss, re == 1, billHist));
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
        var items = form.Items.Where(i => !string.IsNullOrEmpty(i.Sid)).ToList();
        // Kind: "R" raises every service, "D" disputes every service, "D:<sid>" disputes just that one.
        if (form.Kind.StartsWith("D:")) items = items.Where(i => i.Sid == form.Kind[2..]).ToList();
        var valid = items.Count > 0 && items.All(i => !string.IsNullOrWhiteSpace(i.Date) && !string.IsNullOrWhiteSpace(i.No) && i.File != null && i.Voucher != null && i.Inv > 0 && i.Rec > 0);
        if (!valid) return Back(form.ReturnUrl, "Bill not saved — every field is required.");
        var kind = form.Kind.StartsWith("D") ? "D" : "R";
        var stamp = Clock.NowStamp();
        // The uploaded file is not stored yet; only its name is kept until a document store is connected.
        var res = await Finance.RaiseBillsAsync(items.Select(i => (i.Sid, new Bill
        {
            St = kind, Date = i.Date!, No = i.No!.Trim(), Doc = Path.GetFileName(i.File!.FileName), Voucher = Path.GetFileName(i.Voucher!.FileName), Inv = (int)i.Inv!.Value, Rec = (int)i.Rec!.Value,
            Upl = stamp, L1 = new Approval { St = "Q" }, L2 = new Approval { St = "-" },
        })).ToList());
        if (!res.Success) return Back(form.ReturnUrl, SaveFailed(res.Message, "Bill not saved."));
        return Back(form.ReturnUrl, kind == "R" ? $"Bill submitted for {items.Count} service(s)." : $"Dispute raised for {items.Count} service(s).");
    }
}
