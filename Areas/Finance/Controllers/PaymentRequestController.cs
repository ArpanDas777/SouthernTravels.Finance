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
    public IActionResult Popup(string t, string? sid, string? sids, string? no, string? kind, int hist)
    {
        var all = St.Svcs();
        switch (t)
        {
            case "rtgsSel":
            {
                // The ticked entries, limited to those still free for an RTGS advice.
                var ids = (sids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
                var pool = St.RtgsPool().Where(b => ids.Contains(b.Sid)).Select(b => all.First(s => s.Id == b.Sid)).OrderBy(s => s.Vi).ThenBy(s => s.Ti).ToList();
                return pool.Count == 0 ? NotFound() : View("Popups/RtgsSelect", new RtgsSelVm(pool));
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
            case "rtgsUtr":
            {
                var rec = FindRtgs(no);
                return rec == null ? NotFound() : View("Popups/RtgsUtr", new RtgsUtrVm(rec, RtgsItems(rec, all)));
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
            case "prpay" or "prview":
            {
                var s = all.FirstOrDefault(x => x.Id == sid);
                var view = t == "prview";
                // Add / Update is open until the UTR is in; View shows a paid entry.
                if (s?.Bk == null || (view ? !s.Bk.Paid : s.Bk.Paid)) return NotFound();
                return View("Popups/PaymentRequestPay", new PrPayVm(s, Ctx.UserName, view));
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

    /// <summary>The entries of an RTGS advice with their services, in vendor order.</summary>
    List<PrItemVm> RtgsItems(RtgsRecord rec, List<Svc> all) =>
        rec.Sids.Select(sid => all.First(s => s.Id == sid)).Where(s => s.Bk != null).OrderBy(s => s.Vi).ThenBy(s => s.Ti).Select(s => new PrItemVm(s.Bk!, s)).ToList();

    List<RtgsLetterRow> LetterRows(RtgsRecord rec, List<Svc> all) => LetterRowsOf(rec, RtgsItems(rec, all).Select(x => (x.S, (x.S.Bill?.Rec ?? 0) - x.B.Tds)));

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

    /// <summary>
    /// Create RTGS Advice for the ticked entries: shared payment details and a TDS % per ticket. The details are saved on the entries (no UTR yet),
    /// then "RTGS Advice generated" opens.
    /// </summary>
    [HttpPost("CreateRtgs")]
    public async Task<IActionResult> CreateRtgs(Dictionary<string, string>? tds, string? ctype, string? mode, string? bank, string? no, string? paymentDate, string? rnd, string? returnUrl)
    {
        var d = PaymentDetails(ctype, mode, bank, no, paymentDate);
        var ok = d != null && !string.IsNullOrWhiteSpace(rnd);
        var pcts = new Dictionary<string, decimal>();
        foreach (var (sid, v) in tds ?? [])
            if (decimal.TryParse(v, System.Globalization.NumberStyles.Number, Fmt.Inv, out var p) && p is >= 0 and <= 100) pcts[sid] = p;
        if (!ok || pcts.Count == 0 || pcts.Count != (tds?.Count ?? 0)) return Back(returnUrl, "RTGS advice not created — fill every field and a TDS % for each ticket.");
        // Only unpaid RTGS tickets not yet covered by an advice can go into a new one.
        var pool = St.RtgsPool().Select(b => b.Sid).ToHashSet();
        pcts = pcts.Where(x => pool.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);
        if (pcts.Count == 0) return Back(returnUrl, "Those tickets are already covered by an RTGS advice.");
        var res = await Finance.CreateRtgsAsync(pcts, new RtgsCommon
        {
            SenderBank = d!.Value.Bank, CType = d.Value.CType, Mode = d.Value.Mode, ChequeNo = d.Value.No, ChequeDate = d.Value.Date, RandomNo = rnd!.Trim(),
        }, Ctx.UserName);
        if (!res.Success || string.IsNullOrEmpty(res.RtgsNo)) return Back(returnUrl, SaveFailed(res.Message, "RTGS advice not created."));
        TempData["popup"] = "/Finance/PaymentRequest/Popup?t=rtgsOut&no=" + Uri.EscapeDataString(res.RtgsNo);
        return Back(returnUrl, "RTGS advice " + res.RtgsNo + " created.");
    }

    /// <summary>Step 1 (no details yet): everything except the UTR. Step 2 (details added): the same form with a mandatory UTR, which marks the entry Paid.</summary>
    [HttpPost("PaymentPay")]
    public async Task<IActionResult> PaymentPay(string sid, string? ctype, string? mode, string? bank, string? no, string? paymentDate, string? utr, string? returnUrl)
    {
        var b = St.Svcs().FirstOrDefault(x => x.Id == sid)?.Bk;
        if (b is not { Paid: false }) return Back(returnUrl, "Payment not saved — that ticket line is not awaiting payment.");
        var step2 = b.HasDetails;
        var d = PaymentDetails(ctype, mode, bank, no, paymentDate);
        if (d is null || (step2 && string.IsNullOrWhiteSpace(utr))) return Back(returnUrl, "Payment not saved — every field is required.");
        var res = await Finance.SavePayDetailsAsync(sid, Ctx.UserName, d.Value.CType, d.Value.Mode, d.Value.Bank, d.Value.No, d.Value.Date, step2 ? utr!.Trim() : null);
        if (!res.Success) return Back(returnUrl, SaveFailed(res.Message, "Payment not saved."));
        return Back(returnUrl, step2 ? "Payment recorded." : "Payment details added — the advice can now be printed.");
    }

    /// <summary>Update Details from RTGS Advice History: a UTR per ticket; a ticket with a UTR becomes Paid, the rest stay awaiting.</summary>
    [HttpPost("RtgsUtr")]
    public async Task<IActionResult> RtgsUtr(string no, Dictionary<string, string>? utr, string? returnUrl)
    {
        var given = (utr ?? []).Where(x => !string.IsNullOrWhiteSpace(x.Value)).ToDictionary(x => x.Key, x => x.Value.Trim());
        if (FindRtgs(no) == null || given.Count == 0) return Back(returnUrl, "UTR not saved — enter at least one UTR.");
        var res = await Finance.SaveRtgsUtrsAsync(no, Ctx.UserName, given);
        return Back(returnUrl, res.Success ? res.AffectedCount + " ticket(s) of " + no + " marked as paid." : SaveFailed(res.Message, "UTR not saved."));
    }
}
