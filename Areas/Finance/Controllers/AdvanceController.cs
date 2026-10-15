using Microsoft.AspNetCore.Mvc;
using SouthernTravels.Areas.Finance.Data;
using SouthernTravels.Areas.Finance.Models;
using SouthernTravels.Core.Infrastructure;

namespace SouthernTravels.Areas.Finance.Controllers;

/// <summary>Advance Report: fully approved advance requests, Update Payment and the printable Advance Advice.</summary>
[Route("Finance/Advance")]
public class AdvanceController(FinanceStore store) : FinanceControllerBase(store)
{
    [HttpGet("")]
    public IActionResult Index() => Report("Advance", "Advance Requested Date", r => r.Advance());

    /* ---------- popups ---------- */

    [HttpGet("Popup")]
    public IActionResult Popup(string t, string? sid)
    {
        if (t == "advpay" && !Ctx.Perm) return Forbidden();
        if (t is not ("advpay" or "advview")) return NotFound();
        AdvanceRequest? a;
        lock (St.Gate) a = St.Adv.FirstOrDefault(x => x.Sid == sid);
        var s = St.Svcs().FirstOrDefault(x => x.Id == sid);
        if (a == null || s == null) return NotFound();
        var view = t == "advview";
        if (view ? a.Pay == null : a.Pay != null) return NotFound();
        return View("Popups/AdvancePayment", new AdvPayVm(a, s, view));
    }

    /// <summary>Printable Advance Advice (A4). Opened in a new tab from the Advance Report; payment details fill in once paid.</summary>
    [HttpGet("AdvanceAdvice")]
    public IActionResult AdvanceAdvice(string sid)
    {
        int index; AdvanceRequest? a;
        lock (St.Gate)
        {
            index = St.Adv.FindIndex(x => x.Sid == sid);
            a = index >= 0 ? St.Adv[index] : null;
        }
        // Only approved requests (L1 and L2) get an advice.
        if (a is not { L1: "A", L2: "A" }) return NotFound();
        var s = St.Svcs().First(x => x.Id == sid);
        var seq = (index + 1).ToString("0000", Fmt.Inv);
        var vm = new AdvanceAdviceVm(
            AdviceNo: "ADV/2026-27/" + seq, RequestNo: "AR/2026-27/" + seq, FileNo: s.T.File,
            Date: Fmt.Dmy(a.Pay?.PaymentDate ?? Fmt.Add(a.At, 1)), Generated: Fmt.DmyTime(Clock.NowStamp()),
            Service: s, Kyc: St.K[s.Vi], Req: a);
        return View("AdvanceAdvice", vm);
    }

    /* ---------- actions ---------- */

    [HttpPost("AdvancePay")]
    public IActionResult AdvancePay(string sid, decimal? pct, string? ctype, string? no, string? bank, string? mode,
        string? paymentDate, decimal? amountPaid, IFormFile? slip, string? returnUrl)
    {
        if (!Ctx.Perm) return Forbidden();
        if (pct is not { } p || p < 0 || p > 100 || string.IsNullOrWhiteSpace(no) || string.IsNullOrWhiteSpace(bank) || string.IsNullOrWhiteSpace(mode)
            || !ValidPaymentDate(paymentDate) || amountPaid is not > 0 || slip is null)
            return Back(returnUrl, "Payment not saved — every field is required.");
        // The uploaded slip is not stored yet; only its name is kept until a document store is connected.
        St.PayAdvance(sid, new AdvancePayment
        {
            Pct = p, CType = ctype == "Online" ? "Online" : "Physical Cheque", No = no.Trim(), Bank = bank.Trim(), Mode = mode,
            PaymentDate = paymentDate!, AmountPaid = (int)amountPaid.Value, Slip = Path.GetFileName(slip.FileName),
        });
        return Back(returnUrl, "Payment updated.");
    }
}
