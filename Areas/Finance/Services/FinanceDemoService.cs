using SouthernTravels.Areas.Finance.Data;
using SouthernTravels.Areas.Finance.Interfaces;
using SouthernTravels.Areas.Finance.Models;
using SouthernTravels.Areas.Finance.Models.Api;

namespace SouthernTravels.Areas.Finance.Services;

/// <summary>
/// The in-memory sample data behind the same <see cref="IFinanceService"/> contract, used while FinanceSettings:UseApi is false
/// (the backend Finance API does not exist yet). The store is a singleton, so changes last until the app restarts.
/// </summary>
public class FinanceDemoService(FinanceStore store) : IFinanceService
{
    static FinanceApiResponse Ok(int count = 1, string? rtgsNo = null) => new() { Success = true, AffectedCount = count, RtgsNo = rtgsNo };

    public Task<FinanceStore> GetStoreAsync() => Task.FromResult(store);

    public Task<FinanceApiResponse> RaiseBillsAsync(IReadOnlyList<(string Sid, Bill Bill)> bills)
    {
        store.RaiseBills(bills);
        return Task.FromResult(Ok(bills.Count));
    }

    public Task<FinanceApiResponse> ApproveBillAsync(string sid, string by) =>
        Task.FromResult(store.ApproveBill(sid, by) ? Ok() : new FinanceApiResponse { Success = false, Message = "That bill cannot be approved." });

    public Task<FinanceApiResponse> RejectBillAsync(string sid, string kind, string reason, string by) =>
        Task.FromResult(store.RejectBill(sid, kind, reason, by) ? Ok() : new FinanceApiResponse { Success = false, Message = "That bill cannot be rejected." });

    public Task<FinanceApiResponse> DecideAdvanceAsync(string sid, bool approve, string by, string? reason = null) =>
        Task.FromResult(store.DecideAdvance(sid, approve, by, reason) ? Ok() : new FinanceApiResponse { Success = false, Message = "That advance cannot be decided." });

    public Task<FinanceApiResponse> SetKycDocAsync(int vendorIndex, string docKey, string status, string? reason, string by)
    {
        store.SetKycDoc(vendorIndex, docKey, status, reason, by);
        return Task.FromResult(Ok());
    }

    public Task<FinanceApiResponse> SubmitToPaymentRequestAsync(IReadOnlyList<PaymentSubmitItem> items)
    {
        var modes = new Dictionary<int, string>();
        foreach (var i in items)
        {
            var vi = store.V.FindIndex(v => v.Id == i.VendorId);
            if (vi >= 0) modes[vi] = i.PlannedPaymentMode;
        }
        return Task.FromResult(Ok(store.Submit(items.Select(i => i.ServiceID).ToHashSet(), modes)));
    }

    public Task<FinanceApiResponse> CreateRtgsAsync(IReadOnlyDictionary<string, decimal> tdsPct, RtgsCommon common, string by)
    {
        var rec = store.CreateRtgs(tdsPct, common, by);
        return Task.FromResult(rec == null
            ? new FinanceApiResponse { Success = false, Message = "Those tickets are already covered by an RTGS advice." }
            : Ok(rec.Sids.Count, rec.No));
    }

    public Task<FinanceApiResponse> SavePayDetailsAsync(string sid, string by, string cType, string mode, string bank, string chequeNo, string date, string? utr)
    {
        store.SavePayDetails(sid, by, cType, mode, bank, chequeNo, date, utr, store.Bills.TryGetValue(sid, out var b) ? b.Rec : 0);
        return Task.FromResult(Ok());
    }

    public Task<FinanceApiResponse> SaveRtgsUtrsAsync(string rtgsNo, string by, IReadOnlyDictionary<string, string> utrs) =>
        Task.FromResult(Ok(store.SaveRtgsUtrs(rtgsNo, by, utrs)));

    public Task<FinanceApiResponse> PayAdvanceAsync(string sid, int advanceAmount, AdvancePayment payment)
    {
        store.PayAdvance(sid, payment);
        return Task.FromResult(Ok());
    }

    public Task<FinanceApiResponse> CreateBulkPaymentAsync(IReadOnlyDictionary<string, decimal> tdsPct, AdvancePayment common, RtgsCommon advice, string by)
    {
        var rec = store.CreateBulk(tdsPct, common, advice, by);
        return Task.FromResult(rec == null
            ? new FinanceApiResponse { Success = false, Message = "Those advances already have payment details." }
            : new FinanceApiResponse { Success = true, AffectedCount = rec.Sids.Count, BulkNo = rec.No });
    }

    public Task<FinanceApiResponse> SaveBulkUtrsAsync(string bulkNo, IReadOnlyDictionary<string, string> utrs) =>
        Task.FromResult(Ok(store.SaveBulkUtrs(bulkNo, utrs)));

    public Task<FinanceApiResponse> MarkSeenAsync(string area, string vendorId)
    {
        store.MarkSeen(area, vendorId);
        return Task.FromResult(Ok());
    }
}
