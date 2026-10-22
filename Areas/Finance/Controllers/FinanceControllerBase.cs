using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SouthernTravels.Areas.Finance.Data;
using SouthernTravels.Areas.Finance.Interfaces;
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

    FinanceReports Reports => new(St, ReportQuery.Parse(Request), Period);

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
        return View("Report", build(Reports));
    }

    /// <summary>A real yyyy-MM-dd date that is not in the future.</summary>
    protected static bool ValidPaymentDate(string? d) =>
        DateTime.TryParseExact(d, "yyyy-MM-dd", Fmt.Inv, System.Globalization.DateTimeStyles.None, out _) && Fmt.Cmp(d!, Clock.Today) <= 0;

    /// <summary>Cheque No. is required for cheque payments; UTR for electronic transfers (neither for cash).</summary>
    protected static bool PaymentRefOk(string? mode, string? utr, string? chequeNo) => mode switch
    {
        "Cheque" => !string.IsNullOrWhiteSpace(chequeNo),
        "Cash" => true,
        _ => !string.IsNullOrWhiteSpace(utr),
    };
}
