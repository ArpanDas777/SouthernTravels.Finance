using Microsoft.AspNetCore.Mvc;
using SouthernTravels.Areas.Finance.Interfaces;
using SouthernTravels.Areas.Finance.Models;
using SouthernTravels.Areas.Finance.Models.Api;
using SouthernTravels.Core.Infrastructure;

namespace SouthernTravels.Areas.Finance.Controllers;

/// <summary>Payment Request Report: payments per ticket line, Payment Advice, Update payment and RTGS advice (create, history, letters, payment status).</summary>
[Route("Finance/PaymentRequest")]
public class PaymentRequestController(IFinanceService finance) : FinanceControllerBase(finance)
{
    [HttpGet("")]
    public IActionResult Index() => Report("PaymentRequest", "Bill Date", r => r.PaymentRequest());

    /* ---------- popups ---------- */

    [HttpGet("Popup")]
    public IActionResult Popup(string t, string? sid, string? no, string? kind, int hist)
    {
        var all = St.Svcs();
        switch (t)
        {
            case "rtgsSel":
            {
                var pool = St.RtgsPool().Select(b => all.First(s => s.Id == b.Sid)).OrderBy(s => s.Vi).ThenBy(s => s.Ti).ToList();
                return View("Popups/RtgsSelect", new RtgsSelVm(pool, FinanceOptions.SenderBanks));
            }
            case "rtgsOut":
            {
                var rec = FindRtgs(no);
                if (rec == null) return NotFound();
                var rows = LetterRows(rec, all);
                return View("Popups/RtgsOut", new RtgsOutVm(rec, rows.Count, rows.Select(r => r.BenName).Distinct().Count(), rows.Sum(r => r.Amount)));
            }
            case "rtgsHist":
            {
                List<RtgsRecord> log;
                lock (St.Gate) log = St.RtgsLog.AsEnumerable().Reverse().ToList();
                return View("Popups/RtgsHistory", new RtgsHistVm(log.Select(r =>
                {
                    var rows = LetterRows(r, all);
                    return new RtgsHistRow(r, rows.Count, rows.Select(x => x.BenName).Distinct().Count(), rows.Sum(x => x.Amount), PendingRtgs(r, all).Count);
                }).ToList()));
            }
            case "rtgsPay":
            {
                var rec = FindRtgs(no);
                var open = rec == null ? [] : PendingRtgs(rec, all);
                return rec == null || open.Count == 0 ? NotFound() : View("Popups/RtgsPayment", new RtgsPayVm(rec, open.Count, open.Sum(s => s.Bill?.Rec ?? 0), Ctx.UserName));
            }
            case "rtgsView":
            {
                var rec = FindRtgs(no);
                return rec == null ? NotFound()
                    : View("Popups/RtgsLetter", new RtgsLetterVm(rec, kind == "bank", hist == 1, Fmt.Dmy(rec.At), LetterRows(rec, all)));
            }
            case "prAdvice":
            {
                var vm = BuildPrAdvice(all, sid);
                return vm == null ? NotFound() : View("Popups/PaymentAdvice", vm);
            }
            case "prpay":
            {
                var s = all.FirstOrDefault(x => x.Id == sid);
                return s?.Bk == null || s.Bk.Paid ? NotFound() : View("Popups/PaymentRequestPay", new PrPayVm(s, Ctx.UserName));
            }
            default: return NotFound();
        }
    }

    /* ---------- printable documents ---------- */

    /// <summary>Payment Advice for one ticket line in the payment basket; null when the service isn't there.</summary>
    PrAdviceVm? BuildPrAdvice(List<Svc> all, string? sid)
    {
        var s = all.FirstOrDefault(x => x.Id == sid);
        if (s?.Bk == null) return null;
        // "service no." = the service's position within its ticket
        var serviceNo = all.Where(x => x.Ti == s.Ti).ToList().FindIndex(x => x.Id == s.Id) + 1;
        return new PrAdviceVm(s, s.Bk, "PA-" + s.T.Id + "-" + serviceNo, Fmt.Dmy(Clock.Today));
    }

