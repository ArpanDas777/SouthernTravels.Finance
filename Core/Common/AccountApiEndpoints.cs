namespace SouthernTravels.Core.Common;

/// <summary>Login API (the same endpoints STPLCostSheet's Operation login uses). URL = AuthAPI:ApiUrl + "Account/" + action.</summary>
public static class AccountApiEndpoints
{
    public const string Account = "Account";

    public static class AccountActions
    {
        public const string SendOTP = "SendOTP";
        public const string ValidateUserOTP = "ValidateUserOTP";
    }
}
