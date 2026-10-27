using SouthernTravels.Areas.Finance.Models;
using SouthernTravels.Core.Infrastructure;
using SouthernTravels.Core.Models;
using static SouthernTravels.Core.Models.G;
using static SouthernTravels.Areas.Finance.Models.FinanceG;

namespace SouthernTravels.Areas.Finance.Reports;

/// <summary>Vendor KYC Report (Approval): the approver approves or rejects each of a vendor's seven mandatory documents.</summary>
public sealed partial class FinanceReports
{
    sealed record KycApRow(Kyc K, Vendor V, List<KycDoc> Docs, string Status);

    static (string Label, string Tone) KycDocChip(KycDoc d) => d.St switch
    {
        "V" => ("System Approved", "ok"), "A" => ("Approved", "ok"), "P" => ("Pending", "warn"), "R" => ("Rejected", "danger"), _ => ("Not uploaded", "off"),
    };

    /// <summary>Rejected (any rejected) → Incomplete (any not uploaded) → Approved (all seven approved) → Pending.</summary>
    static string KycOverall(List<KycDoc> docs) =>
        docs.Any(d => d.St == "R") ? "Rejected" : docs.Any(d => d.St == "N") ? "Incomplete" : docs.All(d => d.Ok) ? "Approved" : "Pending";

    /// <summary>Placeholder summary of a document, taken from the vendor's KYC record.</summary>
    static string KycDetail(Kyc k, string key) => key switch
    {
        "pan" => k.Pan, "gst" => k.Gstn, "adr" => "XXXX XXXX " + (k.Mobile.Length >= 4 ? k.Mobile[^4..] : "0000"),
        "chq" => k.Bank + " · A/c …" + (k.Acc.Length >= 4 ? k.Acc[^4..] : k.Acc), "inc" => "Incorporation certificate", "con" => "Contract valid to " + Fmt.Fd(k.Expiry),
        _ => k.Ifsc + " · " + k.Bank,
    };

    public ReportVm VendorKycApproval()
    {
        var rows = st.K.Select(k =>
        {
            // A vendor from the API has no document states yet: treat all seven as not uploaded.
            var docs = k.Ap.Count == KycDocs.All.Length ? k.Ap : KycDocs.All.Select(_ => new KycDoc()).ToList();
            return new KycApRow(k, st.V[k.Vi], docs, KycOverall(docs));
        }).Where(r => PerOk(r.K.Created)).ToList();
        var defs = new List<FDef<KycApRow>>
        {
            new("es", "Approval Status", r => r.Status, ["Pending", "Incomplete", "Approved", "Rejected"]),
            new("type", "Service Type", r => r.V.Type), new("state", "State", r => r.V.State),
        };
        var baseRows = rows;
        // Pending first, then rejected, incomplete and approved; vendors in order within each.
        static int Rank(string s) => s switch { "Pending" => 0, "Rejected" => 1, "Incomplete" => 2, _ => 3 };
        rows = ApplyF(rows, defs).Where(r => SearchOk(r.V.Id, r.V.Name, r.V.LegalName, r.V.City)).OrderBy(r => Rank(r.Status)).ThenBy(r => r.K.Vi).ToList();
        var (pageRows, pager) = Paged(rows);
        var kpis = new List<Kpi>
        {
            K("Total Vendors", rows.Count, Strong), K("Pending Approval", rows.Count(r => r.Status == "Pending"), Warn), K("Incomplete", rows.Count(r => r.Status == "Incomplete"), Warn),
            K("Approved", rows.Count(r => r.Status == "Approved"), Green), K("Rejected", rows.Count(r => r.Status == "Rejected"), Red),
            // Vendors with a document the vendor can still verify online (PAN, Aadhaar, Bank) that is waiting.
            K("Awaiting Vendor OTP", rows.Count(r => r.Docs.Where((d, j) => KycDocs.All[j].Via != null && d.St == "P").Any()), Blue),
        };

        var nh = Cols(["", .. KycDocs.All.Select(d => (object)(d.Name, "center"))]);
        var gridRows = pageRows.Select(r =>
        {
            var (k, v) = (r.K, r.V);
            int ok = r.Docs.Count(d => d.Ok), pend = r.Docs.Count(d => d.St == "P"), rej = r.Docs.Count(d => d.St == "R"), none = r.Docs.Count(d => d.St == "N");
            var statusRow = new List<Cell> { Txt("left", "Status", Mut, 600, 11) };
            var detailRow = new List<Cell> { Txt("left", "Details", Mut, 600, 11) };
            for (var j = 0; j < KycDocs.All.Length; j++)
            {
                var d = r.Docs[j]; var (key, name, via) = KycDocs.All[j];
                var (label, tone) = KycDocChip(d);
                List<TipLine>? tip = d.St == "R" ? [new("Rejected by " + d.By, "", b: true), new(d.Reason ?? "")] : d.St == "A" ? [new("Approved by " + d.By, "", b: true), new(Fmt.Fdt(d.At))]
                    : d.St == "V" ? [new("Verified by the vendor through " + via)] : null;
                // ✓ and ✕ side by side, icon only; nothing to decide on a document that was not uploaded, and nothing at all when read-only.
                Line?[] buttons = readOnly || d.St == "N" ? [] : new Line?[]
                {
                    d.St == "A" ? null : ActBtn("Approve document", $"/Finance/VendorKyc/Approve?vi={k.Vi}&key={key}", "primary", "check", iconOnly: true),
                    d.St == "R" ? null : IconBtn("x", "Reject document", Pop("kycrej", ("k", k.Vi), ("key", key)), variant: "soft"),
                };
                statusRow.Add(Ca("center", [Chip(label, tone, tip), .. buttons]));
                detailRow.Add(d.St == "N" ? Txt("center", "-", Mut)
                    : Ca("center", T(d.St == "V" ? "Verified via " + via : KycDetail(k, key), Mut, s: 11, num: true), Lk("View", Pop("doc", ("k", k.Vi), ("n", name)), s: 11)));
            }
            var chip = r.Status switch { "Approved" => "ok", "Rejected" => "danger", "Incomplete" => "off", _ => "warn" };
            return new Row
            {
                Key = v.Id, CardHead = 3,
                Detail = new Detail { Cols = nh, Rows = [statusRow, detailRow], CardHead = 1 },
                Cells =
                [
                    VendorCell(v), C(Pill(v.Type)),
                    C(T($"{ok} / {KycDocs.All.Length} approved", ok == KycDocs.All.Length ? Green : Ink, 700),
                        Sub($"{pend} pending · {rej} rejected · {none} not uploaded")),
                    Ca("center", Chip(r.Status, chip)), Txt("left", Fmt.Fd(k.Created), num: true),
                ],
            };
        }).ToList();

        return new ReportVm
        {
            Title = "Vendor KYC Report", Kpis = kpis, Query = q,
            Desc = "KYC submitted for approval. Every listed document is mandatory and the vendor stays Incomplete until all are uploaded and approved. PAN, Address Proof (Aadhaar) and Bank Details can also be verified by the vendor through OTP / API. Contact, bank and document values are sample data.",
            Qph = "Search Vendor Code, Name, Legal Name, City...", Filters = MkFilters(baseRows, defs), FCount = FCount(defs, false), Count = rows.Count + " vendors",
            HasExport = false, Cols = Cols("", "Vendor Name", "Type", "Documents", ("Status", "center"), "Submitted"),
            Rows = gridRows, EmptyText = "No vendors found.", Pager = pager, ClearAllUrl = ClearAll,
        };
    }
}
