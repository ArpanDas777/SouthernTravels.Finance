using Microsoft.AspNetCore.Mvc;
using SouthernTravels.Core.Infrastructure;

namespace SouthernTravels.Core.Controllers;

/// <summary>Demo user switch and the period filter in the shared layout; they work the same in every area.</summary>
[AutoValidateAntiforgeryToken]
[Route("Session")]
public class SessionController : Controller
{
    [HttpPost("SetUser")]
    public IActionResult SetUser(string user, string? returnUrl)
    {
        if (DemoSession.Users.ContainsKey(user)) Response.Cookies.Append(DemoSession.UserCookie, user, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax });
        return Back(returnUrl);
    }

    [HttpPost("SetPeriod")]
    public IActionResult SetPeriod(string k, string? from, string? to, string? returnUrl)
    {
        if (!DemoSession.PeriodKinds.Contains(k)) k = "All time";
        Response.Cookies.Append(DemoSession.PeriodCookie, k + "|" + (from ?? "") + "|" + (to ?? ""), new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax });
        return Back(returnUrl);
    }

    IActionResult Back(string? returnUrl) => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : Redirect("/");
}
