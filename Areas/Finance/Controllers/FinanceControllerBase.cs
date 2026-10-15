using Microsoft.AspNetCore.Mvc;
using SouthernTravels.Areas.Finance.Data;
using SouthernTravels.Areas.Finance.Reports;
using SouthernTravels.Core.Controllers;
using SouthernTravels.Core.Infrastructure;
using SouthernTravels.Core.Models;

namespace SouthernTravels.Areas.Finance.Controllers;

/// <summary>Shared by the Finance report controllers: the store, the area's shell settings and report rendering.</summary>
[Area(FinanceArea.Name)]
public abstract class FinanceControllerBase(FinanceStore store) : AppController
{
    protected FinanceStore St => store;
    protected override AreaInfo Area => FinanceArea.Info;

    FinanceReports Reports => new(store, ReportQuery.Parse(Request), Ctx.Perm, Period);

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
