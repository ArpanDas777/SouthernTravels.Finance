using SouthernTravels.Core.Models;

namespace SouthernTravels.Areas.Finance.Models.Api;

/// <summary>
/// Reply of every Finance API action (like STPLCostSheet's ApiResponseViewModel): Success/Message plus the list
/// that action fills. Field names are the expected JSON names; adjust them to match the backend.
/// </summary>
public class FinanceApiResponse : ApiResponseViewModel
{
    public List<VendorResponse> objVendorList { get; set; } = [];
    public List<TicketResponse> objTicketList { get; set; } = [];
    public List<VendorServiceResponse> objVendorServiceList { get; set; } = [];
    public List<VendorBillResponse> objVendorBillList { get; set; } = [];
    public List<VendorKycResponse> objVendorKycList { get; set; } = [];
    public List<PaymentRequestResponse> objPaymentRequestList { get; set; } = [];
    public List<AdvanceRequestResponse> objAdvanceRequestList { get; set; } = [];
    public List<RtgsAdviceResponse> objRtgsAdviceList { get; set; } = [];
    public List<VendorNotificationResponse> objVendorNotificationList { get; set; } = [];

    /// <summary>Rows saved by an insert/update action.</summary>
    public int AffectedCount { get; set; }
    /// <summary>Number of the RTGS advice created by CreateRtgsAdvice.</summary>
    public string? RtgsNo { get; set; }
    /// <summary>Number of the Bulk Payment advice created by CreateBulkPaymentAdvice.</summary>
    public string? BulkNo { get; set; }
}

