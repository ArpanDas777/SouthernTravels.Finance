using SouthernTravels.Core.Models.Auth;

namespace SouthernTravels.Core.Interfaces;

/// <summary>OTP login against the Account API.</summary>
public interface IAccountService
{
    Task<LoginApiResponse> SendOtpAsync(EmailIdRequest request);
    Task<LoginApiResponse> ValidateOtpAsync(EmailLoginRequest request);
}
