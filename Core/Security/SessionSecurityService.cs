namespace SouthernTravels.Core.Security;

public class SessionSecurityService(IHttpContextAccessor httpContext) : ISessionSecurityService
{
    public (bool IsValid, string? UserName, string? Token, string? LoginBranchCode, bool IsAdmin, string? TenantId, string? ErrorMessage) ValidateSession()
    {
        var session = httpContext.HttpContext?.Session;
        if (session == null) return (false, null, null, null, false, null, "Session is not available.");

        var userName = session.GetString(SessionKeys.UserId);
        var token = session.GetString(SessionKeys.AccessToken);
        var branch = session.GetString(SessionKeys.HandlingBranch);
        var tenantId = session.GetString(SessionKeys.TenantId);
        var isAdmin = string.Equals(session.GetString(SessionKeys.IsAdmin), "true", StringComparison.OrdinalIgnoreCase);

        // HandlingBranch is not required here: Finance users may not have an operations branch.
        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(token))
            return (false, null, null, null, false, null, "Session expired. Please login again.");

        return (true, userName, token, branch, isAdmin, tenantId, null);
    }
}
