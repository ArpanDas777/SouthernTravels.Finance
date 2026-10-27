namespace SouthernTravels.Core.Infrastructure;

/// <summary>The app's notion of "today": the real current date.</summary>
public static class Clock
{
    public static string Today => DateTime.Now.ToString("yyyy-MM-dd", Fmt.Inv);
    public static string NowStamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm", Fmt.Inv);
}
