using System.Globalization;
using SouthernTravels.Areas.Finance.Data;
using SouthernTravels.Areas.Finance.Models;
using SouthernTravels.Areas.Finance.Models.Api;
using SouthernTravels.Core.Infrastructure;

namespace SouthernTravels.Areas.Finance.Services;

/// <summary>
/// Turns the Finance API lists into the <see cref="FinanceStore"/> shape the reports already use:
/// vendor/ticket ids become list indexes, KYC is aligned with the vendor list, and dates become "yyyy-MM-dd" / "yyyy-MM-dd HH:mm".
/// </summary>
public static class FinanceApiMapper
{
    static readonly string[] DateFormats =
        ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.FFFFFFF", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "dd-MM-yyyy", "dd/MM/yyyy", "dd-MMM-yyyy", "MM/dd/yyyy HH:mm:ss", "MM/dd/yyyy"];

    static DateTime? Parse(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null
        : DateTime.TryParseExact(s.Trim(), DateFormats, Fmt.Inv, DateTimeStyles.AllowWhiteSpaces, out var d) ? d
        : DateTime.TryParse(s, Fmt.Inv, DateTimeStyles.AllowWhiteSpaces, out d) ? d : null;

    /// <summary>"yyyy-MM-dd"; <paramref name="fallback"/> when the API sent nothing usable (the reports need a real date).</summary>
    public static string Day(string? s, string? fallback = null) => Parse(s) is { } d ? Fmt.Ymd(d) : fallback ?? Clock.Today;

    /// <summary>"yyyy-MM-dd HH:mm" (system stamps).</summary>
    public static string Stamp(string? s, string? fallback = null) =>
        Parse(s) is { } d ? d.ToString("yyyy-MM-dd HH:mm", Fmt.Inv) : fallback ?? Clock.Today + " 00:00";

    static string? OptStamp(string? s) => Parse(s) is { } d ? d.ToString("yyyy-MM-dd HH:mm", Fmt.Inv) : null;

    static string Code(string? s, string allowed, string fallback) =>
        s?.Trim() is { Length: > 0 } c && allowed.Contains(char.ToUpperInvariant(c[0])) ? char.ToUpperInvariant(c[0]).ToString() : fallback;

    public static FinanceStore ToStore(
        List<VendorResponse> vendors, List<TicketResponse> tickets, List<VendorServiceResponse> services, List<VendorBillResponse> bills,
        List<VendorKycResponse> kycs, List<PaymentRequestResponse> payments, List<AdvanceRequestResponse> advances,
        List<RtgsAdviceResponse> advices, List<VendorNotificationResponse> flags)
    {
        var v = vendors.Select(x => new Vendor(x.VendorId, x.VendorName, x.ServiceType, x.City, x.State) { Legal = x.LegalName ?? "" }).ToList();
        var vIndex = v.Select((x, i) => (x.Id, i)).GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First().i);

        var t = tickets.Select(x => new Ticket(x.TicketNo, x.MBKID, x.FileNo, x.PaxName, x.NoOfPax, x.BookingExecutive, x.Branch,
            x.Discount, x.BaseAmount, x.GSTAmount, x.GrandTotal, x.PaidAmount, string.IsNullOrWhiteSpace(x.TicketStatus) ? "Pending" : x.TicketStatus, x.NoOfChildren) { BranchCode = x.BranchCode ?? "" }).ToList();
        var tIndex = t.Select((x, i) => (x.Id, i)).GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First().i);

        // A service whose ticket or vendor is not in the lists cannot be shown, so it is skipped.
        var s = services.Where(x => tIndex.ContainsKey(x.TicketNo) && vIndex.ContainsKey(x.VendorId)).Select(x =>
        {
            var start = Day(x.StartDate);
            return new ServiceDef(x.ServiceID, tIndex[x.TicketNo], vIndex[x.VendorId], x.ServiceName, x.ConfirmationNo ?? "",
                start, Day(x.EndDate, start), x.SellingPrice, x.ContractedPrice, x.NegotiatedPrice, Code(x.OpsStatus, "NIC", "N")[0], null, x.AdvancePaid, x.ServiceType, x.HotelName);
        }).ToList();
        var sIds = s.Select(x => x.Id).ToHashSet();

        static Bill MapBill(VendorBillResponse x) => new()
        {
            St = Code(x.BillStatus, "RD", "R"), Date = Day(x.BillDate), No = x.BillNo, Doc = x.DocumentName, Extra = x.AdditionalDocumentName ?? "",
            Inv = x.InvoiceAmount, Rec = x.ReceivableAmount, Mr = x.MismatchReason ?? "", Upl = Stamp(x.UploadedOn),
            L1 = new Approval { St = Code(x.L1Status, "AQX-", "-"), By = x.L1ApprovedBy, At = OptStamp(x.L1ApprovedOn) },
            L2 = new Approval { St = Code(x.L2Status, "AQX-", "-"), By = x.L2ApprovedBy, At = OptStamp(x.L2ApprovedOn) },
            Hist = x.History.Select(MapBill).ToList(),
        };
        var b = bills.Where(x => sIds.Contains(x.ServiceID)).GroupBy(x => x.ServiceID).ToDictionary(g => g.Key, g => MapBill(g.Last()));

