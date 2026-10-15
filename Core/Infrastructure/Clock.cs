namespace SouthernTravels.Core.Infrastructure;

/// <summary>The app's notion of "today". Fixed so the sample data stays meaningful; swap for the real date when a database is connected.</summary>
public static class Clock
{
    public const string Today = "2026-10-05";
    public static string NowStamp() => Today + " " + DateTime.Now.ToString("HH:mm", Fmt.Inv);
}
