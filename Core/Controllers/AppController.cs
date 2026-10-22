using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SouthernTravels.Core.Infrastructure;
using SouthernTravels.Core.Models;
using SouthernTravels.Core.Security;

namespace SouthernTravels.Core.Controllers;

/// <summary>What an area tells the shell about itself: its name, menu and home page.</summary>
public sealed record AreaInfo(string Name, string Short, string HomeUrl, List<NavItem> Nav, string? SeenUrl = null);

/// <summary>
/// Base of every controller in every area. Builds the <see cref="LayoutCtx"/> (signed-in user, period, toast, auto-popup, area menu)
/// that the shared layout reads, and gives the common "go back with a toast" redirect.
/// </summary>
[AutoValidateAntiforgeryToken]
public abstract class AppController : Controller
{
    /// <summary>Implemented once per area.</summary>
    protected abstract AreaInfo Area { get; }

    protected LayoutCtx Ctx { get; private set; } = new();
    protected (string From, string To)? Period { get; private set; }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var (_, kind, from, to) = DemoSession.Read(Request);
        Period = DemoSession.Range(kind, from, to);
        // The signed-in user comes from the session set at login (SessionMiddleware sends anyone without one to the login page).
        var s = HttpContext.Session;
        var user = s.GetString(SessionKeys.UserId) ?? "";
        var branch = s.GetString(SessionKeys.HandlingBranch);
        var isAdmin = string.Equals(s.GetString(SessionKeys.IsAdmin), "true", StringComparison.OrdinalIgnoreCase);
        Ctx = new LayoutCtx
        {
            User = user, UserName = s.GetString(SessionKeys.UserName) is { Length: > 0 } n ? n : user,
            UserRole = (isAdmin ? "Administrator" : "User") + (string.IsNullOrEmpty(branch) ? "" : " · " + branch),
            AreaName = Area.Name, AreaShort = Area.Short, Nav = Area.Nav, SeenUrl = Area.SeenUrl,
            PeriodKind = kind, PeriodFrom = from, PeriodTo = to,
            PeriodText = Period is { } r ? (kind == "Custom" && from == "" ? "…" : Fmt.Fd(r.From)) + " → " + (kind == "Custom" && to == "" ? "…" : Fmt.Fd(r.To)) : null,
            Toast = TempData["toast"] as string, AutoPopup = TempData["popup"] as string,
        };
        ViewData["Ctx"] = Ctx;
        base.OnActionExecuting(context);
    }

    /// <summary>Redirects to <paramref name="returnUrl"/> (local URLs only) or the area's home page, optionally with a toast.</summary>
    protected IActionResult Back(string? returnUrl, string? toast = null)
    {
        if (toast != null) TempData["toast"] = toast;
        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : Redirect(Area.HomeUrl);
    }

}
