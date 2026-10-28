using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SouthernTravels.Areas.Finance.Data;
using SouthernTravels.Areas.Finance.Interfaces;
using SouthernTravels.Areas.Finance.Models;
using SouthernTravels.Areas.Finance.Reports;
using SouthernTravels.Core.Controllers;
using SouthernTravels.Core.Infrastructure;
using SouthernTravels.Core.Models;

namespace SouthernTravels.Areas.Finance.Controllers;

/// <summary>
/// Shared by the Finance report controllers: the area's shell settings, report rendering and the data.
/// Before every action the report data is loaded through <see cref="IFinanceService"/> (the Finance API, or the
/// in-memory sample data while FinanceSettings:UseApi is false); actions save through <see cref="Finance"/>.
/// </summary>
[Area(FinanceArea.Name)]
public abstract class FinanceControllerBase(IFinanceService finance) : AppController
{
    FinanceStore? _store;
    string? _loadError;

    /// <summary>Reads and saves Finance data (API or sample data).</summary>
    protected IFinanceService Finance => finance;
    /// <summary>The report data loaded for this request.</summary>
    protected FinanceStore St => _store ?? throw new InvalidOperationException("Finance data has not been loaded.");
    protected override AreaInfo Area => FinanceArea.Info;

    FinanceReports Reports => new(St, ReportQuery.Parse(Request), Period, Ctx.ReadOnly, Ctx.Approver);

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        try
        {
            _store = await finance.GetStoreAsync();
        }
        catch (ApplicationException ex)
        {
            // API down or session expired: show the page empty with the reason, instead of an error page.
            HttpContext.RequestServices.GetRequiredService<ILogger<FinanceControllerBase>>().LogError(ex, "Loading Finance data failed");
            _store = FinanceStore.Empty();
            _loadError = ex.Message;
        }
        await base.OnActionExecutionAsync(context, next);
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        base.OnActionExecuting(context);
        // Approved Vendor Payable and Payment Request are not part of the approver's menu.
        if (Ctx.Approver && context.RouteData.Values["controller"] is "ApprovedVendorPayable" or "PaymentRequest" && context.RouteData.Values["action"] is not "Popup")
        {
            context.Result = Redirect(FinanceArea.Home);
            return;
        }
        // A read-only user (vijay) can look but not save anything.
        if (Ctx.ReadOnly && HttpMethods.IsPost(Request.Method))
        {
            context.Result = Back(Request.Form["returnUrl"], "Read-only access — nothing was saved.");
            return;
        }
        if (_loadError != null) Ctx.Toast = "Could not load Finance data — " + _loadError;
    }

    /// <summary>Toast text for a failed save: the API's message, or <paramref name="fallback"/>.</summary>
    protected static string SaveFailed(string? apiMessage, string fallback) =>
        string.IsNullOrWhiteSpace(apiMessage) ? fallback : fallback.TrimEnd('.') + " — " + apiMessage;

    /// <summary>Renders a report page. <paramref name="page"/> is the controller name (it highlights the side menu).</summary>
    protected IActionResult Report(string page, string periodField, Func<FinanceReports, ReportVm> build)
    {
        Ctx.Page = page;
        Ctx.PeriodField = periodField;
        var vm = build(Reports);
        if (Ctx.Approver) vm.Title += " (Approval)";
        return View("Report", vm);
    }

    /// <summary>Approve / reject actions are for the approver only (a read-only user never gets this far: every save is refused earlier). Returns the redirect to send back, or null when allowed.</summary>
    protected IActionResult? ApproverOnly(string? returnUrl) => Ctx.Approver ? null : Back(returnUrl, "You do not have permission to do that.");

    /// <summary>A real yyyy-MM-dd date that is not in the future.</summary>
    protected static bool ValidPaymentDate(string? d) =>
        DateTime.TryParseExact(d, "yyyy-MM-dd", Fmt.Inv, System.Globalization.DateTimeStyles.None, out _) && Fmt.Cmp(d!, Clock.Today) <= 0;

    /// <summary>
    /// One RTGS / Bulk Payment letter line per (service, net amount): the net payable to the vendor's legal name, in the given order.
    /// Sender account numbers are placeholders until real ones are supplied.
    /// </summary>
    protected List<RtgsLetterRow> LetterRowsOf(RtgsRecord rec, IEnumerable<(Svc S, int Net)> items, string remarks = "") =>
        items.Select((x, i) => new RtgsLetterRow(i + 1, rec.Common.SenderBank + " [A/c No.]", rec.Common.ChequeNo, rec.Common.RandomNo, Fmt.Fd(rec.Common.ChequeDate),
            St.K[x.S.Vi].Acc, St.K[x.S.Vi].Ifsc, x.Net, x.S.V.LegalName, remarks)).ToList();

    /// <summary>The payment details of an Advance, Bulk Payment, Payment Request or RTGS form (cheque type, mode, STPL bank, cheque / request no., date), or null when any is missing or invalid.</summary>
    protected static (string CType, string Mode, string Bank, string No, string Date)? PaymentDetails(string? ctype, string? mode, string? bank, string? no, string? date) =>
        FinanceOptions.ChequeTypes.Contains(ctype ?? "") && FinanceOptions.AdvanceModes.Contains(mode ?? "") && FinanceOptions.SenderBanks.Contains(bank ?? "")
            && !string.IsNullOrWhiteSpace(no) && ValidPaymentDate(date)
            ? (ctype!, mode!, bank!, no!.Trim(), date!) : null;
}