public class VendorResponse
{
    public string VendorId { get; set; } = "";
    public string VendorName { get; set; } = "";
    /// <summary>Accommodation, Transport, Guide, En-Route Meals or Other.</summary>
    public string ServiceType { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    /// <summary>Registered legal name (shown under the vendor name).</summary>
    public string? LegalName { get; set; }
}

public class TicketResponse
{
    public string TicketNo { get; set; } = "";
    public string MBKID { get; set; } = "";
    public string FileNo { get; set; } = "";
    public string PaxName { get; set; } = "";
    public int NoOfPax { get; set; }
    public int NoOfChildren { get; set; }
    public string BookingExecutive { get; set; } = "";
    public string Branch { get; set; } = "";
    /// <summary>Branch code shown under the executive name (e.g. VIZ001).</summary>
    public string? BranchCode { get; set; }
    public int Discount { get; set; }
    public int BaseAmount { get; set; }
    public int GSTAmount { get; set; }
    public int GrandTotal { get; set; }
    public int PaidAmount { get; set; }
    /// <summary>Confirmed or Pending.</summary>
    public string TicketStatus { get; set; } = "";
}

public class VendorServiceResponse
{
    public string ServiceID { get; set; } = "";
    public string TicketNo { get; set; } = "";
    public string VendorId { get; set; } = "";
    public string ServiceName { get; set; } = "";
    public string? ConfirmationNo { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public int SellingPrice { get; set; }
    public int ContractedPrice { get; set; }
    public int NegotiatedPrice { get; set; }
    /// <summary>N = New, I = In Progress, C = Completed.</summary>
    public string OpsStatus { get; set; } = "N";
    /// <summary>Advance already paid outside the Advance Report.</summary>
    public int AdvancePaid { get; set; }
    /// <summary>Service type of this service when it differs from the vendor type (one vendor can supply several types).</summary>
    public string? ServiceType { get; set; }
    /// <summary>Hotel name of an accommodation service, when it differs from the vendor name.</summary>
    public string? HotelName { get; set; }
}

public class VendorBillResponse
{
    public string ServiceID { get; set; } = "";
    /// <summary>R = Raised, D = Dispute.</summary>
    public string BillStatus { get; set; } = "R";
    public string? BillDate { get; set; }
    public string BillNo { get; set; } = "";
    public string DocumentName { get; set; } = "";
    public int InvoiceAmount { get; set; }
    public int ReceivableAmount { get; set; }
    public string? UploadedOn { get; set; }
    /// <summary>A = Approved, Q = Waiting, X = Rejected, - = Not reached.</summary>
    public string L1Status { get; set; } = "-";
    public string? L1ApprovedBy { get; set; }
    public string? L1ApprovedOn { get; set; }
    public string L2Status { get; set; } = "-";
    public string? L2ApprovedBy { get; set; }
    public string? L2ApprovedOn { get; set; }
    public string? AdditionalDocumentName { get; set; }
    public string? MismatchReason { get; set; }
    /// <summary>Earlier uploads of this bill, oldest first.</summary>
    public List<VendorBillResponse> History { get; set; } = [];
}

public class VendorKycResponse
{
    public string VendorId { get; set; } = "";
    public bool HasPanCopy { get; set; }
    public bool HasGstCertificate { get; set; }
    public bool HasAddressProof { get; set; }
    public bool HasCancelledCheque { get; set; }
    public bool HasIncorporationCertificate { get; set; }
    public string ContactPerson { get; set; } = "";
    public string Mobile { get; set; } = "";
    public string Email { get; set; } = "";
    public string PAN { get; set; } = "";
    public string GSTN { get; set; } = "";
    public string BankName { get; set; } = "";
    public string AccountNo { get; set; } = "";
    public string IFSC { get; set; } = "";
    public string PaymentTerms { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string ApprovedBy { get; set; } = "";
    public string? CreatedDate { get; set; }
    public string? ContractExpiry { get; set; }
    public bool IsActive { get; set; }
    public string Address { get; set; } = "";
    public string FormNo { get; set; } = "";
}

/// <summary>A service sent to Payment Request (the payment basket) and its payment, once recorded.</summary>
public class PaymentRequestResponse
{
    public string ServiceID { get; set; } = "";
    /// <summary>RTGS, Physical Cheque, E-Cheque or Credit Card.</summary>
    public string? PlannedPaymentMode { get; set; }
    public string? RtgsNo { get; set; }
    public bool IsPaid { get; set; }
    public string? MgmtApproval { get; set; }
    public string? PaidBy { get; set; }
    public string? PaidOn { get; set; }
    public string? BankName { get; set; }
    public string? PaymentMode { get; set; }
    public string? UTRNo { get; set; }
    public string? ChequeNo { get; set; }
    public string? PaymentDate { get; set; }
    public int AmountPaid { get; set; }
    /// <summary>Physical Cheque or E-Cheque; set with the payment details (step 1).</summary>
    public string? ChequeType { get; set; }
    public string? RandomNo { get; set; }
    public decimal TdsPercent { get; set; }
    public int TdsAmount { get; set; }
    /// <summary>When the payment details were added; empty until step 1.</summary>
    public string? DetailsAddedOn { get; set; }
}

public class AdvanceRequestResponse
{
    public string ServiceID { get; set; } = "";
    public int AdvanceAmount { get; set; }
    public string? RequestedOn { get; set; }
    public string RequestedBy { get; set; } = "";
    public string Branch { get; set; } = "";
    public string L1Status { get; set; } = "-";
    public string L2Status { get; set; } = "-";
    /// <summary>When rejected: the level (L1 or L2), reason, who and when.</summary>
    public string? RejectedLevel { get; set; }
    public string? RejectReason { get; set; }
    public string? RejectedBy { get; set; }
    public string? RejectedOn { get; set; }
    /// <summary>True once the advance payment has been recorded; the fields below are then filled.</summary>
    public bool IsPaid { get; set; }
    public decimal TdsPercent { get; set; }
    public int TdsAmount { get; set; }
    public int NetAmount { get; set; }
    public string? PaymentAddedOn { get; set; }
    public string? PaymentDate { get; set; }
    public int AmountPaid { get; set; }
    /// <summary>Online or Physical Cheque.</summary>
    public string? ChequeType { get; set; }
    public string? ReferenceNo { get; set; }
    public string? BankName { get; set; }
    public string? PaymentMode { get; set; }
    /// <summary>UTR / transaction no.; the advance counts as paid only once this is set.</summary>
    public string? UTRNo { get; set; }
    public string? RandomNo { get; set; }
    public string? BulkPaymentNo { get; set; }
}

public class RtgsAdviceResponse
{
    /// <summary>RTGS advice number, or the Bulk Payment advice number when <see cref="IsBulk"/>.</summary>
    public string RtgsNo { get; set; } = "";
    /// <summary>True for a Bulk Payment advice (Advance Report); false for an RTGS advice (Payment Request).</summary>
    public bool IsBulk { get; set; }
    public string? CreatedOn { get; set; }
    public string CreatedBy { get; set; } = "";
    public string SenderBank { get; set; } = "";
    public string ChequeNo { get; set; } = "";
    public string? ChequeDate { get; set; }
    public string RandomNo { get; set; } = "";
    public List<string> ServiceIDs { get; set; } = [];
}

/// <summary>Red dots: vendors with newly submitted tickets (Payment Request) or newly added bills (Approved Vendor Payable).</summary>
public class VendorNotificationResponse
{
    public string VendorId { get; set; } = "";
    public bool HasNewTickets { get; set; }
    public bool HasNewBills { get; set; }
}
