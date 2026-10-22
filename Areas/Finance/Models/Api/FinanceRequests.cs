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

public class CreateRtgsAdviceRequest : FinanceApiRequest
{
    public List<string> ServiceIDs { get; set; } = [];
    public string SenderBank { get; set; } = "";
    public string ChequeNo { get; set; } = "";
    public string ChequeDate { get; set; } = "";
    public string RandomNo { get; set; } = "";
    public string? Remarks { get; set; }
    public string CreatedByName { get; set; } = "";
}

/// <summary>Payment details entered in Update Payment Details (Payment Request) and Update Payment Status (RTGS).</summary>
public class PaymentDetailsRequest : FinanceApiRequest
{
    public string BankName { get; set; } = "";
    public string PaymentMode { get; set; } = "";
    public string? UTRNo { get; set; }
    public string? ChequeNo { get; set; }
    public string PaymentDate { get; set; } = "";
    public string PaymentSlip { get; set; } = "";
    public string PaidByName { get; set; } = "";
}

public class UpdatePaymentRequestPaymentRequest : PaymentDetailsRequest
{
    public string ServiceID { get; set; } = "";
    public int AmountPaid { get; set; }
}

public class UpdateRtgsPaymentStatusRequest : PaymentDetailsRequest
{
    public string RtgsNo { get; set; } = "";
    /// <summary>Every unpaid ticket line of the advice with its own approved payable.</summary>
    public List<RtgsPaymentItem> Items { get; set; } = [];
}

public class RtgsPaymentItem
{
    public string ServiceID { get; set; } = "";
    public int AmountPaid { get; set; }
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
    public string PaymentSlip { get; set; } = "";
}

public class MarkVendorSeenRequest : FinanceApiRequest
{
    /// <summary>"pr" (Payment Request) or "avp" (Approved Vendor Payable).</summary>
    public string Area { get; set; } = "";
    public string VendorId { get; set; } = "";
}
