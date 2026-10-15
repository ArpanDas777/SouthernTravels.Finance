using Microsoft.AspNetCore.Mvc;
using SouthernTravels.Areas.Finance.Data;

namespace SouthernTravels.Areas.Finance.Controllers;

/// <summary>The Finance area's front door and the one endpoint the reports share.</summary>
public class HomeController(FinanceStore store) : FinanceControllerBase(store)
{
    [HttpGet("Finance")]
    public IActionResult Index() => Redirect(FinanceArea.Home);

    /// <summary>Clears a vendor's red dot once its row is expanded (area "pr" = Payment Request, "avp" = Approved Vendor Payable).</summary>
    [HttpPost("Finance/Seen")]
    public IActionResult Seen(string area, string vendor)
    {
        St.MarkSeen(area, vendor);
        return NoContent();
    }
}