    /// <summary>Printable Payment Advice (portrait A4) for one ticket line. Opens in a new tab from the pop-up's Print button.</summary>
    [HttpGet("PaymentAdvicePdf")]
    public IActionResult PaymentAdvicePdf(string sid)
    {
        var vm = BuildPrAdvice(St.Svcs(), sid);
        return vm == null ? NotFound() : View("PaymentAdvicePdf", vm);
    }

    /// <summary>Printable RTGS letter (A4) for one advice — Internal, Bank, or both together. Opens in a new tab; "Download PDF" is the print dialog's Save as PDF.</summary>
    [HttpGet("RtgsPdf")]
    public IActionResult RtgsPdf(string no, string kind)
    {
        var rec = FindRtgs(no);
        if (rec == null) return NotFound();
        var rows = LetterRows(rec, St.Svcs());
        RtgsLetterVm Letter(bool bank) => new(rec, bank, false, Fmt.Dmy(rec.At), rows);
        // kind=both puts the Internal and Bank letters in one printout (one PDF).
        return kind == "both" ? View("RtgsBothPdf", new RtgsBothVm(Letter(false), Letter(true))) : View("RtgsPdf", Letter(kind == "bank"));
    }

    RtgsRecord? FindRtgs(string? no)
    {
        lock (St.Gate) return St.RtgsLog.FirstOrDefault(r => r.No == no);
    }

    /// <summary>Services of an RTGS advice whose payment has not been recorded yet.</summary>
    static List<Svc> PendingRtgs(RtgsRecord rec, List<Svc> all) =>
        rec.Sids.Select(sid => all.First(s => s.Id == sid)).Where(s => s.Bk is { Paid: false }).ToList();

    /// <summary>One letter line per service in the advice, in vendor order. Sender account numbers are placeholders until real ones are supplied.</summary>
    List<RtgsLetterRow> LetterRows(RtgsRecord rec, List<Svc> all) =>
        rec.Sids.Select(sid => all.First(s => s.Id == sid)).OrderBy(s => s.Vi).ThenBy(s => s.Ti)
            .Select((s, i) => new RtgsLetterRow(i + 1, rec.Common.SenderBank + " [A/c No.]", rec.Common.ChequeNo, rec.Common.RandomNo,
                Fmt.Fd(rec.Common.ChequeDate), St.K[s.Vi].Acc, St.K[s.Vi].Ifsc, s.Bill?.Rec ?? 0, s.V.Name, rec.Common.Remarks ?? "")).ToList();

    /* ---------- actions ---------- */
    /// <summary>Unpaid ticket lines of a vendor that are still free to take a different planned payment mode.</summary>
    static List<Svc> OpenOnVendor(List<Svc> all, int vi) => all.Where(s => s.Vi == vi && s.Bk is { Paid: false, RtgsNo: null }).ToList();

    /// <summary>Changes the planned payment mode of one ticket line, or of every open line of its vendor.</summary>
    [HttpPost("SetMode")]
    public IActionResult SetMode(string sid, string? mode, string? all, string? returnUrl)
    {
        var svcs = St.Svcs();
        var s = svcs.FirstOrDefault(x => x.Id == sid);
        if (s?.Bk == null || mode == null || !FinanceOptions.PlanModes.Contains(mode)) return Back(returnUrl, "Mode not changed.");
        var sids = all == "1" ? OpenOnVendor(svcs, s.Vi).Select(x => x.Id) : [sid];
        var n = St.SetPlanMode(sids, mode);
        return Back(returnUrl, n == 0 ? "Mode not changed — the line is already paid or has an RTGS advice." : $"Payment mode set to {mode} for {n} line(s).");
    }


