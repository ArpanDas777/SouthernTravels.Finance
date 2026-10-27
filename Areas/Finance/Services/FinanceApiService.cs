using SouthernTravels.Areas.Finance.Common;
using SouthernTravels.Areas.Finance.Data;
using SouthernTravels.Areas.Finance.Interfaces;
using SouthernTravels.Areas.Finance.Models;
using SouthernTravels.Areas.Finance.Models.Api;
using SouthernTravels.Core.Common;
using SouthernTravels.Core.Helpers;
using SouthernTravels.Core.Security;

namespace SouthernTravels.Areas.Finance.Services;

/// <summary>
/// Finance data from the backend API, built like STPLCostSheet's OperationService: BaseApiService for the URL,
/// IApiClientHelper for the POST, and the session's user and token sent with every request.
/// Used when appsettings FinanceSettings:UseApi is true.
/// </summary>
public class FinanceApiService : BaseApiService, IFinanceService
{
    readonly IApiClientHelper _apiClient;
    readonly ISessionSecurityService _security;
    readonly IConfiguration _configuration;
    readonly ILogger<FinanceApiService> _logger;

    public FinanceApiService(IApiClientHelper apiClient, ISessionSecurityService security, IConfiguration config, ILogger<FinanceApiService> logger)
        : base(config, "FinanceAPI")
    {
        _apiClient = apiClient;
        _security = security;
        _configuration = config;
        _logger = logger;
    }

    /// <summary>Fills tenant, user and security key from the session (as every STPLCostSheet call does).</summary>
    T Signed<T>(T request) where T : FinanceApiRequest
    {
        var session = _security.ValidateSession();
        if (!session.IsValid) throw new ApplicationException(session.ErrorMessage ?? "Session expired. Please login again.");
        request.TenantID = int.TryParse(session.TenantId, out var tid) ? tid : _configuration.GetValue("FinanceSettings:TenantId", 1);
        request.UserId = session.UserName!;
        request.UserName = session.UserName!;
        request.SecurityKey = session.Token!;
        return request;
    }

    async Task<FinanceApiResponse> Post(string action, FinanceApiRequest request)
    {
        var response = await _apiClient.PostAsync<FinanceApiResponse>(BuildUrl(FinanceApiEndpoints.Finance, action), Signed(request));
        if (response == null) throw new ApplicationException($"No response from the Finance API ({action}).");
        return response;
    }

