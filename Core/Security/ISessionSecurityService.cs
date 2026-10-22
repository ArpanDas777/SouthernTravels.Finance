namespace SouthernTravels.Core.Security;

/// <summary>Reads the signed-in user and API token from the session (same contract as STPLCostSheet).</summary>
public interface ISessionSecurityService
{
    (bool IsValid, string? UserName, string? Token, string? LoginBranchCode, bool IsAdmin, string? TenantId, string? ErrorMessage) ValidateSession();
}

/// <summary>Session keys shared by the login, the middleware and <see cref="SessionSecurityService"/>.</summary>
public static class SessionKeys
{
    public const string UserId = "UserId", UserName = "UserName", Email = "Email", AccessToken = "accessToken", RefreshToken = "refreshToken",
        HandlingBranch = "HandlingBranch", TenantId = "TenantId", IsAdmin = "IsAdmin", LoginTime = "LoginTime";
}
