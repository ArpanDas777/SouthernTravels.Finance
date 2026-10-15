using SouthernTravels.Core.Infrastructure;
namespace SouthernTravels.Areas.Finance.Models;

public sealed record Vendor(string Id, string Name, string Type, string City, string State);

public sealed record Ticket(
    string Id, string Mbk, string File, string Pax, int N, string Exec, string Branch,
    int Disc, int Base, int Gst, int Grand, int Paid, string Status, int Children)
{
    public int Adults => N - Children;
}

/// <summary>A vendor service booked against a ticket. Ops: N = New, I = In Progress, C = Completed.</summary>
public sealed record ServiceDef(
    string Id, int Ti, int Vi, string Name, string Conf, string Start, string End,
    int Sell, int Contr, int Neg, char Ops, string? Seed, int Adv0);

/// <summary>St: A = Approved, Q = Waiting, X = Rejected, - = Not reached.</summary>
public sealed class Approval
{
    public string St { get; set; } = "-";
    public string? By { get; set; }
    public string? At { get; set; }
}

/// <summary>St: R = Raised, D = Dispute.</summary>
public sealed class Bill
{
    public string St { get; set; } = "R";
    public string Date { get; set; } = "";
    public string No { get; set; } = "";
    public string Doc { get; set; } = "";
    public int Inv { get; set; }
    public int Rec { get; set; }
    public string Upl { get; set; } = "";
    public Approval L1 { get; set; } = new();
    public Approval L2 { get; set; } = new();
}

public sealed class BasketItem
{
    public string Sid { get; set; } = "";
    public bool Paid { get; set; }
    public string? Mgmt { get; set; }
    public string? By { get; set; }
    public string? At { get; set; }
    public string? Bank { get; set; }
    public string? Mode { get; set; }
    public string? Utr { get; set; }
    /// <summary>Planned payment mode chosen per vendor in the Payment Submission Advice (FinanceController.PlanModes). Mode is the actual mode recorded when paying.</summary>
    public string? Pm { get; set; }
    /// <summary>Number of the RTGS advice this entry was included in, once one has been created.</summary>
    public string? RtgsNo { get; set; }
    public string? ChequeNo { get; set; }
    /// <summary>Date the payment was made (entered by the user). <see cref="At"/> is the system "added" stamp.</summary>
    public string? PaymentDate { get; set; }
    public int AmountPaid { get; set; }
    /// <summary>Uploaded payment slip file name (file itself is not stored yet).</summary>
    public string? Slip { get; set; }
}

public sealed class AdvancePayment
{
    public decimal Pct { get; set; }
    public int Tds { get; set; }
    public int Net { get; set; }
    /// <summary>System "added" stamp — when the payment details were recorded.</summary>
    public string Date { get; set; } = "";
    /// <summary>Date the payment was made (entered by the user).</summary>
    public string PaymentDate { get; set; } = "";
    public int AmountPaid { get; set; }
    /// <summary>Uploaded payment slip file name (file itself is not stored yet).</summary>
    public string Slip { get; set; } = "";
    public string CType { get; set; } = "";
    public string No { get; set; } = "";
    public string Bank { get; set; } = "";
    public string Mode { get; set; } = "";
}

public sealed class AdvanceRequest
{
    public string Sid { get; set; } = "";
    public int Amt { get; set; }
    public string At { get; set; } = "";
    public string By { get; set; } = "";
    public string Br { get; set; } = "";
    public string L1 { get; set; } = "-";
    public string L2 { get; set; } = "-";
    public AdvancePayment? Pay { get; set; }
}

public sealed record Kyc(
    int Vi, bool[] Docs, string Contact, string Mobile, string Email, string Pan, string Gstn,
    string Bank, string Acc, string Ifsc, string Terms, string By, string Appr,
    string Created, string Expiry, bool Active, string Addr, string Form);

/// <summary>A service joined with its ticket, vendor, bill and payment state.</summary>
public sealed class Svc
{
    public required ServiceDef Def { get; init; }
    public required Ticket T { get; init; }
    public required Vendor V { get; init; }
    public Bill? Bill { get; init; }
    public BasketItem? Bk { get; init; }
    public int AdvPaid { get; init; }
    public bool Eligible { get; init; }

    public string Id => Def.Id;
    public int Ti => Def.Ti;
    public int Vi => Def.Vi;
    public string Name => Def.Name;
    public string Conf => Def.Conf;
    public string Start => Def.Start;
    public string End => Def.End;
    public int Sell => Def.Sell;
    public int Contr => Def.Contr;
    public int Neg => Def.Neg;
    public char Ops => Def.Ops;
    public int Adv0 => Def.Adv0;

    public int Bal => Neg - AdvPaid;
    public int Var => Neg - Contr;
    public int Profit => Sell - Contr;
    public int Dur => Fmt.Days(Start, End) + 1;
    public string Status => Conf != "" ? "Confirmed" : "Pending";
    /// <summary>Bill status: N = Not Raised, R = Raised, D = Dispute.</summary>
    public string BStat => Bill?.St ?? "N";
}

/// <summary>Details shared by every ticket in one RTGS advice.</summary>
public sealed class RtgsCommon
{
    /// <summary>HDFC, ICICI or Axis.</summary>
    public string SenderBank { get; set; } = "";
    public string ChequeNo { get; set; } = "";
    public string ChequeDate { get; set; } = "";
    public string RandomNo { get; set; } = "";
    public string? Remarks { get; set; }
}

/// <summary>An RTGS advice that has been created: the services it covers and the shared details.</summary>
public sealed class RtgsRecord
{
    public string No { get; set; } = "";
    public string At { get; set; } = "";
    public string By { get; set; } = "";
    public List<string> Sids { get; set; } = [];
    public RtgsCommon Common { get; set; } = new();
}
