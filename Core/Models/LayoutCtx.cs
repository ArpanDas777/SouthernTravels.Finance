namespace SouthernTravels.Core.Models;

/// <summary>One entry of an area's side menu. <paramref name="Controller"/> is also the value of <see cref="LayoutCtx.Page"/> on that page.</summary>
public sealed record NavItem(string Controller, string Label, string Icon, string Url);

/// <summary>Everything the shared layout needs: who is signed in, the period filter, the area's menu, toast and auto-popup.</summary>
public sealed class LayoutCtx
{
    public string UserName { get; init; } = "Administrator";
    public string UserRole { get; init; } = "Administrator";

    /// <summary>The area being shown (e.g. "Finance"): names the product in the header and the menu heading.</summary>
    public string AreaName { get; init; } = "";
    public string AreaShort { get; init; } = "";
    public List<NavItem> Nav { get; init; } = [];
    /// <summary>POST endpoint that clears a "new" dot once a row is opened; null when the area has none.</summary>
    public string? SeenUrl { get; init; }

    public string Page { get; set; } = "";
    public string PeriodKind { get; init; } = "All time";
    public string PeriodFrom { get; init; } = "";
    public string PeriodTo { get; init; } = "";
    public string? PeriodText { get; init; }
    public string PeriodField { get; set; } = "Bill Date";
    public string? Toast { get; set; }
    /// <summary>A pop-up URL to open as soon as the page loads (e.g. "RTGS Advice generated" after creating one).</summary>
    public string? AutoPopup { get; init; }
}
