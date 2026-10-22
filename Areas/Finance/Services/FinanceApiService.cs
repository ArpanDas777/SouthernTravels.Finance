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
                InvoiceAmount = x.Bill.Inv, ReceivableAmount = x.Bill.Rec,
            }).ToList(),
        });

    /* ---------- Approved Vendor Payable ---------- */

    public Task<FinanceApiResponse> SubmitToPaymentRequestAsync(IReadOnlyList<PaymentSubmitItem> items) =>
        Save(FinanceApiEndpoints.FinanceActions.SubmitToPaymentRequest, new SubmitToPaymentRequestRequest { Items = items.ToList() });

    /* ---------- Payment Request ---------- */

    public Task<FinanceApiResponse> CreateRtgsAsync(IReadOnlyCollection<string> sids, RtgsCommon common, string by) =>
        Save(FinanceApiEndpoints.FinanceActions.CreateRtgsAdvice, new CreateRtgsAdviceRequest
        {
            ServiceIDs = sids.ToList(), SenderBank = common.SenderBank, ChequeNo = common.ChequeNo, ChequeDate = common.ChequeDate,
            RandomNo = common.RandomNo, Remarks = common.Remarks, CreatedByName = by,
        });

    public Task<FinanceApiResponse> PayRequestAsync(string sid, string by, PaymentEntry p, int amountPaid) =>
        Save(FinanceApiEndpoints.FinanceActions.UpdatePaymentRequestPayment, new UpdatePaymentRequestPaymentRequest
        {
            ServiceID = sid, AmountPaid = amountPaid, BankName = p.Bank, PaymentMode = p.Mode, UTRNo = p.Utr, ChequeNo = p.ChequeNo,
            PaymentDate = p.PaymentDate, PaymentSlip = p.Slip, PaidByName = by,
        });

    public Task<FinanceApiResponse> PayRtgsAsync(string rtgsNo, string by, PaymentEntry p, IReadOnlyList<RtgsPaymentItem> items) =>
        Save(FinanceApiEndpoints.FinanceActions.UpdateRtgsPaymentStatus, new UpdateRtgsPaymentStatusRequest
        {
            RtgsNo = rtgsNo, Items = items.ToList(), BankName = p.Bank, PaymentMode = p.Mode, UTRNo = p.Utr, ChequeNo = p.ChequeNo,
            PaymentDate = p.PaymentDate, PaymentSlip = p.Slip, PaidByName = by,
        });

    /* ---------- Advance ---------- */

    public Task<FinanceApiResponse> PayAdvanceAsync(string sid, int advanceAmount, AdvancePayment pay)
    {
        pay.Settle(advanceAmount);
        return Save(FinanceApiEndpoints.FinanceActions.UpdateAdvancePayment, new UpdateAdvancePaymentRequest
        {
            ServiceID = sid, TdsPercent = pay.Pct, TdsAmount = pay.Tds, NetAmount = pay.Net, ChequeType = pay.CType, ReferenceNo = pay.No,
            BankName = pay.Bank, PaymentMode = pay.Mode, PaymentDate = pay.PaymentDate, AmountPaid = pay.AmountPaid, PaymentSlip = pay.Slip,
        });
    }

    /* ---------- red dots ---------- */

    public Task<FinanceApiResponse> MarkSeenAsync(string area, string vendorId) =>
        Save(FinanceApiEndpoints.FinanceActions.MarkVendorSeen, new MarkVendorSeenRequest { Area = area, VendorId = vendorId });
}
