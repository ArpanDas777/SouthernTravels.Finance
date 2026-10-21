using SouthernTravels.Core.Controllers;
using SouthernTravels.Core.Models;

namespace SouthernTravels.Areas.Finance;

/// <summary>How the Finance area presents itself in the shared shell: heading, side menu (one entry per report) and home page.</summary>
public static class FinanceArea
{
    public const string Name = "Finance", Home = "/Finance/VendorPayable";

    public static readonly AreaInfo Info = new(Name, "Fin", Home,
    [
        new("VendorPayable", "Vendor Payable Report", "M12 2v20M17 6H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6", "/Finance/VendorPayable"),
        new("Advance", "Advance Report", "M12 7v5l3 2M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z", "/Finance/Advance"),
        new("ApprovedVendorPayable", "Fund Management (Approved Vendor Payable)", "M20 6 9 17l-5-5", "/Finance/ApprovedVendorPayable"),
        new("PaymentRequest", "Payment Request Report", "M22 2 11 13M22 2l-7 20-4-9-9-4z", "/Finance/PaymentRequest"),
        new("VendorKyc", "Vendor KYC Report", "M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM23 21v-2a4 4 0 0 0-3-3.9M16 3.1a4 4 0 0 1 0 7.8", "/Finance/VendorKyc"),
    ], SeenUrl: "/Finance/Seen");
}