    /// <summary>Creates an RTGS advice from the tickets ticked in "Create RTGS Advice", then opens "RTGS Advice generated".</summary>
    [HttpPost("CreateRtgs")]
    public async Task<IActionResult> CreateRtgs(string[]? sid, string? senderBank, string? chequeNo, string? chequeDate, string? randomNo, string? remarks, string? returnUrl)
    {
        var idSet = (sid ?? []).ToHashSet();
        var ok = FinanceOptions.SenderBanks.Contains(senderBank ?? "") && !string.IsNullOrWhiteSpace(chequeNo) && !string.IsNullOrWhiteSpace(randomNo)
            && DateTime.TryParseExact(chequeDate, "yyyy-MM-dd", Fmt.Inv, System.Globalization.DateTimeStyles.None, out _);
        if (!ok || idSet.Count == 0) return Back(returnUrl, "RTGS advice not created — fill every required field and choose at least one ticket.");
        // Only unpaid RTGS tickets not yet covered by an advice can go into a new one.
        var pool = St.RtgsPool().Select(b => b.Sid).Where(idSet.Contains).ToList();
        if (pool.Count == 0) return Back(returnUrl, "Those tickets are already covered by an RTGS advice.");
        var res = await Finance.CreateRtgsAsync(pool, new RtgsCommon
        {
            SenderBank = senderBank!, ChequeNo = chequeNo!.Trim(), ChequeDate = chequeDate!, RandomNo = randomNo!.Trim(),
            Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim(),
        }, Ctx.UserName);
        if (!res.Success || string.IsNullOrEmpty(res.RtgsNo)) return Back(returnUrl, SaveFailed(res.Message, "RTGS advice not created."));
        TempData["popup"] = "/Finance/PaymentRequest/Popup?t=rtgsOut&no=" + Uri.EscapeDataString(res.RtgsNo);
        return Back(returnUrl, "RTGS advice " + res.RtgsNo + " created.");
    }

    [HttpPost("PaymentPay")]
    public async Task<IActionResult> PaymentPay(string sid, string? bank, string? mode, string? utr, string? chequeNo,
        string? paymentDate, decimal? amountPaid, IFormFile? slip, string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(bank) || string.IsNullOrWhiteSpace(mode) || !PaymentRefOk(mode, utr, chequeNo) || !ValidPaymentDate(paymentDate) || amountPaid is not > 0 || slip is null)
            return Back(returnUrl, "Payment not saved — every field is required.");
        if (St.Svcs().FirstOrDefault(x => x.Id == sid)?.Bk is not { Paid: false }) return Back(returnUrl, "Payment not saved — that ticket line is not awaiting payment.");
        var res = await Finance.PayRequestAsync(sid, Ctx.UserName,
            new PaymentEntry(bank.Trim(), mode, utr?.Trim(), chequeNo?.Trim(), paymentDate!, Path.GetFileName(slip.FileName)), (int)amountPaid.Value);
        return Back(returnUrl, res.Success ? "Payment recorded." : SaveFailed(res.Message, "Payment not saved."));
    }

    /// <summary>Update Payment Status from RTGS Advice History: one set of payment details for every unpaid ticket in the advice.</summary>
    [HttpPost("RtgsPay")]
    public async Task<IActionResult> RtgsPay(string no, string? bank, string? mode, string? utr, string? chequeNo,
        string? paymentDate, IFormFile? slip, string? returnUrl)
    {
        var rec = FindRtgs(no);
        if (rec == null || string.IsNullOrWhiteSpace(bank) || string.IsNullOrWhiteSpace(mode) || !PaymentRefOk(mode, utr, chequeNo) || !ValidPaymentDate(paymentDate) || slip is null)
            return Back(returnUrl, "Payment not saved — every field is required.");
        // Every unpaid ticket of the advice is paid its own approved payable.
        var items = PendingRtgs(rec, St.Svcs()).Select(s => new RtgsPaymentItem { ServiceID = s.Id, AmountPaid = s.Bill?.Rec ?? 0 }).ToList();
        if (items.Count == 0) return Back(returnUrl, "Nothing to pay — every ticket in " + no + " is already paid.");
        var res = await Finance.PayRtgsAsync(no, Ctx.UserName,
            new PaymentEntry(bank.Trim(), mode, utr?.Trim(), chequeNo?.Trim(), paymentDate!, Path.GetFileName(slip.FileName)), items);
        return Back(returnUrl, res.Success ? res.AffectedCount + " payment(s) recorded for " + no + "." : SaveFailed(res.Message, "Payment not saved."));
    }
}
