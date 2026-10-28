using Microsoft.AspNetCore.Mvc;
using SouthernTravels.Areas.Finance.Interfaces;
using SouthernTravels.Areas.Finance.Models;
using SouthernTravels.Core.Infrastructure;

namespace SouthernTravels.Areas.Finance.Controllers;

/// <summary>
/// Advance Report: fully approved advance requests. Payment is two steps (Add Payment Details, then Update Payment Details with the UTR),
/// several advances can be paid together with Bulk Payment (advice letters as PDF, history, Update UTR), and the Advance Advice prints.
/// </summary>
[Route("Finance/Advance")]
public class AdvanceController(IFinanceService finance) : FinanceControllerBase(finance)
{
    [HttpGet("")]
    public IActionResult Index() => Report("Advance", "Advance Requested Date", r => r.Advance());

    /* ---------- popups ---------- */

    [HttpGet("Popup")]
    public IActionResult Popup(string t, string? sid, string? sids, string? no, string? kind)
    {
        var all = St.Svcs();
        switch (t)
        {
            case "advpay" or "advview":
            {
                AdvanceRequest? a;
                lock (St.Gate) a = St.Adv.FirstOrDefault(x => x.Sid == sid);
                var s = all.FirstOrDefault(x => x.Id == sid);
                if (a == null || s == null) return NotFound();
                var view = t == "advview";
                // Add / Update is open until the UTR is in; View shows a paid advance.
                if (view ? !a.Paid : a.Paid) return NotFound();
                List<AdvanceRequest> others; lock (St.Gate) others = St.Adv.Where(x => x.Sid == sid && x != a && x.Paid).ToList();
                return View("Popups/AdvancePayment", new AdvPayVm(a, s, view, (s.Adv0 > 0 ? 1 : 0) + others.Count, s.Adv0 + others.Sum(x => x.Amt)));
            }
            case "advrej":
            {
                var a = St.Adv.FirstOrDefault(x => x.Sid == sid);
                var s = all.FirstOrDefault(x => x.Id == sid);
                return a == null || s == null ? NotFound() : View("Popups/RejectAdvance", new AdvRowVm(a, s));
            }
            case "bulkPay":
            {
                var ids = (sids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
                var pool = St.BulkPool().Where(a => ids.Contains(a.Sid)).Select(a => new AdvRowVm(a, all.First(s => s.Id == a.Sid))).ToList();
                return pool.Count == 0 ? NotFound() : View("Popups/BulkPayment", new BulkPayVm(pool));
            }
            case "bulkHist":
            {
                List<RtgsRecord> log; lock (St.Gate) log = St.BulkLog.AsEnumerable().Reverse().ToList();
                return View("Popups/BulkHistory", new BulkHistVm(log.Select(r =>
                {
                    var items = BulkItems(r, all);
                    return new BulkHistRow(r, items, items.Sum(i => i.Req.Pay?.Net ?? i.Req.Amt), items.Count(i => !i.Req.Paid));
                }).ToList()));
            }
            case "bulkOut":
            {
                var rec = FindBulk(no);
                if (rec == null) return NotFound();
                var rows = BulkRows(rec, all);
                return View("~/Areas/Finance/Views/PaymentRequest/Popups/RtgsOut.cshtml", new RtgsOutVm(rec, rows.Count, rows.Select(r => r.BenName).Distinct().Count(), rows.Sum(r => r.Amount), true));
            }
            case "bulkView":
            {
                var rec = FindBulk(no);
                return rec == null ? NotFound()
                    : View("~/Areas/Finance/Views/PaymentRequest/Popups/RtgsLetter.cshtml", new RtgsLetterVm(rec, kind == "bank", true, Fmt.Dmy(rec.At), BulkRows(rec, all), true));
            }
            case "bulkUtr":
            {
                var rec = FindBulk(no);
                return rec == null ? NotFound() : View("Popups/BulkUtr", new BulkUtrVm(rec, BulkItems(rec, all)));
            }
            default: return NotFound();
        }
    }

    RtgsRecord? FindBulk(string? no)
    {
        lock (St.Gate) return St.BulkLog.FirstOrDefault(r => r.No == no);
    }

    /// <summary>The advances of a Bulk Payment advice, in ticket order.</summary>
    List<AdvRowVm> BulkItems(RtgsRecord rec, List<Svc> all)
    {
        lock (St.Gate)
            return rec.Sids.Select(sid => new AdvRowVm(St.Adv.First(a => a.Sid == sid), all.First(s => s.Id == sid))).OrderBy(x => x.Service.Vi).ThenBy(x => x.Service.Ti).ToList();
    }

    /// <summary>One letter line per advance: the net payable (advance less TDS).</summary>
    List<RtgsLetterRow> BulkRows(RtgsRecord rec, List<Svc> all) => LetterRowsOf(rec, BulkItems(rec, all).Select(x => (x.Service, x.Req.Pay?.Net ?? x.Req.Amt)), "Advance payment");

    /// <summary>Printable Bulk Payment letter (A4) — Internal, Bank, or both together. Opens in a new tab; Download PDF is the print dialog's Save as PDF.</summary>
    [HttpGet("BulkPdf")]
    public IActionResult BulkPdf(string no, string kind)
    {
        var rec = FindBulk(no);
        if (rec == null) return NotFound();
        var rows = BulkRows(rec, St.Svcs());
        RtgsLetterVm Letter(bool bank) => new(rec, bank, false, Fmt.Dmy(rec.At), rows, true);
        return kind == "both"
            ? View("~/Areas/Finance/Views/PaymentRequest/RtgsBothPdf.cshtml", new RtgsBothVm(Letter(false), Letter(true)))
            : View("~/Areas/Finance/Views/PaymentRequest/RtgsPdf.cshtml", Letter(kind == "bank"));
    }

    /// <summary>Printable Advance Advice (A4). Opened in a new tab from the Advance Report once payment details are added.</summary>
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
        if (a is not { Approved: true }) return NotFound();
        var s = St.Svcs().First(x => x.Id == sid);
        var seq = (index + 1).ToString("0000", Fmt.Inv);
        // Every payment to this vendor on this ticket: earlier advances, advances paid through the Advance Report, and payment-request payments.
        var all = St.Svcs();
        var mine = all.Where(x => x.Ti == s.Ti && x.Vi == s.Vi).ToList();
        var paid = new List<(string Date, string Type, string Service, string Ref, string Mode, int Amount, int Sell)>();
        lock (St.Gate)
            foreach (var x in mine)
            {
                if (x.Adv0 > 0) paid.Add(("", "Earlier advance", x.Name, "-", "-", x.Adv0, x.Sell));
                // An advance counts at its full requested amount (TDS is deducted from what the vendor receives, not from what is settled);
                // another service's advance counts once its UTR is in, this one as soon as its payment details are added.
                if (St.Adv.FirstOrDefault(r => r.Sid == x.Id) is { Pay: { } ap } req && (x.Id == sid || req.Paid))
                    paid.Add((ap.PaymentDate, x.Id == sid ? "This advance" : "Advance", x.Name, ap.Utr != "" ? ap.Utr : ap.No, ap.Mode, req.Amt, x.Sell));
                if (x.Bk is { Paid: true } bk)
                    paid.Add((bk.PaymentDate ?? "", "Payment", x.Name, bk.Utr ?? bk.ChequeNo ?? "-", bk.Mode ?? "-", bk.AmountPaid, x.Sell));
            }
        var payable = mine.Sum(x => x.Neg);
        var running = payable;
        var txns = paid.OrderBy(p => p.Date, StringComparer.Ordinal)
            .Select(p => new PayTxn(p.Date, p.Type, p.Service, p.Ref, p.Mode, p.Amount, Fmt.Pct(p.Amount, p.Sell).Replace(".0%", "%"), running -= p.Amount)).ToList();
        var vm = new AdvanceAdviceVm(
            AdviceNo: "ADV/2026-27/" + seq, RequestNo: "AR/2026-27/" + seq, FileNo: s.T.File,
            Date: Fmt.Dmy(a.Pay?.PaymentDate ?? Fmt.Add(a.At, 1)), Generated: Fmt.DmyTime(Clock.NowStamp()),
            Service: s, Kyc: St.K[s.Vi], Req: a, Txns: txns, Payable: payable, SellTotal: mine.Sum(x => x.Sell));
        return View("AdvanceAdvice", vm);
    }

