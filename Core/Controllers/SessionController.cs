using Microsoft.AspNetCore.Mvc;
using SouthernTravels.Core.Infrastructure;
using SouthernTravels.Core.Security;

namespace SouthernTravels.Core.Controllers;

/// <summary>The period filter in each report's title row; it works the same in every area.</summary>
[AutoValidateAntiforgeryToken]
[Route("Session")]
public class SessionController : Controller
{
    [HttpPost("SetPeriod")]
    public IActionResult SetPeriod(string k, string? from, string? to, string? returnUrl)
    {
        if (!PeriodFilter.PeriodKinds.Contains(k)) k = "All time";
        Response.Cookies.Append(PeriodFilter.PeriodCookie, k + "|" + (from ?? "") + "|" + (to ?? ""), new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax });
        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : Redirect("/");
    }

    /// <summary>The Switch user control in the sidebar: User or Approver. It overrides the mode the login account starts in. Returns to the page without its query, so tabs reset.</summary>
    [HttpPost("SetMode")]
    public IActionResult SetMode(string m, string? returnUrl)
    {
        HttpContext.Session.SetString(SessionKeys.Mode, m == "approver" ? "approver" : "user");
        var path = (returnUrl ?? "").Split('?')[0];
        return !string.IsNullOrEmpty(path) && Url.IsLocalUrl(path) ? LocalRedirect(path) : Redirect("/");
    }
}
