using System.Collections;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using SouthernTravels.Core.Common;

namespace SouthernTravels.Core.Helpers;

/// <summary>
/// Typed HttpClient used by every API service (registered with AddHttpClient&lt;IApiClientHelper, ApiClientHelper&gt;).
/// Same behaviour as STPLCostSheet: 404 returns default, other errors throw ApplicationException with the API's message.
/// </summary>
public class ApiClientHelper(HttpClient httpClient, ILogger<ApiClientHelper> logger) : IApiClientHelper
{
    public async Task<T?> PostAsync<T>(string url, object requestObj)
    {
        string responseJson;
        HttpResponseMessage response;
        try
        {
            var json = JsonConvert.SerializeObject(requestObj);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            response = await httpClient.PostAsync(url, content);
            responseJson = await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "API not reachable: {Url}", url);
            throw new ApplicationException("API is not reachable. Please ensure the service is running.", ex);
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, "API timed out: {Url}", url);
            throw new ApplicationException("API request timed out. Please try again later.", ex);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
            return default;

        if (!response.IsSuccessStatusCode)
        {
            ApiErrorResponse? apiError = null;
            try { apiError = JsonConvert.DeserializeObject<ApiErrorResponse>(responseJson); }
            catch (JsonException) { /* not JSON: fall through to the raw body */ }

            logger.LogWarning("API error {Status} from {Url}: {Body}", (int)response.StatusCode, url, responseJson);
            throw new ApplicationException(!string.IsNullOrWhiteSpace(apiError?.Message)
                ? apiError.Message
                : $"API Error ({(int)response.StatusCode}): {responseJson}");
        }

        if (string.IsNullOrWhiteSpace(responseJson))
            return default;

        // Smart deserialization: if T is List<TItem> but the API returned a single object, wrap it in a list.
        try
        {
            return JsonConvert.DeserializeObject<T>(responseJson);
        }
        catch (JsonSerializationException)
        {
            if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(List<>))
            {
                var itemType = typeof(T).GetGenericArguments()[0];
                var single = JsonConvert.DeserializeObject(responseJson, itemType);
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
                list.Add(single);
                return (T)list;
            }

            throw new ApplicationException(
                $"Deserialization failed. JSON structure does not match type {typeof(T).Name}. \nResponse: {responseJson}");
        }
    }
}