    /* ---------- actions ---------- */

    /// <summary>
    /// Step 1 (no payment details yet): saves everything except the UTR. Step 2 (details already added): the same form with a mandatory UTR, which marks the advance Paid.
    /// </summary>
    [HttpPost("AdvancePay")]
    public async Task<IActionResult> AdvancePay(string sid, decimal? pct, string? ctype, string? mode, string? bank, string? no, string? paymentDate, string? utr, string? returnUrl)
    {
        AdvanceRequest? req;
        lock (St.Gate) req = St.Adv.FirstOrDefault(x => x.Sid == sid);
        if (req is not { Approved: true, Paid: false }) return Back(returnUrl, "Payment not saved — that advance is not open for payment.");
        var d = PaymentDetails(ctype, mode, bank, no, paymentDate);
        var step2 = req.Pay != null;
        if (pct is not { } p || p < 0 || p > 100 || d is null || (step2 && string.IsNullOrWhiteSpace(utr)))
            return Back(returnUrl, "Payment not saved — every field is required.");
        var res = await Finance.PayAdvanceAsync(sid, req.Amt, new AdvancePayment
        {
            Pct = p, CType = d.Value.CType, Mode = d.Value.Mode, Bank = d.Value.Bank, No = d.Value.No, PaymentDate = d.Value.Date, Utr = step2 ? utr!.Trim() : "",
        });
        if (!res.Success) return Back(returnUrl, SaveFailed(res.Message, "Payment not saved."));
        return Back(returnUrl, step2 ? "Advance marked as paid." : "Payment details added — the advice can now be printed.");
    }

