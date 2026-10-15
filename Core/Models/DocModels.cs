namespace SouthernTravels.Core.Models;

/// <summary>A label/value grid inside a popup ("facts" block).</summary>
public sealed class FactsBlock
{
    public int Cols { get; init; } = 2;
    public bool Strip { get; init; }
    public bool Warn { get; init; }
    public string? Title { get; init; }
    public List<(string L, string V, string? Color, bool Bold)> Items { get; init; } = [];
}

/// <summary>The header every printable document shares: company block, title, reference rows and the generated time.</summary>
public sealed record DocHead(string Title, List<(string K, string V)> Rows, string Generated);