        // K[i] must be V[i]'s KYC, so vendors without a KYC record get an empty one.
        var kycByVendor = kycs.GroupBy(x => x.VendorId).ToDictionary(g => g.Key, g => g.Last());
        var k = v.Select((vendor, i) => kycByVendor.TryGetValue(vendor.Id, out var x)
            ? new Kyc(i, [x.HasPanCopy, x.HasGstCertificate, x.HasAddressProof, x.HasCancelledCheque, x.HasIncorporationCertificate],
                x.ContactPerson, x.Mobile, x.Email, x.PAN, x.GSTN, x.BankName, x.AccountNo, x.IFSC, x.PaymentTerms, x.CreatedBy, x.ApprovedBy,
                Day(x.CreatedDate), Day(x.ContractExpiry), x.IsActive, x.Address, x.FormNo)
            : new Kyc(i, new bool[5], "", "", "", "", "", "", "", "", "", "", "", Clock.Today, Clock.Today, false, "", "")).ToList();

        var basket = payments.Where(x => sIds.Contains(x.ServiceID)).Select(x => new BasketItem
        {
            Sid = x.ServiceID, Pm = x.PlannedPaymentMode, RtgsNo = x.RtgsNo, Paid = x.IsPaid, Mgmt = x.MgmtApproval, By = x.PaidBy,
            At = OptStamp(x.PaidOn), Bank = x.BankName, Mode = x.PaymentMode, Utr = x.UTRNo, ChequeNo = x.ChequeNo,
            PaymentDate = string.IsNullOrWhiteSpace(x.PaymentDate) ? null : Day(x.PaymentDate), AmountPaid = x.AmountPaid,
            CType = x.ChequeType, Rnd = x.RandomNo, Pct = x.TdsPercent, Tds = x.TdsAmount, DetailsAt = OptStamp(x.DetailsAddedOn),
        }).ToList();

        var adv = advances.Where(x => sIds.Contains(x.ServiceID)).Select(x => new AdvanceRequest
        {
            Sid = x.ServiceID, Amt = x.AdvanceAmount, At = Stamp(x.RequestedOn), By = x.RequestedBy, Br = x.Branch,
            L1 = Code(x.L1Status, "AQX-", "-"), L2 = Code(x.L2Status, "AQX-", "-"),
            Rej = string.IsNullOrWhiteSpace(x.RejectedLevel) ? null : new AdvRej(x.RejectedLevel, x.RejectReason, x.RejectedBy ?? "", Stamp(x.RejectedOn)),
            Pay = !x.IsPaid ? null : new AdvancePayment
            {
                Pct = x.TdsPercent, Tds = x.TdsAmount, Net = x.NetAmount, Date = Stamp(x.PaymentAddedOn), PaymentDate = Day(x.PaymentDate),
                AmountPaid = x.AmountPaid, CType = x.ChequeType ?? "", No = x.ReferenceNo ?? "",
                Bank = x.BankName ?? "", Mode = x.PaymentMode ?? "", Utr = x.UTRNo ?? "", Rnd = x.RandomNo ?? "", BulkNo = x.BulkPaymentNo,
            },
        }).ToList();

        // RTGS and Bulk Payment advices share one list (IsBulk tells them apart) and one shape.
        RtgsRecord ToRecord(RtgsAdviceResponse x) => new()
        {
            No = x.RtgsNo, At = Stamp(x.CreatedOn), By = x.CreatedBy, Sids = x.ServiceIDs.Where(sIds.Contains).ToList(),
            Common = new RtgsCommon { SenderBank = x.SenderBank, ChequeNo = x.ChequeNo, ChequeDate = Day(x.ChequeDate), RandomNo = x.RandomNo, Remarks = x.Remarks },
        };
        var ordered = advices.OrderBy(x => Parse(x.CreatedOn)).ToList();
        var rtgsLog = ordered.Where(x => !x.IsBulk).Select(ToRecord).ToList();
        var bulkLog = ordered.Where(x => x.IsBulk).Select(ToRecord).ToList();

        return new FinanceStore(v, t, s, k, b, basket, adv, rtgsLog, bulkLog,
            flags.Where(f => f.HasNewTickets).Select(f => f.VendorId), flags.Where(f => f.HasNewBills).Select(f => f.VendorId));
    }
}
