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

    public Task<FinanceApiResponse> CreateRtgsAsync(IReadOnlyCollection<string> sids, RtgsCommon common, string by)
    {
        var rec = store.CreateRtgs(sids, common, by);
        return Task.FromResult(rec == null
            ? new FinanceApiResponse { Success = false, Message = "Those tickets are already covered by an RTGS advice." }
            : Ok(rec.Sids.Count, rec.No));
    }

    public Task<FinanceApiResponse> PayRequestAsync(string sid, string by, PaymentEntry p, int amountPaid)
    {
        store.PayRequest(sid, by, p.Bank, p.Mode, p.Utr, p.ChequeNo, p.PaymentDate, amountPaid, p.Slip);
        return Task.FromResult(Ok());
    }

    public Task<FinanceApiResponse> PayRtgsAsync(string rtgsNo, string by, PaymentEntry p, IReadOnlyList<RtgsPaymentItem> items)
    {
        var amounts = items.ToDictionary(i => i.ServiceID, i => i.AmountPaid);
        var n = store.PayRtgs(rtgsNo, by, p.Bank, p.Mode, p.Utr, p.ChequeNo, p.PaymentDate, p.Slip, sid => amounts.GetValueOrDefault(sid));
        return Task.FromResult(Ok(n));
    }

    public Task<FinanceApiResponse> PayAdvanceAsync(string sid, int advanceAmount, AdvancePayment payment)
    {
        store.PayAdvance(sid, payment);
        return Task.FromResult(Ok());
    }

    public Task<FinanceApiResponse> MarkSeenAsync(string area, string vendorId)
    {
        store.MarkSeen(area, vendorId);
        return Task.FromResult(Ok());
    }
}
