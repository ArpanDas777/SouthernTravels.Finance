namespace SouthernTravels.Areas.Finance.Models.Api;

/// <summary>
/// Fields sent with every Finance API call, as STPLCostSheet does (tenantID, userId, userName, securityKey).
/// FinanceApiService fills them from the session.
/// </summary>
public abstract class FinanceApiRequest
{
    public int TenantID { get; set; }
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string SecurityKey { get; set; } = "";
}

/// <summary>Request for the report-data lists. Dates are optional (yyyy-MM-dd); empty means everything.</summary>
public class FinanceListRequest : FinanceApiRequest
{
    public string FromDate { get; set; } = "";
    public string ToDate { get; set; } = "";
}

public class RaiseVendorBillRequest : FinanceApiRequest
{
    public List<RaiseVendorBillItem> Bills { get; set; } = [];
}

public class RaiseVendorBillItem
{
    public string ServiceID { get; set; } = "";
    /// <summary>R = Raised, D = Dispute.</summary>
    public string BillStatus { get; set; } = "R";
    public string BillDate { get; set; } = "";
    public string BillNo { get; set; } = "";
    public string DocumentName { get; set; } = "";
    public int InvoiceAmount { get; set; }
    public int ReceivableAmount { get; set; }
    /// <summary>Optional additional document (file name).</summary>
    public string? AdditionalDocumentName { get; set; }
    /// <summary>Given when the invoice amount is above the vendor payable.</summary>
    public string? MismatchReason { get; set; }
}

public class SubmitToPaymentRequestRequest : FinanceApiRequest
{
    public List<PaymentSubmitItem> Items { get; set; } = [];
}

/// <summary>One service sent to Payment Request with the planned payment mode chosen for its vendor.</summary>
public class PaymentSubmitItem
{
    public string ServiceID { get; set; } = "";
    public string VendorId { get; set; } = "";
    public string PlannedPaymentMode { get; set; } = "RTGS";
}

/// <summary>Create RTGS Advice: shared payment details and a TDS % per entry (the details are saved on the entries; no UTR yet).</summary>
public class CreateRtgsAdviceRequest : FinanceApiRequest
{
    public List<RtgsAdviceItem> Items { get; set; } = [];
    /// <summary>Bank A/C (STPL).</summary>
    public string SenderBank { get; set; } = "";
    public string ChequeType { get; set; } = "";
    public string PaymentMode { get; set; } = "";
    public string ChequeNo { get; set; } = "";
    /// <summary>Date of Advance.</summary>
    public string ChequeDate { get; set; } = "";
    public string RandomNo { get; set; } = "";
    public string CreatedByName { get; set; } = "";
}

public class RtgsAdviceItem
{
    public string ServiceID { get; set; } = "";
    public decimal TdsPercent { get; set; }
}

/// <summary>Payment Request payment of one entry: step 1 sends no UTRNo, step 2 sends it (the entry is then Paid).</summary>
public class UpdatePaymentRequestPaymentRequest : FinanceApiRequest
{
    public string ServiceID { get; set; } = "";
    public string ChequeType { get; set; } = "";
    public string PaymentMode { get; set; } = "";
    public string BankName { get; set; } = "";
    public string ChequeNo { get; set; } = "";
    public string PaymentDate { get; set; } = "";
    public string? UTRNo { get; set; }
    public string PaidByName { get; set; } = "";
}

/// <summary>Update Details of an RTGS advice: a UTR per entry.</summary>
public class UpdateRtgsPaymentStatusRequest : FinanceApiRequest
{
    public string RtgsNo { get; set; } = "";
    public List<BulkUtrItem> Items { get; set; } = [];
    public string PaidByName { get; set; } = "";
}

public class UpdateAdvancePaymentRequest : FinanceApiRequest
{
    public string ServiceID { get; set; } = "";
    public decimal TdsPercent { get; set; }
    public int TdsAmount { get; set; }
    public int NetAmount { get; set; }
    /// <summary>Online or Physical Cheque.</summary>
    public string ChequeType { get; set; } = "";
    public string ReferenceNo { get; set; } = "";
    public string BankName { get; set; } = "";
    public string PaymentMode { get; set; } = "";
    public string PaymentDate { get; set; } = "";
    public int AmountPaid { get; set; }
    /// <summary>UTR / transaction no.; empty in step 1 (Add Payment Details), required in step 2 (Mark as Paid).</summary>
    public string? UTRNo { get; set; }
    public string? RandomNo { get; set; }
}

public class MarkVendorSeenRequest : FinanceApiRequest
{
    /// <summary>"pr" (Payment Request) or "avp" (Approved Vendor Payable).</summary>
    public string Area { get; set; } = "";
    public string VendorId { get; set; } = "";
}

/// <summary>Bulk Payment: one advice for several advances (TDS % per advance, shared payment details, no UTR yet).</summary>
public class CreateBulkPaymentAdviceRequest : FinanceApiRequest
{
    public List<BulkPaymentItem> Items { get; set; } = [];
    /// <summary>Physical Cheque or E-Cheque.</summary>
    public string ChequeType { get; set; } = "";
    public string PaymentMode { get; set; } = "";
    public string SenderBank { get; set; } = "";
    public string ChequeNo { get; set; } = "";
    public string PaymentDate { get; set; } = "";
    public string RandomNo { get; set; } = "";
    public string CreatedByName { get; set; } = "";
}

public class BulkPaymentItem
{
    public string ServiceID { get; set; } = "";
    public decimal TdsPercent { get; set; }
}

/// <summary>Update UTR / Transaction of a Bulk Payment advice: a UTR per advance (partial saves allowed).</summary>
public class UpdateBulkPaymentUtrRequest : FinanceApiRequest
{
    public string BulkNo { get; set; } = "";
    public List<BulkUtrItem> Items { get; set; } = [];
}

public class BulkUtrItem
{
    public string ServiceID { get; set; } = "";
    public string UTRNo { get; set; } = "";
}

/// <summary>Approver (L2) decision on a bill: Status A approves; X rejects (Kind Rejected or Disputed, with a reason).</summary>
public class ApproveVendorBillRequest : FinanceApiRequest
{
    public string ServiceID { get; set; } = "";
    public string Status { get; set; } = "A";
    public string? Kind { get; set; }
    public string? Reason { get; set; }
    public string ApprovedByName { get; set; } = "";
}

/// <summary>Approver decision on one vendor KYC document (DocKey: pan, gst, adr, chq, inc, con, bnk): A approves, X rejects with a reason.</summary>
public class UpdateKycDocumentRequest : FinanceApiRequest
{
    public int VendorIndex { get; set; }
    public string DocKey { get; set; } = "";
    public string Status { get; set; } = "A";
    public string? Reason { get; set; }
    public string ApprovedByName { get; set; } = "";
}

/// <summary>Approver (L2) decision on an advance request: Status A approves, X rejects (with a reason).</summary>
public class ApproveAdvanceRequest : FinanceApiRequest
{
    public string ServiceID { get; set; } = "";
    public string Status { get; set; } = "A";
    public string? Reason { get; set; }
    public string ApprovedByName { get; set; } = "";
}
