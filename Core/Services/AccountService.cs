using SouthernTravels.Core.Common;
using SouthernTravels.Core.Helpers;
using SouthernTravels.Core.Interfaces;
using SouthernTravels.Core.Models.Auth;

namespace SouthernTravels.Core.Services;

/// <summary>Calls Account/SendOTP and Account/ValidateUserOTP, like STPLCostSheet's OperationService.SendOTPAsync / ValidateUserOTPAsync.</summary>
public class AccountService(IApiClientHelper apiClient, IConfiguration config, ILogger<AccountService> logger)
    : BaseApiService(config, "AuthAPI"), IAccountService
{
    public Task<LoginApiResponse> SendOtpAsync(EmailIdRequest request) =>
        Call(AccountApiEndpoints.AccountActions.SendOTP, request, "Failed to send OTP.");

    public Task<LoginApiResponse> ValidateOtpAsync(EmailLoginRequest request) =>
        // RedirectUrl stays in this app; only the email, OTP and IP go to the API.
        Call(AccountApiEndpoints.AccountActions.ValidateUserOTP,
            new { request.EmailId, request.OTPCode, request.UserIP }, "OTP validation failed.");

    async Task<LoginApiResponse> Call(string action, object body, string fallback)
    {
        try
        {
            return await apiClient.PostAsync<LoginApiResponse>(BuildUrl(AccountApiEndpoints.Account, action), body)
                ?? Fail<LoginApiResponse>(fallback);
        }
        catch (ApplicationException ex)
        {
            logger.LogError(ex, "Account API {Action} failed", action);
            return Fail<LoginApiResponse>(ex.Message);
        }
    }
}
