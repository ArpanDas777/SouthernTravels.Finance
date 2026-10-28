using SouthernTravels.Core.Models;
using SouthernTravels.Areas.Finance.Data;
using SouthernTravels.Core.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace SouthernTravels.Areas.Finance.Models;

/* ---------- popup view models ---------- */

public sealed record RaiseVm(List<Svc> Services, bool Reupload);
public sealed record VendorBillsVm(Vendor Vendor, List<Svc> Services);
public sealed record AdviceRow(int N, string Name, string Id, int Vi, int Tickets, string Amt, string Legal = "");
public sealed record AdviceVm(List<string> Ids, string No, string Issued, List<AdviceRow> Rows, string Summary, string Total, int ServiceCount, int VendorCount);
/// <summary>Add / Update / View of an advance payment. Earlier = the advances already paid (with a UTR) before this one.</summary>
public sealed record AdvPayVm(AdvanceRequest Req, Svc Service, bool ViewOnly, int EarlierCount, int EarlierAmt);
/// <summary>Bulk Payment pop-up: the advances to pay together.</summary>
public sealed record BulkPayVm(List<AdvRowVm> Rows);
public sealed record AdvRowVm(AdvanceRequest Req, Svc Service);
/// <summary>Bulk Payment History: each advice with its advances, per-advance payment state.</summary>
public sealed record BulkHistRow(RtgsRecord Rec, List<AdvRowVm> Items, int Total, int Pending);
public sealed record BulkHistVm(List<BulkHistRow> Rows);
public sealed record BulkUtrVm(RtgsRecord Rec, List<AdvRowVm> Items);
/// <summary>Add / Update / View of a Payment Request payment (two steps: details, then the UTR).</summary>
public sealed record PrPayVm(Svc Service, string PaidBy, bool ViewOnly = false);
public sealed record PrItemVm(BasketItem B, Svc S);
/// <summary>Update Details of an RTGS advice: a UTR per entry.</summary>
public sealed record RtgsUtrVm(RtgsRecord Rec, List<PrItemVm> Items);
public sealed record DocVm(Vendor Vendor, string DocName, string File);
public sealed record KycRejectVm(int Vi, Vendor Vendor, string Key, string DocName);

/// <summary>One payment made to the vendor for the ticket (an advance, an earlier advance, or a payment-request payment); Share is its % of the selling rate and Balance what is still payable after it.</summary>
public sealed record PayTxn(string Date, string Type, string Service, string Ref, string Mode, int Paid, string Share, int Balance);

/// <summary>Printable A4 advance advice for one approved advance request.</summary>
public sealed record AdvanceAdviceVm(
    string AdviceNo, string RequestNo, string FileNo, string Date, string Generated,
    Svc Service, Kyc Kyc, AdvanceRequest Req, List<PayTxn> Txns, int Payable, int SellTotal)
{
    public string VendorAddress => Kyc.Addr + ", " + Service.V.State;
    public int TotalPaid => Txns.Sum(t => t.Paid);
    public int Balance => Payable - TotalPaid;
    /// <summary>Paid before this advance, and what is still payable once this advance is paid (the same as Balance when it is already paid).</summary>
    public int PaidBefore => TotalPaid - (Pay != null ? Req.Amt : 0);
    public int BalanceAfter => Payable - PaidBefore - Req.Amt;
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
    /// <summary>Optional additional document.</summary>
    public IFormFile? Extra { get; set; }
    public decimal? Inv { get; set; }
    public string? Mr { get; set; }
}

public sealed class RaiseForm
{
    public List<RaiseItem> Items { get; set; } = [];
    /// <summary>R = submit bill, D = raise dispute.</summary>
    public string Kind { get; set; } = "R";
    /// <summary>1 when this is a Reupload.</summary>
    public int Reupload { get; set; }
    public string? ReturnUrl { get; set; }
}


/* ---------- RTGS advice and payment advice ---------- */

/// <summary>Create RTGS Advice (step 1): the unpaid RTGS services to choose from.</summary>
public sealed record RtgsSelVm(List<Svc> Pool);
/// <summary>RTGS Advice generated (or Bulk Payment advice generated): the two documents. <paramref name="Bulk"/> switches the names and the URLs.</summary>
public sealed record RtgsOutVm(RtgsRecord Rec, int Count, int Vendors, int Total, bool Bulk = false);
public sealed record RtgsHistRow(RtgsRecord Rec, int Count, int Vendors, int Total, int Pending);
public sealed record RtgsHistVm(List<RtgsHistRow> Rows);

