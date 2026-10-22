namespace SouthernTravels.Core.Models;

/// <summary>Common envelope every API reply starts with. Each area derives its own response with its obj... lists.</summary>
public class ApiResponseViewModel
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}
