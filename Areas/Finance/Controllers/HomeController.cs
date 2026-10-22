using Microsoft.AspNetCore.Mvc;
using SouthernTravels.Areas.Finance.Interfaces;

namespace SouthernTravels.Areas.Finance.Controllers;

/// <summary>The Finance area's front door and the one endpoint the reports share.</summary>
public class HomeController(IFinanceService finance) : FinanceControllerBase(finance)
{
    [HttpGet("Finance")]
    public IActionResult Index() => Redirect(FinanceArea.Home);

    /// <summary>Clears a vendor's red dot once its row is expanded (area "pr" = Payment Request, "avp" = Approved Vendor Payable).</summary>
    [HttpPost("Finance/Seen")]
    public async Task<IActionResult> Seen(string area, string vendor)
    {
        var res = await Finance.MarkSeenAsync(area, vendor);
        return res.Success ? NoContent() : StatusCode(StatusCodes.Status502BadGateway);
    }
}
