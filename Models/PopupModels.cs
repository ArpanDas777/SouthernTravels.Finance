using Microsoft.AspNetCore.Http;

namespace SouthernTravels.Finance.Models;

/* ---------- popup view models ---------- */

public sealed record RaiseVm(List<Svc> Services, bool Reupload);
public sealed record VendorBillsVm(Vendor Vendor, List<Svc> Services);
public sealed record AdviceRow(int N, string Name, string Id, int Tickets, string Amt);
public sealed record AdviceVm(List<string> Ids, string No, string Issued, List<AdviceRow> Rows, string Summary, string Total, List<string> Pending, int ServiceCount, int VendorCount);
public sealed record AdvPayVm(AdvanceRequest Req, Svc Service, bool ViewOnly);
public sealed record PrPayVm(Svc Service, string PaidBy);
public sealed record DocVm(Vendor Vendor, string DocName, string File);

/// <summary>A label/value grid inside a popup ("facts" block).</summary>
public sealed class FactsBlock
{
    public int Cols { get; init; } = 2;
    public bool Strip { get; init; }
    public bool Warn { get; init; }
    public string? Title { get; init; }
    public List<(string L, string V, string? Color, bool Bold)> Items { get; init; } = [];
}

/// <summary>Printable A4 advance advice for one approved advance request.</summary>
public sealed record AdvanceAdviceVm(
    string AdviceNo, string RequestNo, string FileNo, string Date, string Generated,
    Svc Service, Kyc Kyc, AdvanceRequest Req)
{
    public string VendorAddress => Kyc.Addr + ", " + Service.V.State;
    public int ServiceCost => Service.Neg;
    public int Profit => Service.Sell - Service.Neg;
    public AdvancePayment? Pay => Req.Pay;
    /// <summary>File-name friendly advice number, used as the PDF's default name.</summary>
    public string FileName => "Advance Advice " + AdviceNo.Replace('/', '-');
}

/// <summary>Printable A4 payment advice for one vendor: the unpaid tickets from its Payment Request drill-down.</summary>
public sealed record PaymentAdviceVm(
    string AdviceNo, string Date, string Generated, Vendor Vendor, Kyc Kyc, List<Svc> Items)
{
    public string VendorAddress => Kyc.Addr + ", " + Vendor.State;
    /// <summary>Vendor payable = the bill's recommended amount (as in the Payment Request drill-down).</summary>
    public int Total => Items.Sum(s => s.Bill?.Rec ?? 0);
    public int TotalSelling => Items.Sum(s => s.Sell);
    public int Tickets => Items.Select(s => s.Ti).Distinct().Count();
    public string FileName => "Payment Advice " + AdviceNo.Replace('/', '-') + " " + Vendor.Id + (Tickets == 1 ? " " + Items[0].T.Id : "");
}

/* ---------- posted forms ---------- */

public sealed class RaiseItem
{
    public string Sid { get; set; } = "";
    public string? Date { get; set; }
    public string? No { get; set; }
    public IFormFile? File { get; set; }
    public decimal? Inv { get; set; }
    public decimal? Rec { get; set; }
}

public sealed class RaiseForm
{
    public List<RaiseItem> Items { get; set; } = [];
    /// <summary>R = submit bill, D = raise dispute.</summary>
    public string Kind { get; set; } = "R";
    public string? ReturnUrl { get; set; }
}
