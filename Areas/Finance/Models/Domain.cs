using SouthernTravels.Core.Infrastructure;
namespace SouthernTravels.Areas.Finance.Models;

public sealed record Vendor(string Id, string Name, string Type, string City, string State)
{
    /// <summary>Registered legal name; a placeholder derived from the vendor name until the Finance API supplies it.</summary>
    public string Legal { get; init; } = "";
    public string LegalName => Legal != "" ? Legal : Name + " Private Limited";
}

public sealed record Ticket(
    string Id, string Mbk, string File, string Pax, int N, string Exec, string Branch,
    int Disc, int Base, int Gst, int Grand, int Paid, string Status, int Children)
{
    public int Adults => N - Children;
    /// <summary>Branch code of the booking executive (shown under the name; blank until the API supplies it).</summary>
    public string BranchCode { get; init; } = "";
    /// <summary>"5 pax · 4A + 1C".</summary>
    public string PaxInfo => N + " pax · " + Adults + "A" + (Children > 0 ? " + " + Children + "C" : "");
}

/// <summary>
/// A vendor service booked against a ticket. Ops: N = New, I = In Progress, C = Completed.
/// SType overrides the vendor's service type for this service (one vendor can supply several types); Hotel is the hotel name when it differs from the vendor's.
/// </summary>
public sealed record ServiceDef(
    string Id, int Ti, int Vi, string Name, string Conf, string Start, string End,
    int Sell, int Contr, int Neg, char Ops, string? Seed, int Adv0, string? SType = null, string? Hotel = null);

/// <summary>St: A = Approved, Q = Waiting, X = Rejected, - = Not reached.</summary>
public sealed class Approval
{
    public string St { get; set; } = "-";
    public string? By { get; set; }
    public string? At { get; set; }
    /// <summary>Rejected or Disputed, when St is X.</summary>
    public string? Kind { get; set; }
    public string? Reason { get; set; }
}

/// <summary>St: R = Raised, D = Dispute.</summary>
public sealed class Bill
{
    public string St { get; set; } = "R";
    public string Date { get; set; } = "";
    public string No { get; set; } = "";
    public string Doc { get; set; } = "";
    /// <summary>Uploaded voucher file name (only the name is stored yet).</summary>
    public string Voucher { get; set; } = "";
    /// <summary>Optional additional document (file name only).</summary>
    public string Extra { get; set; } = "";
    public int Inv { get; set; }
    public int Rec { get; set; }
    /// <summary>Mismatch reason, given when the invoice amount is above the vendor payable.</summary>
    public string Mr { get; set; } = "";
    public string Upl { get; set; } = "";
    public Approval L1 { get; set; } = new();
    public Approval L2 { get; set; } = new();
    /// <summary>Earlier uploads of this bill (newest last); each reupload appends the version it replaces.</summary>
    public List<Bill> Hist { get; set; } = [];
    public bool Approved => L1.St == "A" && L2.St == "A";
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
    /// <summary>Cheque type chosen when the payment details were added (Physical Cheque / E-Cheque).</summary>
    public string? CType { get; set; }
    /// <summary>Random no. of the RTGS advice the details came from.</summary>
    public string? Rnd { get; set; }
    public decimal Pct { get; set; }
    public int Tds { get; set; }
    /// <summary>System stamp of when the payment details were added (step 1); null until then. The entry is Paid once the UTR is in (step 2).</summary>
    public string? DetailsAt { get; set; }
    public bool HasDetails => DetailsAt != null;
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
    public string CType { get; set; } = "";
    public string No { get; set; } = "";
    public string Bank { get; set; } = "";
    public string Mode { get; set; } = "";
    /// <summary>UTR / transaction no.; the advance counts as paid only once this is set (step 2 of the payment).</summary>
    public string Utr { get; set; } = "";
    /// <summary>Random no. of a Bulk Payment advice.</summary>
    public string Rnd { get; set; } = "";
    /// <summary>Number of the Bulk Payment advice that covers this advance, when there is one.</summary>
    public string? BulkNo { get; set; }
    public bool Paid => Utr != "";

    /// <summary>Works out TDS (from <see cref="Pct"/>) and the net payable for an advance of <paramref name="advanceAmount"/>.</summary>
    public void Settle(int advanceAmount)
    {
        Tds = Fmt.Round((double)(advanceAmount * Pct / 100));
        Net = advanceAmount - Tds;
    }
}

/// <summary>Why and by whom an advance request was rejected: Lvl is L1 or L2.</summary>
public sealed record AdvRej(string Lvl, string? Reason, string By, string At);

public sealed class AdvanceRequest
{
    public string Sid { get; set; } = "";
    public int Amt { get; set; }
    public string At { get; set; } = "";
    public string By { get; set; } = "";
    public string Br { get; set; } = "";
    public string L1 { get; set; } = "-";
    public string L2 { get; set; } = "-";
    /// <summary>Payment details; set by "Add Payment Details" (no UTR yet). The advance is paid only once <see cref="AdvancePayment.Utr"/> is set.</summary>
    public AdvancePayment? Pay { get; set; }
    /// <summary>Who made the L2 decision and when (blank for the original sample data).</summary>
    public string? L2By { get; set; }
    public string? L2At { get; set; }
    /// <summary>Set when the request was rejected; blank rejections show "Reason: -".</summary>
    public AdvRej? Rej { get; set; }
    public bool Paid => Pay is { Paid: true };
    public bool Approved => L1 == "A" && L2 == "A";
}

