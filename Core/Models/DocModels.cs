namespace SouthernTravels.Core.Models;

/// <summary>A label/value grid inside a popup ("facts" block).</summary>
public sealed class FactsBlock
{
    public int Cols { get; init; } = 2;
    public bool Strip { get; init; }
    public bool Warn { get; init; }
    public string? Title { get; init; }
    public List<(string L, string V, string? Color, bool Bold)> Items { get; init; } = [];
    /// <summary>Labels whose value is shown as an underlined link (a document name; it shows a toast because no files are stored yet).</summary>
    public HashSet<string> Links { get; init; } = [];
}

/// <summary>The header every printable document shares: company block, title, reference rows and the generated time.</summary>
public sealed record DocHead(string Title, List<(string K, string V)> Rows, string Generated, bool NoContact = false);
