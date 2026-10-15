using SouthernTravels.Core.Models;
using SouthernTravels.Areas.Finance.Data;
using SouthernTravels.Core.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace SouthernTravels.Areas.Finance.Models;

/* ---------- popup view models ---------- */

public sealed record RaiseVm(List<Svc> Services, bool Reupload, List<Svc> History);
public sealed record VendorBillsVm(Vendor Vendor, List<Svc> Services);
public sealed record AdviceRow(int N, string Name, string Id, int Vi, int Tickets, string Amt);
public sealed record AdviceVm(List<string> Ids, string No, string Issued, List<AdviceRow> Rows, string Summary, string Total, List<string> Pending, int ServiceCount, int VendorCount);
public sealed record AdvPayVm(AdvanceRequest Req, Svc Service, bool ViewOnly);
public sealed record PrPayVm(Svc Service, string PaidBy);
public sealed record DocVm(Vendor Vendor, string DocName, string File);

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


/* ---------- RTGS advice and payment advice ---------- */

/// <summary>Create RTGS Advice (step 1): the unpaid RTGS services to choose from.</summary>
public sealed record RtgsSelVm(List<Svc> Pool, string[] Banks);
/// <summary>RTGS Advice generated: the two documents (Internal .xlsx and Bank .csv).</summary>
public sealed record RtgsOutVm(RtgsRecord Rec, int Count, int Vendors, int Total);
public sealed record RtgsHistRow(RtgsRecord Rec, int Count, int Vendors, int Total, int Pending);
public sealed record RtgsPayVm(RtgsRecord Rec, int Pending, int Total, string PaidBy);
public sealed record RtgsHistVm(List<RtgsHistRow> Rows, bool CanPay);

/// <summary>One line of an RTGS letter: a single service paid to a beneficiary.</summary>
public sealed record RtgsLetterRow(int N, string SenderAc, string ChequeNo, string RandomNo, string ChequeDate, string BenAc, string Ifsc, int Amount, string BenName, string Remarks);

/// <summary>An RTGS letter. Internal adds a total row; Bank has none. <paramref name="FromHistory"/> shows "Back to History".</summary>
public sealed record RtgsLetterVm(RtgsRecord Rec, bool Bank, bool FromHistory, string Date, List<RtgsLetterRow> Rows)
{
    public int Total => Rows.Sum(r => r.Amount);
    public int Vendors => Rows.Select(r => r.BenName).Distinct().Count();
    public string Kind => Bank ? "bank" : "internal";
    public DocHead Head => new(Bank ? "RTGS ADVICE — BANK" : "RTGS ADVICE — INTERNAL",
        [("Advice No", Rec.No), ("Date", Date)], Fmt.DmyTime(Rec.At));
}

/// <summary>Payment Advice for one ticket line (a service in the payment basket), addressed to the vendor.</summary>
public sealed record PrAdviceVm(Svc S, BasketItem B, string AdviceNo, string Date)
{
    public DocHead Head => new("PAYMENT ADVICE", [("Advice No", AdviceNo), ("Date", Date), ("Ticket", S.T.Id)], Fmt.DmyTime(Clock.NowStamp()));
}