/// <summary>
/// One vendor KYC document as the approver sees it. St: V = verified by the vendor through the API / OTP (System Approved), A = approved by the approver,
/// P = pending, R = rejected, N = not uploaded.
/// </summary>
public sealed class KycDoc
{
    public string St { get; set; } = "N";
    public string? By { get; set; }
    public string? At { get; set; }
    public string? Reason { get; set; }
    public bool Ok => St is "V" or "A";
}

/// <summary>The seven mandatory vendor documents: key, name and (when the vendor can verify it online) how.</summary>
public static class KycDocs
{
    public static readonly (string Key, string Name, string? Via)[] All =
    [
        ("pan", "PAN", "PAN verification API"), ("gst", "GST", null), ("adr", "Aadhaar", "Aadhaar OTP"), ("chq", "Cancelled Cheque", null),
        ("inc", "Incorporation", null), ("con", "Contract", null), ("bnk", "Bank", "Bank OTP / penny-drop"),
    ];
}

public sealed record Kyc(
    int Vi, bool[] Docs, string Contact, string Mobile, string Email, string Pan, string Gstn,
    string Bank, string Acc, string Ifsc, string Terms, string By, string Appr,
    string Created, string Expiry, bool Active, string Addr, string Form)
{
    /// <summary>Approver view: the state of each of the seven documents, in <see cref="KycDocs.All"/> order.</summary>
    public List<KycDoc> Ap { get; init; } = [];
}

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
    /// <summary>Advances paid so far: the amount recorded on the service plus each advance with a UTR.</summary>
    public int AdvCount { get; init; }

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
    public int Profit => Sell - Contr;
    public int Dur => Fmt.Days(Start, End) + 1;
    public string Status => Conf != "" ? "Confirmed" : "Pending";
    /// <summary>Bill status: N = Pending (no bill, or not yet approved by L1 and L2), R = Uploaded (approved), D = Disputed.</summary>
    public string BStat => Bill is null ? "N" : Bill.St == "D" ? "D" : Bill.Approved ? "R" : "N";

    /* ---------- accommodation details (placeholders until the API supplies them) ---------- */

    public bool IsAcc => V.Type == "Accommodation";
    public string Hotel => Def.Hotel ?? V.Name;
    public int Nights => Math.Max(1, Fmt.Days(Start, End));
    static readonly string[] Cats = ["Standard", "Deluxe", "Premium", "Executive"];
    public string RoomCat
    {
        get
        {
            foreach (var c in new[] { "Suite", "Villa", "Palace", "Premium", "Executive", "Deluxe", "Superior", "Haveli", "Resort" })
                if (Name.Contains(c, StringComparison.OrdinalIgnoreCase)) return c is "Superior" or "Haveli" or "Resort" ? "Standard" : c;
            return Cats[(Vi + Ti) % Cats.Length];
        }
    }
    static readonly (string Code, string Full)[] Meals =
        [("CP", "Continental Plan (room + breakfast)"), ("MAP", "Modified American Plan (breakfast + dinner)"), ("AP", "American Plan (all meals)"),
         ("EP", "European Plan (room only)"), ("CPAI", "Continental Plan, all inclusive")];
    public string Meal => Meals[(Vi + Ti) % 5].Code;
    public string MealFull => Meals[(Vi + Ti) % 5].Full;
    /// <summary>"Dbl ×2 · Sin ×1 · e.bed ×1 · CWOB ×1", derived from the ticket's pax count.</summary>
    public string Occ
    {
        get
        {
            int n = T.N, dbl = n / 2, sin = n % 2;
            var p = new List<string>();
            if (dbl > 0) p.Add("Dbl ×" + dbl);
            if (sin > 0) p.Add("Sin ×" + sin);
            if (n >= 6) p.Add("e.bed ×1");
            if (n >= 4) p.Add("CWOB ×1");
            return string.Join(" · ", p);
        }
    }
    /// <summary>"3 days" / "1 day".</summary>
    public string DurText => Dur + (Dur == 1 ? " day" : " days");
    /// <summary>Voucher number, placeholder until vouchers come from the API.</summary>
    public string Voucher => "VCH" + (51000 + (int.TryParse(Id.TrimStart('S'), out var n) ? n : Math.Abs(Id.GetHashCode() % 1000)));
}

/// <summary>Details shared by every ticket in one RTGS advice.</summary>
public sealed class RtgsCommon
{
    /// <summary>The sender account chosen in Create RTGS Advice (CBI-Delhi, CBI-Hyderabad or SBI-Hyderabad).</summary>
    public string SenderBank { get; set; } = "";
    public string ChequeNo { get; set; } = "";
    public string ChequeDate { get; set; } = "";
    public string RandomNo { get; set; } = "";
    /// <summary>Payment mode (RTGS, NEFT or IMPS) of a Bulk Payment advice.</summary>
    public string Mode { get; set; } = "RTGS";
    /// <summary>Physical Cheque or E-Cheque.</summary>
    public string CType { get; set; } = "Physical Cheque";
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

