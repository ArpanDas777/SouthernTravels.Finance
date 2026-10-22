using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SouthernTravels.Core.Interfaces;
using SouthernTravels.Core.Models.Auth;
using SouthernTravels.Core.Security;

namespace SouthernTravels.Core.Controllers;

/// <summary>
/// OTP login shared by every area, ported from STPLCostSheet's Account/OperationLogin:
/// enter email -> Send OTP (AJAX) -> enter the 6-digit OTP -> Verify &amp; Login -> session is set -> back to the page that was asked for.
/// </summary>
[Route("Account")]
[AutoValidateAntiforgeryToken]
public class AccountController(IAccountService account) : Controller
{
    const string ViewPath = "~/Core/Views/Account/Login.cshtml";
    const string Home = "/Finance/VendorPayable";

    [HttpGet("Login")]
    public IActionResult Login(string? returnUrl, string? email, bool expired = false, bool limited = false)
    {
        HttpContext.Session.Clear();
        if (expired) TempData["LoginInfo"] = "Your session expired. Please log in again.";
        if (limited) TempData["LoginError"] = "Too many attempts. Please wait a minute and try again.";
        return View(ViewPath, new LoginVm
        {
            ReturnUrl = LocalOrNull(returnUrl), Email = email,
            Error = TempData["LoginError"] as string, Info = TempData["LoginInfo"] as string,
        });
    }

    /// <summary>Called by login.js; returns { success, message }.</summary>
    [HttpPost("SendOTP")]
    [EnableRateLimiting("loginLimit")]
    public async Task<IActionResult> SendOtp(EmailIdRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EmailId) || !request.EmailId.Contains('@'))
            return Json(new { success = false, message = "Please enter a valid email address." });

        request.EmailId = request.EmailId.Trim();
        request.UserIP = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await account.SendOtpAsync(request);
        return Json(new { success = result.Success, message = result.Message ?? (result.Success ? "OTP sent to your email." : "Failed to send OTP.") });
    }

    [HttpPost("ValidateOTP")]
    [EnableRateLimiting("loginLimit")]
    public async Task<IActionResult> ValidateOtp(EmailLoginRequest request)
    {
        var returnUrl = LocalOrNull(request.RedirectUrl);
        if (string.IsNullOrWhiteSpace(request.EmailId) || request.OTPCode?.Trim() is not { Length: 6 } otp || !otp.All(char.IsDigit))
            return Fail("Please enter the 6-digit OTP sent to your email.", request.EmailId, returnUrl);

        request.EmailId = request.EmailId.Trim();
        request.OTPCode = otp;
        request.UserIP = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await account.ValidateOtpAsync(request);
        var user = result.objOTPLogin;
        if (!result.Success || user == null || string.IsNullOrEmpty(user.accessToken))
            return Fail(result.Message ?? "OTP validation failed.", request.EmailId, returnUrl);

        SetUserSession(user, request.EmailId);
        return LocalRedirect(returnUrl ?? Home);
    }

    [HttpGet("Logout"), HttpPost("Logout")]
    [IgnoreAntiforgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        TempData["LoginInfo"] = "You have been logged out.";
        return RedirectToAction(nameof(Login));
    }

    /* ---------- helpers ---------- */

    IActionResult Fail(string message, string? email, string? returnUrl)
    {
        TempData["LoginError"] = message;
        return RedirectToAction(nameof(Login), new { returnUrl, email });
    }

    string? LocalOrNull(string? url) => !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) && !url.StartsWith("/Account", StringComparison.OrdinalIgnoreCase) ? url : null;

    /// <summary>Same session keys STPLCostSheet sets after OTP login (SetUserOtpSession).</summary>
    void SetUserSession(LoginUser u, string email)
    {
        var s = HttpContext.Session;
        s.Clear();
        s.SetString(SessionKeys.UserId, u.UserName ?? email);
        s.SetString(SessionKeys.UserName, string.IsNullOrWhiteSpace(u.Name) ? u.UserName ?? email : u.Name);
        s.SetString(SessionKeys.Email, u.Email ?? email);
        s.SetString(SessionKeys.HandlingBranch, u.OpsBranch ?? "");
        s.SetString(SessionKeys.AccessToken, u.accessToken ?? "");
        s.SetString(SessionKeys.RefreshToken, u.refreshToken ?? "");
        s.SetString(SessionKeys.IsAdmin, u.IsAdmin ? "true" : "false");
        s.SetString(SessionKeys.TenantId, u.TenantId.ToString());
        s.SetString(SessionKeys.LoginTime, DateTime.Now.ToString("dddd, dd MMMM yyyy hh:mm tt"));
    }
}
