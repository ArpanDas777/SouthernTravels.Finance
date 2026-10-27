namespace SouthernTravels.Areas.Finance.Common;

/// <summary>
/// API controller and action names for the Finance module (URL = FinanceAPI:ApiUrl + controller + "/" + action),
/// in the same style as STPLCostSheet's ApiEndpoints.
/// PLACEHOLDERS: rename these to the real action names once the backend Finance API is built.
/// </summary>
public static class FinanceApiEndpoints
{
    public const string Finance = "Finance";

    public static class FinanceActions
    {
        // ---------- Report data (loaded for every Finance page) ----------
        public const string GetVendorList = "GetVendorList";
        public const string GetTicketList = "GetTicketList";
        public const string GetVendorServiceList = "GetVendorServiceList";
        public const string GetVendorBillList = "GetVendorBillList";
        public const string GetVendorKycList = "GetVendorKycList";
        public const string GetPaymentRequestList = "GetPaymentRequestList";
        public const string GetAdvanceRequestList = "GetAdvanceRequestList";
        public const string GetRtgsAdviceList = "GetRtgsAdviceList";
        public const string GetVendorNotificationFlags = "GetVendorNotificationFlags";

        // ---------- Vendor Payable ----------
        public const string RaiseVendorBill = "RaiseVendorBill";
        public const string ApproveVendorBill = "ApproveVendorBill";

        // ---------- Approved Vendor Payable ----------
        public const string SubmitToPaymentRequest = "SubmitToPaymentRequest";

        // ---------- Payment Request ----------
        public const string CreateRtgsAdvice = "CreateRtgsAdvice";
        public const string UpdatePaymentRequestPayment = "UpdatePaymentRequestPayment";
        public const string UpdateRtgsPaymentStatus = "UpdateRtgsPaymentStatus";

        // ---------- Advance ----------
        public const string UpdateAdvancePayment = "UpdateAdvancePayment";
        public const string ApproveAdvanceRequest = "ApproveAdvanceRequest";
        public const string CreateBulkPaymentAdvice = "CreateBulkPaymentAdvice";
        public const string UpdateBulkPaymentUtr = "UpdateBulkPaymentUtr";

        // ---------- Red "new" dots ----------
        public const string UpdateKycDocument = "UpdateKycDocument";
        public const string MarkVendorSeen = "MarkVendorSeen";
    }
}
