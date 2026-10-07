using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SouthernTravels.Finance.Data;
using SouthernTravels.Finance.Infrastructure;
using SouthernTravels.Finance.Models;
using SouthernTravels.Finance.Reports;

namespace SouthernTravels.Finance.Controllers;

[AutoValidateAntiforgeryToken]
public class FinanceController(FinanceStore st) : Controller
{
    const string UserCookie = "fin_user", PeriodCookie = "fin_period";
    public static readonly string[] PeriodKinds = ["All time", "Today", "This Week", "This Month", "This Quarter", "This Year", "Custom"];
    public static readonly string[] PaymentModes = ["NEFT", "RTGS", "IMPS", "Cheque", "Cash"];

    static readonly Dictionary<string, (string Name, string Role)> Users = new()
    {
        ["admin"] = ("Administrator", "Administrator"),
        ["mounika"] = ("Mounika", "Finance Executive"),
        ["vijay"] = ("Vijay Gupta", "Finance Head"),
    };

    LayoutCtx Ctx { get; set; } = new();
    (string From, string To)? Period { get; set; }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var user = Request.Cookies[UserCookie] is { } u && Users.ContainsKey(u) ? u : "admin";
        var parts = (Request.Cookies[PeriodCookie] ?? "All time||").Split('|');
        string kind = PeriodKinds.Contains(parts[0]) ? parts[0] : "All time", from = parts.ElementAtOrDefault(1) ?? "", to = parts.ElementAtOrDefault(2) ?? "";
        Period = Range(kind, from, to);
        Ctx = new LayoutCtx
        {
            User = user, UserName = Users[user].Name, UserRole = Users[user].Role,
            PeriodKind = kind, PeriodFrom = from, PeriodTo = to,
            PeriodText = Period is { } r ? (kind == "Custom" && from == "" ? "…" : Fmt.Fd(r.From)) + " → " + (kind == "Custom" && to == "" ? "…" : Fmt.Fd(r.To)) : null,
            Toast = TempData["toast"] as string,
        };
        ViewData["Ctx"] = Ctx;
        base.OnActionExecuting(context);
    }

    static (string, string)? Range(string kind, string from, string to)
    {
        var t = Fmt.D(FinanceStore.Today);
        var today = FinanceStore.Today;
        switch (kind)
        {
            case "Today": return (today, today);
            case "This Week": { var dow = ((int)t.DayOfWeek + 6) % 7; return (Fmt.Add(today, -dow), Fmt.Add(today, 6 - dow)); }
            case "This Month": return (Fmt.Ymd(new DateTime(t.Year, t.Month, 1)), Fmt.Ymd(new DateTime(t.Year, t.Month, 1).AddMonths(1).AddDays(-1)));
            case "This Quarter": { var q = new DateTime(t.Year, (t.Month - 1) / 3 * 3 + 1, 1); return (Fmt.Ymd(q), Fmt.Ymd(q.AddMonths(3).AddDays(-1))); }
            case "This Year": return (t.Year + "-01-01", t.Year + "-12-31");
            case "Custom": return (from != "" ? from : "0000-00-00", to != "" ? to : "9999-12-31");
            default: return null;
        }
    }

    FinanceReports Reports => new(st, ReportQuery.Parse(Request), Ctx.Perm, Period);

    IActionResult Report(string page, string periodField, Func<FinanceReports, ReportVm> build)
    {
        Ctx.Page = page;
        Ctx.PeriodField = periodField;
        return View("Report", build(Reports));
    }

    /* ---------- reports ---------- */

    public IActionResult VendorPayable() => Report(nameof(VendorPayable), "Bill Date", r => r.VendorPayable());
    public IActionResult Advance() => Report(nameof(Advance), "Advance Requested Date", r => r.Advance());
    public IActionResult ApprovedVendorPayable() => Report(nameof(ApprovedVendorPayable), "Bill Date", r => r.ApprovedVendorPayable());
    public IActionResult PaymentRequest() => Report(nameof(PaymentRequest), "Bill Date", r => r.PaymentRequest());
    public IActionResult VendorKyc() => Report(nameof(VendorKyc), "Date of Creation", r => r.VendorKyc());

    /* ---------- popups (loaded into the modal by site.js) ---------- */

    [HttpGet]
    public IActionResult Popup(string t, string? ids, int re, string? id, int? vi, string? sid, int? k, string? n)
    {
        var all = st.Svcs();
        if (t is "raise" or "advice" or "advpay" or "prpay" && !Ctx.Perm) return StatusCode(StatusCodes.Status403Forbidden);
        switch (t)
        {
            case "raise":
            {
                var set = (ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
                var ss = set.Select(x => all.FirstOrDefault(s => s.Id == x)).OfType<Svc>()
                    .Where(s => re == 1 ? s.Bill != null : s.Eligible).ToList();
                return ss.Count == 0 ? NotFound() : View("Popups/Raise", new RaiseVm(ss, re == 1));
            }
            case "viewbill":
            {
                var s = all.FirstOrDefault(x => x.Id == id);
                return s?.Bill == null ? NotFound() : View("Popups/ViewBill", s);
            }
            case "vbills":
            {
                if (vi is not { } v || v < 0 || v >= st.V.Count) return NotFound();
                List<Svc> ss;
                lock (st.Gate) ss = st.AwaitingSubmission(all).Where(s => s.Vi == v).ToList();
                return View("Popups/VendorBills", new VendorBillsVm(st.V[v], ss));
            }
            case "advice": return View("Popups/Advice", BuildAdvice(all, (ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet()));
            case "advpay":
            case "advview":
            {
                AdvanceRequest? a;
                lock (st.Gate) a = st.Adv.FirstOrDefault(x => x.Sid == sid);
                var s = all.FirstOrDefault(x => x.Id == sid);
                if (a == null || s == null) return NotFound();
                var view = t == "advview";
                if (view ? a.Pay == null : a.Pay != null) return NotFound();
                return View("Popups/AdvancePayment", new AdvPayVm(a, s, view));
            }
            case "prpay":
            {
                var s = all.FirstOrDefault(x => x.Id == sid);
                return s?.Bk == null || s.Bk.Paid ? NotFound() : View("Popups/PaymentRequestPay", new PrPayVm(s, Ctx.UserName));
            }
            case "doc":
            {
                if (k is not { } ki || ki < 0 || ki >= st.V.Count || string.IsNullOrEmpty(n)) return NotFound();
                var v = st.V[ki];
                var file = v.Id + "_" + System.Text.RegularExpressions.Regex.Replace(n, "[^A-Za-z]+", "_") + ".pdf";
                return View("Popups/Document", new DocVm(v, n, file));
            }
            default: return NotFound();
        }
    }

    /// <summary>Printable Advance Advice (A4). Opened in a new tab from the Advance Report; payment details fill in once paid.</summary>
    [HttpGet]
    public IActionResult AdvanceAdvice(string sid)
    {
        int index; AdvanceRequest? a;
        lock (st.Gate)
        {
            index = st.Adv.FindIndex(x => x.Sid == sid);
            a = index >= 0 ? st.Adv[index] : null;
        }
        // Only approved requests (L1 and L2) get an advice.
        if (a is not { L1: "A", L2: "A" }) return NotFound();
        var s = st.Svcs().First(x => x.Id == sid);
        var seq = (index + 1).ToString("0000", Fmt.Inv);
        var vm = new AdvanceAdviceVm(
            AdviceNo: "ADV/2026-27/" + seq, RequestNo: "AR/2026-27/" + seq, FileNo: s.T.File,
            Date: Fmt.Dmy(a.Pay?.PaymentDate ?? Fmt.Add(a.At, 1)), Generated: Fmt.DmyTime(FinanceStore.NowStamp()),
            Service: s, Kyc: st.K[s.Vi], Req: a);
        return View("AdvanceAdvice", vm);
    }

    /// <summary>Printable Payment Advice (A4) for a vendor — lists the unpaid basket tickets passed from the drill-down.</summary>
    [HttpGet]
    public IActionResult PaymentAdvice(int vi, string? sids)
    {
        if (vi < 0 || vi >= st.V.Count) return NotFound();
        var wanted = (sids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var all = st.Svcs();
        List<BasketItem> basket;
        lock (st.Gate) basket = st.Basket.ToList();
        // Only this vendor's basket items that are still unpaid; anything paid since the page loaded drops out.
        var items = basket.Where(b => !b.Paid && wanted.Contains(b.Sid))
            .Select(b => all.First(s => s.Id == b.Sid))
            .Where(s => s.Vi == vi)
            .OrderBy(s => s.Ti).ThenBy(s => s.Start, StringComparer.Ordinal)
            .ToList();
        if (items.Count == 0) return NotFound();
        var vm = new PaymentAdviceVm(
            AdviceNo: "PAY/2026-27/" + (vi + 1).ToString("0000", Fmt.Inv), Date: Fmt.Dmy(FinanceStore.Today),
            Generated: Fmt.DmyTime(FinanceStore.NowStamp()), Vendor: st.V[vi], Kyc: st.K[vi], Items: items);
        return View("PaymentAdvice", vm);
    }

    AdviceVm BuildAdvice(List<Svc> all, HashSet<string> ids)
    {
        List<Svc> pool; int basketCount;
        lock (st.Gate) { pool = st.AwaitingSubmission(all); basketCount = st.Basket.Count; }
        var sel = pool.Where(s => ids.Contains(s.Id)).ToList();
        var vs = sel.GroupBy(s => s.Vi).ToList();
        var rows = vs.Select((g, i) => new AdviceRow(i + 1, st.V[g.Key].Name, st.V[g.Key].Id, g.Select(s => s.Ti).Distinct().Count(), Fmt.Inr(g.Sum(s => s.Bill!.Rec)))).ToList();
        var pending = vs.Select(g =>
        {
            var rest = pool.Where(s => s.Vi == g.Key && !ids.Contains(s.Id)).ToList();
            return rest.Count > 0 ? $"{st.V[g.Key].Name}: {rest.Select(s => s.Ti).Distinct().Count()} ticket(s) pending ({Fmt.Inr(rest.Sum(s => s.Bill!.Rec))})" : null;
        }).OfType<string>().ToList();
        return new AdviceVm(sel.Select(s => s.Id).ToList(), "PSA-2610-" + (101 + basketCount), Fmt.Fdt(FinanceStore.NowStamp()), rows,
            sel.Select(s => s.Ti).Distinct().Count() + " tickets · " + vs.Count + " vendors", Fmt.Inr(sel.Sum(s => s.Bill!.Rec)), pending, sel.Count, vs.Count);
    }

    /* ---------- actions ---------- */

    IActionResult Back(string? returnUrl, string? toast = null)
    {
        if (toast != null) TempData["toast"] = toast;
        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(VendorPayable));
    }

    [HttpPost]
    public IActionResult RaiseBill(RaiseForm form)
    {
        if (!Ctx.Perm) return StatusCode(StatusCodes.Status403Forbidden);
        var items = form.Items.Where(i => !string.IsNullOrEmpty(i.Sid)).ToList();
        var valid = items.Count > 0 && items.All(i => !string.IsNullOrWhiteSpace(i.Date) && !string.IsNullOrWhiteSpace(i.No) && i.File != null && i.Inv > 0 && i.Rec > 0);
        if (!valid) return Back(form.ReturnUrl, "Bill not saved — every field is required.");
        var kind = form.Kind == "D" ? "D" : "R";
        var stamp = FinanceStore.NowStamp();
        // The uploaded file is not stored yet; only its name is kept until a document store is connected.
        st.RaiseBills(items.Select(i => (i.Sid, new Bill
        {
            St = kind, Date = i.Date!, No = i.No!.Trim(), Doc = Path.GetFileName(i.File!.FileName), Inv = (int)i.Inv!.Value, Rec = (int)i.Rec!.Value,
            Upl = stamp, L1 = new Approval { St = "Q" }, L2 = new Approval { St = "-" },
        })));
        return Back(form.ReturnUrl, kind == "R" ? $"Bill submitted for {items.Count} service(s)." : $"Dispute raised for {items.Count} service(s).");
    }

    [HttpPost]
    public IActionResult Submit(string ids, string? returnUrl)
    {
        if (!Ctx.Perm) return StatusCode(StatusCodes.Status403Forbidden);
        var n = st.Submit((ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet());
        return Back(returnUrl, n + " service(s) sent to Payment Request Report.");
    }

    [HttpPost]
    public IActionResult AdvancePay(string sid, decimal? pct, string? ctype, string? no, string? bank, string? mode,
        string? paymentDate, decimal? amountPaid, IFormFile? slip, string? returnUrl)
    {
        if (!Ctx.Perm) return StatusCode(StatusCodes.Status403Forbidden);
        if (pct is not { } p || p < 0 || p > 100 || string.IsNullOrWhiteSpace(no) || string.IsNullOrWhiteSpace(bank) || string.IsNullOrWhiteSpace(mode)
            || !ValidPaymentDate(paymentDate) || amountPaid is not > 0 || slip is null)
            return Back(returnUrl, "Payment not saved — every field is required.");
        // The uploaded slip is not stored yet; only its name is kept until a document store is connected.
        st.PayAdvance(sid, new AdvancePayment
        {
            Pct = p, CType = ctype == "Online" ? "Online" : "Physical Cheque", No = no.Trim(), Bank = bank.Trim(), Mode = mode,
            PaymentDate = paymentDate!, AmountPaid = (int)amountPaid.Value, Slip = Path.GetFileName(slip.FileName),
        });
        return Back(returnUrl, "Payment updated.");
    }

    [HttpPost]
    public IActionResult PaymentPay(string sid, string? bank, string? mode, string? utr, string? chequeNo,
        string? paymentDate, decimal? amountPaid, IFormFile? slip, string? returnUrl)
    {
        if (!Ctx.Perm) return StatusCode(StatusCodes.Status403Forbidden);
        // Cheque No. is required for cheque payments; UTR for electronic transfers (not for cash or cheque).
        var refOk = mode switch
        {
            "Cheque" => !string.IsNullOrWhiteSpace(chequeNo),
            "Cash" => true,
            _ => !string.IsNullOrWhiteSpace(utr),
        };
        if (string.IsNullOrWhiteSpace(bank) || string.IsNullOrWhiteSpace(mode) || !refOk || !ValidPaymentDate(paymentDate) || amountPaid is not > 0 || slip is null)
            return Back(returnUrl, "Payment not saved — every field is required.");
        st.PayRequest(sid, Ctx.UserName, bank.Trim(), mode, utr?.Trim(), chequeNo?.Trim(), paymentDate!, (int)amountPaid.Value, Path.GetFileName(slip.FileName));
        return Back(returnUrl, "Payment recorded.");
    }

    /// <summary>A real yyyy-MM-dd date that is not in the future.</summary>
    static bool ValidPaymentDate(string? d) =>
        DateTime.TryParseExact(d, "yyyy-MM-dd", Fmt.Inv, System.Globalization.DateTimeStyles.None, out _) && Fmt.Cmp(d!, FinanceStore.Today) <= 0;

    /// <summary>Clears the red "new tickets" dot once a vendor row is expanded in the Payment Request Report.</summary>
    [HttpPost]
    public IActionResult Seen(string vendor)
    {
        st.MarkSeen(vendor);
        return NoContent();
    }

    [HttpPost]
    public IActionResult SetUser(string user, string? returnUrl)
    {
        if (Users.ContainsKey(user)) Response.Cookies.Append(UserCookie, user, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax });
        return Back(returnUrl);
    }

    [HttpPost]
    public IActionResult SetPeriod(string k, string? from, string? to, string? returnUrl)
    {
        if (!PeriodKinds.Contains(k)) k = "All time";
        Response.Cookies.Append(PeriodCookie, k + "|" + (from ?? "") + "|" + (to ?? ""), new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax });
        return Back(returnUrl);
    }
}
