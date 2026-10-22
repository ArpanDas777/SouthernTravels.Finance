using SouthernTravels.Core.Models;

namespace SouthernTravels.Core.Common;

/// <summary>
/// Base of every API service (same as STPLCostSheet): reads the API base URL from appsettings and builds
/// "{BaseUrl}{controller}/{action}" URLs. Each area passes its own appsettings section, e.g. "FinanceAPI".
/// </summary>
public abstract class BaseApiService
{
    protected readonly string BaseUrl;

    protected BaseApiService(IConfiguration config, string apiSection)
    {
        var url = config[apiSection + ":ApiUrl"];
        if (string.IsNullOrEmpty(url))
            throw new InvalidOperationException($"API BaseUrl missing in configuration ({apiSection}:ApiUrl).");
        BaseUrl = url.EndsWith('/') ? url : url + "/";
    }

    protected string BuildUrl(string controller, string action) => $"{BaseUrl}{controller}/{action}";

    /// <summary>Creates a common failed response.</summary>
    protected static T Fail<T>(string message) where T : ApiResponseViewModel, new() => new() { Success = false, Message = message };
}
