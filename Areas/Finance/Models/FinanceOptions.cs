namespace SouthernTravels.Areas.Finance.Models;

/// <summary>Fixed pick-lists used by several Finance reports and pop-ups.</summary>
public static class FinanceOptions
{
    /// <summary>Actual payment modes recorded in Update Payment Details.</summary>
    public static readonly string[] PaymentModes = ["NEFT", "RTGS", "IMPS", "Cheque", "Cash"];
    /// <summary>Planned payment modes chosen per vendor when a submission is confirmed (RTGS is the default).</summary>
    public static readonly string[] PlanModes = ["RTGS", "Physical Cheque", "E-Cheque", "Credit Card"];
    public static readonly string[] SenderBanks = ["CBI-Delhi", "CBI-Hyderabad", "SBI-Hyderabad"];
}