/// <summary>One line of an RTGS letter: a single service paid to a beneficiary.</summary>
public sealed record RtgsLetterRow(int N, string SenderAc, string ChequeNo, string RandomNo, string ChequeDate, string BenAc, string Ifsc, int Amount, string BenName, string Remarks);

/// <summary>Both letters of one advice, printed together.</summary>
public sealed record RtgsBothVm(RtgsLetterVm Internal, RtgsLetterVm Bank);

/// <summary>
/// An RTGS letter (or, with <paramref name="Bulk"/>, a Bulk Payment letter — the same layout under another name).
/// Internal adds a total row; Bank has none. <paramref name="FromHistory"/> shows "Back to History".
/// </summary>
public sealed record RtgsLetterVm(RtgsRecord Rec, bool Bank, bool FromHistory, string Date, List<RtgsLetterRow> Rows, bool Bulk = false)
{
    public int Total => Rows.Sum(r => r.Amount);
    public int Vendors => Rows.Select(r => r.BenName).Distinct().Count();
    public string Kind => Bank ? "bank" : "internal";
    /// <summary>"RTGS" or "Bulk Payment".</summary>
    public string Name => Bulk ? "Bulk Payment" : "RTGS";
    /// <summary>Printable letter page, pop-up and history for this kind of advice (append "&amp;kind=…").</summary>
    public string PdfUrl => Bulk ? "/Finance/Advance/BulkPdf?no=" : "/Finance/PaymentRequest/RtgsPdf?no=";
    public string ViewUrl => Bulk ? "/Finance/Advance/Popup?t=bulkView&no=" : "/Finance/PaymentRequest/Popup?t=rtgsView&no=";
    public string HistUrl => Bulk ? "/Finance/Advance/Popup?t=bulkHist" : "/Finance/PaymentRequest/Popup?t=rtgsHist";
    public DocHead Head => new(Name.ToUpperInvariant() + " ADVICE — " + (Bank ? "BANK" : "INTERNAL"),
        [("Advice No", Rec.No), ("Date", Date)], Fmt.DmyTime(Rec.At));
}

/// <summary>Payment Advice for one ticket line (a service in the payment basket), addressed to the vendor.</summary>
public sealed record PrAdviceVm(Svc S, BasketItem B, string AdviceNo, string Date)
{
    public DocHead Head => new("PAYMENT ADVICE", [("Advice No", AdviceNo), ("Date", Date), ("Ticket", S.T.Id)], Fmt.DmyTime(Clock.NowStamp()));
}


/// <summary>
/// The payment-detail fields shared by the Advance, Bulk Payment, Payment Request and Create RTGS Advice pop-ups. <paramref name="ViewOnly"/> shows them read-only;
/// <paramref name="AskUtr"/> adds the mandatory UTR (step 2) and <paramref name="AskRnd"/> the Random No. (advice pop-ups).
/// </summary>
public sealed record PayFieldsVm(string DateLabel, bool ViewOnly = false, string? Date = null, string? CType = null, string? Mode = null, string? Bank = null,
    string? No = null, string? Utr = null, bool AskUtr = false, bool AskRnd = false, string? DefaultMode = null);

/// <summary>One ticket of a TDS table (Bulk Payment / Create RTGS Advice): the posted key is tds[Sid].</summary>
public sealed record TdsRow(string Sid, Svc S, int Amount);
public sealed record TdsTableVm(string AmountLabel, List<TdsRow> Rows);

/// <summary>One line of an Update UTR table (Bulk Payment / RTGS advice).</summary>
public sealed record UtrRow(string Sid, Svc S, int Net, bool Paid, string? Utr);
public sealed record UtrTableVm(string No, List<UtrRow> Rows);

/// <summary>The required Reason box shared by the reject pop-ups (bill, advance, KYC document); <paramref name="Hidden"/> are the posted ids.</summary>
public sealed record ReasonFieldVm(string Placeholder, (string Name, string Value)[] Hidden);