    /// <summary>Calls a save action and turns API errors into a failed response the controller shows as a toast.</summary>
    async Task<FinanceApiResponse> Save(string action, FinanceApiRequest request)
    {
        try
        {
            var response = await Post(action, request);
            if (!response.Success) _logger.LogWarning("Finance API {Action} failed: {Message}", action, response.Message);
            return response;
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "Finance API {Action} failed", action);
            return Fail<FinanceApiResponse>(ex.Message);
        }
    }

    /* ---------- report data ---------- */

    public async Task<FinanceStore> GetStoreAsync()
    {
        async Task<FinanceApiResponse> List(string action)
        {
            var response = await Post(action, new FinanceListRequest());
            if (!response.Success) _logger.LogWarning("Finance API {Action} returned Success=false: {Message}", action, response.Message);
            return response;
        }

        // The lists are independent, so they load in parallel.
        var vendors = List(FinanceApiEndpoints.FinanceActions.GetVendorList);
        var tickets = List(FinanceApiEndpoints.FinanceActions.GetTicketList);
        var services = List(FinanceApiEndpoints.FinanceActions.GetVendorServiceList);
        var bills = List(FinanceApiEndpoints.FinanceActions.GetVendorBillList);
        var kyc = List(FinanceApiEndpoints.FinanceActions.GetVendorKycList);
        var payments = List(FinanceApiEndpoints.FinanceActions.GetPaymentRequestList);
        var advances = List(FinanceApiEndpoints.FinanceActions.GetAdvanceRequestList);
        var rtgs = List(FinanceApiEndpoints.FinanceActions.GetRtgsAdviceList);
        var flags = List(FinanceApiEndpoints.FinanceActions.GetVendorNotificationFlags);
        await Task.WhenAll(vendors, tickets, services, bills, kyc, payments, advances, rtgs, flags);

        return FinanceApiMapper.ToStore(
            vendors.Result.objVendorList, tickets.Result.objTicketList, services.Result.objVendorServiceList, bills.Result.objVendorBillList,
            kyc.Result.objVendorKycList, payments.Result.objPaymentRequestList, advances.Result.objAdvanceRequestList,
            rtgs.Result.objRtgsAdviceList, flags.Result.objVendorNotificationList);
    }

    /* ---------- Vendor Payable ---------- */

    public Task<FinanceApiResponse> RaiseBillsAsync(IReadOnlyList<(string Sid, Bill Bill)> bills) =>
        Save(FinanceApiEndpoints.FinanceActions.RaiseVendorBill, new RaiseVendorBillRequest
        {
            Bills = bills.Select(x => new RaiseVendorBillItem
            {
                ServiceID = x.Sid, BillStatus = x.Bill.St, BillDate = x.Bill.Date, BillNo = x.Bill.No, DocumentName = x.Bill.Doc,
                InvoiceAmount = x.Bill.Inv, ReceivableAmount = x.Bill.Rec, AdditionalDocumentName = x.Bill.Extra, MismatchReason = x.Bill.Mr,
            }).ToList(),
        });

    public Task<FinanceApiResponse> ApproveBillAsync(string sid, string by) =>
        Save(FinanceApiEndpoints.FinanceActions.ApproveVendorBill, new ApproveVendorBillRequest { ServiceID = sid, Status = "A", ApprovedByName = by });

    public Task<FinanceApiResponse> RejectBillAsync(string sid, string kind, string reason, string by) =>
        Save(FinanceApiEndpoints.FinanceActions.ApproveVendorBill, new ApproveVendorBillRequest { ServiceID = sid, Status = "X", Kind = kind, Reason = reason, ApprovedByName = by });

    public Task<FinanceApiResponse> DecideAdvanceAsync(string sid, bool approve, string by, string? reason = null) =>
        Save(FinanceApiEndpoints.FinanceActions.ApproveAdvanceRequest, new ApproveAdvanceRequest { ServiceID = sid, Status = approve ? "A" : "X", Reason = reason, ApprovedByName = by });

    public Task<FinanceApiResponse> SetKycDocAsync(int vendorIndex, string docKey, string status, string? reason, string by) =>
        Save(FinanceApiEndpoints.FinanceActions.UpdateKycDocument, new UpdateKycDocumentRequest { VendorIndex = vendorIndex, DocKey = docKey, Status = status, Reason = reason, ApprovedByName = by });

    /* ---------- Approved Vendor Payable ---------- */

    public Task<FinanceApiResponse> SubmitToPaymentRequestAsync(IReadOnlyList<PaymentSubmitItem> items) =>
        Save(FinanceApiEndpoints.FinanceActions.SubmitToPaymentRequest, new SubmitToPaymentRequestRequest { Items = items.ToList() });

    /* ---------- Payment Request ---------- */

    public Task<FinanceApiResponse> CreateRtgsAsync(IReadOnlyDictionary<string, decimal> tdsPct, RtgsCommon common, string by) =>
        Save(FinanceApiEndpoints.FinanceActions.CreateRtgsAdvice, new CreateRtgsAdviceRequest
        {
            Items = tdsPct.Select(x => new RtgsAdviceItem { ServiceID = x.Key, TdsPercent = x.Value }).ToList(), SenderBank = common.SenderBank,
            ChequeType = common.CType, PaymentMode = common.Mode, ChequeNo = common.ChequeNo, ChequeDate = common.ChequeDate, RandomNo = common.RandomNo, CreatedByName = by,
        });

    public Task<FinanceApiResponse> SavePayDetailsAsync(string sid, string by, string cType, string mode, string bank, string chequeNo, string date, string? utr) =>
        Save(FinanceApiEndpoints.FinanceActions.UpdatePaymentRequestPayment, new UpdatePaymentRequestPaymentRequest
        {
            ServiceID = sid, ChequeType = cType, PaymentMode = mode, BankName = bank, ChequeNo = chequeNo, PaymentDate = date, UTRNo = utr, PaidByName = by,
        });

    public Task<FinanceApiResponse> SaveRtgsUtrsAsync(string rtgsNo, string by, IReadOnlyDictionary<string, string> utrs) =>
        Save(FinanceApiEndpoints.FinanceActions.UpdateRtgsPaymentStatus, new UpdateRtgsPaymentStatusRequest
        {
            RtgsNo = rtgsNo, PaidByName = by, Items = utrs.Where(x => x.Value != "").Select(x => new BulkUtrItem { ServiceID = x.Key, UTRNo = x.Value }).ToList(),
        });

    /* ---------- Advance ---------- */

    public Task<FinanceApiResponse> PayAdvanceAsync(string sid, int advanceAmount, AdvancePayment pay)
    {
        pay.Settle(advanceAmount);
        return Save(FinanceApiEndpoints.FinanceActions.UpdateAdvancePayment, new UpdateAdvancePaymentRequest
        {
            ServiceID = sid, TdsPercent = pay.Pct, TdsAmount = pay.Tds, NetAmount = pay.Net, ChequeType = pay.CType, ReferenceNo = pay.No,
            BankName = pay.Bank, PaymentMode = pay.Mode, PaymentDate = pay.PaymentDate, AmountPaid = pay.AmountPaid,
            UTRNo = pay.Utr, RandomNo = pay.Rnd,
        });
    }

    public Task<FinanceApiResponse> CreateBulkPaymentAsync(IReadOnlyDictionary<string, decimal> tdsPct, AdvancePayment common, RtgsCommon advice, string by) =>
        Save(FinanceApiEndpoints.FinanceActions.CreateBulkPaymentAdvice, new CreateBulkPaymentAdviceRequest
        {
            Items = tdsPct.Select(x => new BulkPaymentItem { ServiceID = x.Key, TdsPercent = x.Value }).ToList(), ChequeType = common.CType,
            PaymentMode = common.Mode, SenderBank = advice.SenderBank, ChequeNo = advice.ChequeNo, PaymentDate = common.PaymentDate, RandomNo = common.Rnd, CreatedByName = by,
        });

    public Task<FinanceApiResponse> SaveBulkUtrsAsync(string bulkNo, IReadOnlyDictionary<string, string> utrs) =>
        Save(FinanceApiEndpoints.FinanceActions.UpdateBulkPaymentUtr, new UpdateBulkPaymentUtrRequest
        {
            BulkNo = bulkNo, Items = utrs.Where(x => x.Value != "").Select(x => new BulkUtrItem { ServiceID = x.Key, UTRNo = x.Value }).ToList(),
        });

    /* ---------- red dots ---------- */

    public Task<FinanceApiResponse> MarkSeenAsync(string area, string vendorId) =>
        Save(FinanceApiEndpoints.FinanceActions.MarkVendorSeen, new MarkVendorSeenRequest { Area = area, VendorId = vendorId });
}
