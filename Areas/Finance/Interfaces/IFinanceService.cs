using SouthernTravels.Areas.Finance.Data;
using SouthernTravels.Areas.Finance.Models;
using SouthernTravels.Areas.Finance.Models.Api;

namespace SouthernTravels.Areas.Finance.Interfaces;

/// <summary>
/// Everything the Finance controllers read or save. Two implementations, chosen in Program.cs by FinanceSettings:UseApi:
/// <see cref="Services.FinanceApiService"/> (calls the backend API, like STPLCostSheet's OperationService) and
/// <see cref="Services.FinanceDemoService"/> (the in-memory sample data, until the API exists).
/// </summary>
public interface IFinanceService
{
    /// <summary>All report data (vendors, tickets, services, bills, KYC, payment requests, advances, RTGS advices, dots) for this request.</summary>
    Task<FinanceStore> GetStoreAsync();

    // Vendor Payable
    Task<FinanceApiResponse> RaiseBillsAsync(IReadOnlyList<(string Sid, Bill Bill)> bills);

    // Approved Vendor Payable
    Task<FinanceApiResponse> SubmitToPaymentRequestAsync(IReadOnlyList<PaymentSubmitItem> items);

    // Payment Request
    Task<FinanceApiResponse> CreateRtgsAsync(IReadOnlyCollection<string> sids, RtgsCommon common, string by);
    Task<FinanceApiResponse> PayRequestAsync(string sid, string by, PaymentEntry payment, int amountPaid);
    Task<FinanceApiResponse> PayRtgsAsync(string rtgsNo, string by, PaymentEntry payment, IReadOnlyList<RtgsPaymentItem> items);

    // Advance
    Task<FinanceApiResponse> PayAdvanceAsync(string sid, int advanceAmount, AdvancePayment payment);

    // Red dots
    Task<FinanceApiResponse> MarkSeenAsync(string area, string vendorId);
}
