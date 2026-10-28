namespace SouthernTravels.Areas.Finance.Models;

/// <summary>Fixed pick-lists used by several Finance reports and pop-ups.</summary>
public static class FinanceOptions
{
    /// <summary>Planned payment modes chosen per vendor when a submission is confirmed (RTGS is the default).</summary>
    public static readonly string[] PlanModes = ["RTGS", "Physical Cheque", "E-Cheque", "Credit Card"];
    public static readonly string[] SenderBanks = ["CBI-Delhi", "CBI-Hyderabad", "SBI-Hyderabad"];
    /// <summary>Cheque types of an advance payment.</summary>
    public static readonly string[] ChequeTypes = ["Physical Cheque", "E-Cheque"];
    /// <summary>Payment modes of an advance payment.</summary>
    public static readonly string[] AdvanceModes = ["RTGS", "NEFT", "IMPS"];

    /// <summary>
    /// Documents to upload with a bill, by service type (placeholder lists until the real ones are supplied). The first entry is the main bill;
    /// the rest are the optional additional documents.
    /// </summary>
    public static string[] DocTypes(string type) => type switch
    {
        "Accommodation" => ["Hotel Invoice", "Guest Folio"],
        "Transport" => ["Transport Invoice", "Trip Sheet", "Toll & Parking Receipts"],
        "Guide" => ["Guide Invoice", "Guide Service Report"],
        "En-Route Meals" => ["Meals Invoice", "Meal Voucher / Pax Count Sheet"],
        _ => ["Invoice", "Supporting Document"],
    };
}
