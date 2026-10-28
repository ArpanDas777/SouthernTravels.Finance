using Microsoft.AspNetCore.Mvc;
using SouthernTravels.Areas.Finance.Interfaces;
using SouthernTravels.Areas.Finance.Models;

namespace SouthernTravels.Areas.Finance.Controllers;

/// <summary>Vendor KYC Report: vendor records and their KYC documents; for the approver, approving or rejecting each document.</summary>
[Route("Finance/VendorKyc")]
public class VendorKycController(IFinanceService finance) : FinanceControllerBase(finance)
{
    [HttpGet("")]
    public IActionResult Index() => Report("VendorKyc", "Date of Creation", r => Ctx.Approver ? r.VendorKycApproval() : r.VendorKyc());

    [HttpGet("Popup")]
    public IActionResult Popup(string t, int? k, string? n, string? key)
    {
        if (k is not { } ki || ki < 0 || ki >= St.V.Count) return NotFound();
        var v = St.V[ki];
        switch (t)
        {
            case "doc" when !string.IsNullOrEmpty(n):
            {
                var file = v.Id + "_" + System.Text.RegularExpressions.Regex.Replace(n, "[^A-Za-z]+", "_") + ".pdf";
                return View("Popups/Document", new DocVm(v, n, file));
            }
            // Reject a document: the reason is required.
            case "kycrej" when KycDocs.All.Any(d => d.Key == key):
                return View("Popups/RejectDoc", new KycRejectVm(ki, v, key!, KycDocs.All.First(d => d.Key == key).Name));
            default: return NotFound();
        }
    }

    /// <summary>The approver approves one document.</summary>
    [HttpPost("Approve")]
    public async Task<IActionResult> Approve(int vi, string key, string? returnUrl)
    {
        if (ApproverOnly(returnUrl) is { } denied) return denied;
        var res = await Finance.SetKycDocAsync(vi, key, "A", null, Ctx.UserName);
        return Back(returnUrl, res.Success ? "Document approved." : SaveFailed(res.Message, "Document not saved."));
    }

    /// <summary>The approver rejects one document; the reason is required.</summary>
    [HttpPost("RejectDoc")]
    public async Task<IActionResult> RejectDoc(int vi, string key, string? reason, string? returnUrl)
    {
        if (ApproverOnly(returnUrl) is { } denied) return denied;
        if (string.IsNullOrWhiteSpace(reason)) return Back(returnUrl, "Not saved — a reason is required.");
        var res = await Finance.SetKycDocAsync(vi, key, "X", reason.Trim(), Ctx.UserName);
        var v = St.V.ElementAtOrDefault(vi); var doc = KycDocs.All.FirstOrDefault(d => d.Key == key).Name;
        return Back(returnUrl, res.Success ? $"{doc} rejected — {v?.Id} · {v?.Name}." : SaveFailed(res.Message, "Document not saved."));
    }
}
