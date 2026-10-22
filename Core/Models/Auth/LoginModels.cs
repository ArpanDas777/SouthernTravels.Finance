namespace SouthernTravels.Core.Models.Auth;

/// <summary>Body of Account/SendOTP (same as STPLCostSheet's EmailIdRequest).</summary>
public class EmailIdRequest
{
    public string EmailId { get; set; } = "";
    public string? UserIP { get; set; }
}

/// <summary>Body of Account/ValidateUserOTP (same as STPLCostSheet's EmailLoginRequest).</summary>
public class EmailLoginRequest
{
    public string EmailId { get; set; } = "";
    public string OTPCode { get; set; } = "";
    public string? UserIP { get; set; }
    /// <summary>Local page to open after login (not sent on to anywhere else).</summary>
    public string? RedirectUrl { get; set; }
}

/// <summary>The signed-in executive returned by Account/ValidateUserOTP (STPLCostSheet's Executives).</summary>
public class LoginUser
{
    public int iId { get; set; }
    public string? UserName { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? OpsBranch { get; set; }
    public DateTime? CreatedOn { get; set; }
    public bool IsActive { get; set; }
    public bool IsAdmin { get; set; }
    public int TenantId { get; set; }
    public string? accessToken { get; set; }
    public string? refreshToken { get; set; }
}

/// <summary>Reply of the Account API actions.</summary>
public class LoginApiResponse : ApiResponseViewModel
{
    public LoginUser? objOTPLogin { get; set; }
}

/// <summary>What the login page shows.</summary>
public class LoginVm
{
    public string? ReturnUrl { get; set; }
    public string? Email { get; set; }
    public string? Error { get; set; }
    public string? Info { get; set; }
}
