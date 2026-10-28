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
    /// <summary>Approver (L2): approve a bill, or reject / dispute it with a reason.</summary>
    Task<FinanceApiResponse> DecideBillAsync(string sid, bool approve, string by, string? kind = null, string? reason = null);
    /// <summary>Approver (L2): approve or reject an advance request.</summary>
    Task<FinanceApiResponse> DecideAdvanceAsync(string sid, bool approve, string by, string? reason = null);

    // Vendor KYC (approver): approve or reject one vendor document.
    Task<FinanceApiResponse> SetKycDocAsync(int vendorIndex, string docKey, string status, string? reason, string by);

    // Approved Vendor Payable
    Task<FinanceApiResponse> SubmitToPaymentRequestAsync(IReadOnlyList<PaymentSubmitItem> items);

    // Payment Request
    /// <summary>Create RTGS Advice: shared details and a TDS % per entry; the details are saved on the entries (no UTR yet).</summary>
    Task<FinanceApiResponse> CreateRtgsAsync(IReadOnlyDictionary<string, decimal> tdsPct, RtgsCommon common, string by);
    /// <summary>Step 1 (no UTR) or step 2 (UTR given: Paid) of a Payment Request payment.</summary>
    Task<FinanceApiResponse> SavePayDetailsAsync(string sid, string by, string cType, string mode, string bank, string chequeNo, string date, string? utr);
    /// <summary>Update Details of an RTGS advice: a UTR per entry.</summary>
    Task<FinanceApiResponse> SaveRtgsUtrsAsync(string rtgsNo, string by, IReadOnlyDictionary<string, string> utrs);

    // Advance
    Task<FinanceApiResponse> PayAdvanceAsync(string sid, int advanceAmount, AdvancePayment payment);
    /// <summary>Bulk Payment: saves the shared payment details on every advance (TDS % each, no UTR yet) and creates one Bulk Payment advice.</summary>
    Task<FinanceApiResponse> CreateBulkPaymentAsync(IReadOnlyDictionary<string, decimal> tdsPct, AdvancePayment common, RtgsCommon advice, string by);
    /// <summary>Update UTR of a Bulk Payment advice: advances with a UTR become Paid.</summary>
    Task<FinanceApiResponse> SaveBulkUtrsAsync(string bulkNo, IReadOnlyDictionary<string, string> utrs);

    // Red dots
    Task<FinanceApiResponse> MarkSeenAsync(string area, string vendorId);
}
