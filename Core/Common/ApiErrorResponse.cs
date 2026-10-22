namespace SouthernTravels.Core.Common;

/// <summary>Error body the API returns with a non-success status code.</summary>
public class ApiErrorResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? TraceId { get; set; }
}
