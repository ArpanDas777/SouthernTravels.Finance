namespace SouthernTravels.Core.Helpers;

/// <summary>Posts a request object as JSON to the backend API and reads the JSON reply (same contract as STPLCostSheet).</summary>
public interface IApiClientHelper
{
    Task<T?> PostAsync<T>(string url, object requestObj);
}
