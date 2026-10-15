using Microsoft.AspNetCore.Mvc;
using SouthernTravels.Areas.Finance.Data;
using SouthernTravels.Areas.Finance.Models;

namespace SouthernTravels.Areas.Finance.Controllers;

/// <summary>Vendor KYC Report: vendor records and their KYC documents.</summary>
[Route("Finance/VendorKyc")]
public class VendorKycController(FinanceStore store) : FinanceControllerBase(store)
{
    [HttpGet("")]
    public IActionResult Index() => Report("VendorKyc", "Date of Creation", r => r.VendorKyc());

    [HttpGet("Popup")]
    public IActionResult Popup(string t, int? k, string? n)
    {
        if (t != "doc" || k is not { } ki || ki < 0 || ki >= St.V.Count || string.IsNullOrEmpty(n)) return NotFound();
        var v = St.V[ki];
        var file = v.Id + "_" + System.Text.RegularExpressions.Regex.Replace(n, "[^A-Za-z]+", "_") + ".pdf";
        return View("Popups/Document", new DocVm(v, n, file));
    }
}