    /// <summary>Bulk Payment: shared payment details and a TDS % per advance; creates the Bulk Payment advice and opens its documents.</summary>
    [HttpPost("BulkPay")]
    public async Task<IActionResult> BulkPay(Dictionary<string, string>? tds, string? ctype, string? mode, string? bank, string? no, string? paymentDate, string? rnd, string? returnUrl)
    {
        var d = PaymentDetails(ctype, mode, bank, no, paymentDate);
        var pcts = new Dictionary<string, decimal>();
        foreach (var (sid, v) in tds ?? [])
            if (decimal.TryParse(v, System.Globalization.NumberStyles.Number, Fmt.Inv, out var p) && p is >= 0 and <= 100) pcts[sid] = p;
        if (d is null || string.IsNullOrWhiteSpace(rnd) || pcts.Count == 0 || pcts.Count != (tds?.Count ?? 0))
            return Back(returnUrl, "Bulk payment not saved — fill every field and a TDS % for each advance.");
        var open = St.BulkPool().Select(a => a.Sid).ToHashSet();
        pcts = pcts.Where(x => open.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);
        if (pcts.Count == 0) return Back(returnUrl, "Those advances already have payment details.");
        var res = await Finance.CreateBulkPaymentAsync(pcts,
            new AdvancePayment { CType = d.Value.CType, Mode = d.Value.Mode, Bank = d.Value.Bank, No = d.Value.No, PaymentDate = d.Value.Date, Rnd = rnd.Trim() },
            new RtgsCommon { SenderBank = d.Value.Bank, ChequeNo = d.Value.No, ChequeDate = d.Value.Date, RandomNo = rnd.Trim(), Mode = d.Value.Mode }, Ctx.UserName);
        if (!res.Success || string.IsNullOrEmpty(res.BulkNo)) return Back(returnUrl, SaveFailed(res.Message, "Bulk payment not saved."));
        TempData["popup"] = "/Finance/Advance/Popup?t=bulkOut&no=" + Uri.EscapeDataString(res.BulkNo);
        return Back(returnUrl, "Bulk Payment advice " + res.BulkNo + " created.");
    }

    /// <summary>Update UTR / Transaction of a Bulk Payment advice: an advance with a UTR becomes Paid; the rest stay awaiting.</summary>
    [HttpPost("BulkUtr")]
    public async Task<IActionResult> BulkUtr(string no, Dictionary<string, string>? utr, string? returnUrl)
    {
        var rec = FindBulk(no);
        var given = (utr ?? []).Where(x => !string.IsNullOrWhiteSpace(x.Value)).ToDictionary(x => x.Key, x => x.Value.Trim());
        if (rec == null || given.Count == 0) return Back(returnUrl, "UTR not saved — enter at least one UTR.");
        var res = await Finance.SaveBulkUtrsAsync(no, given);
        return Back(returnUrl, res.Success ? "UTR updated for " + res.AffectedCount + " ticket(s) — marked as paid." : SaveFailed(res.Message, "UTR not saved."));
    }

    /// <summary>The approver (L2) approves or rejects an advance request (no reason is asked for).</summary>
    [HttpPost("Approve")]
    public Task<IActionResult> Approve(string sid, string? returnUrl) => Decide(sid, true, returnUrl);

    [HttpPost("Reject")]
    public Task<IActionResult> Reject(string sid, string? reason, string? returnUrl) => Decide(sid, false, returnUrl, reason?.Trim());

    async Task<IActionResult> Decide(string sid, bool approve, string? returnUrl, string? reason = null)
    {
        if (ApproverOnly(returnUrl) is { } denied) return denied;
        if (!approve && string.IsNullOrEmpty(reason)) return Back(returnUrl, "Not rejected — a reason is required.");
        var res = await Finance.DecideAdvanceAsync(sid, approve, Ctx.UserName, reason);
        var s = St.Svcs().FirstOrDefault(x => x.Id == sid);
        var amt = St.Adv.FirstOrDefault(x => x.Sid == sid)?.Amt ?? 0;
        return Back(returnUrl, !res.Success ? SaveFailed(res.Message, "Not saved.")
            : approve ? $"L2 approved — advance for {s?.T.Id} ({Fmt.Inr(amt)})" : $"L2 rejected — advance for {s?.T.Id} ({Fmt.Inr(amt)}).");
    }
}
