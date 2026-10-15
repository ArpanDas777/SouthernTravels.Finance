using SouthernTravels.Core.Infrastructure;
using SouthernTravels.Core.Models;

namespace SouthernTravels.Areas.Finance.Models;

/// <summary>Finance-only helpers for building grid cells and pop-up / document links.</summary>
public static class FinanceG
{
    /// <summary>CSS suffix for a service type's colour on printed advice documents.</summary>
    public static string SvcKey(string type) => type switch
    {
        "Accommodation" => "accommodation", "Transport" => "transport", "Guide" => "guide", "En-Route Meals" => "enroute-meals", _ => "other",
    };

    /// <summary>A button-styled link to the printable Advance Advice (opens in a new tab).</summary>
    public static string AdviceUrl(string sid) => "/Finance/Advance/AdvanceAdvice?sid=" + Uri.EscapeDataString(sid);

    public static Line Chk(Svc s, string group, int amount, string meta) =>
        new() { Kind = LineKind.Chk, Value = s.Id, Group = group, Text = s.T.Id, Sub = s.V.Name, Amount = amount, Vi = s.Vi, Vid = s.V.Id, Mbk = s.T.Mbk, Svc = s.Name + (s.Conf != "" ? " · " + s.Conf : ""), Meta = meta };

    public static string ChipTone(string s) => s switch
    {
        "Confirmed" or "Paid" or "Approved" or "Full Payment" => "ok",
        "Pending" or "Part Payment" => "warn",
        "Cancelled" => "danger",
        _ => "off",
    };

    public static List<TipLine>? ApTip(Approval? l) =>
        l is { St: "A" } ? [new(l.By ?? "", "", b: true), new(Fmt.Fdt(l.At))] : null;

    /// <summary>Each pop-up is served by the controller of the report it belongs to.</summary>
    static readonly Dictionary<string, string> PopupOwner = new()
    {
        ["raise"] = "VendorPayable", ["viewbill"] = "VendorPayable",
        ["vbills"] = "ApprovedVendorPayable", ["advice"] = "ApprovedVendorPayable",
        ["advpay"] = "Advance", ["advview"] = "Advance",
        ["prAdvice"] = "PaymentRequest", ["prpay"] = "PaymentRequest", ["rtgsSel"] = "PaymentRequest", ["rtgsOut"] = "PaymentRequest",
        ["rtgsHist"] = "PaymentRequest", ["rtgsView"] = "PaymentRequest", ["rtgsPay"] = "PaymentRequest",
        ["doc"] = "VendorKyc",
    };

    /// <summary>Base of a pop-up URL, e.g. "/Finance/VendorPayable/Popup?t=raise" (append "&amp;key=value" pairs).</summary>
    public static string PopUrl(string t) => "/Finance/" + PopupOwner[t] + "/Popup?t=" + t;

    public static string Pop(string t, params (string k, object v)[] args) =>
        PopUrl(t) + string.Concat(args.Select(a => "&" + a.k + "=" + Uri.EscapeDataString(Convert.ToString(a.v, Fmt.Inv) ?? "")));
}
