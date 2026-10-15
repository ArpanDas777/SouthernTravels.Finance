using SouthernTravels.Areas.Finance.Data;
using SouthernTravels.Areas.Finance.Models;
using SouthernTravels.Core.Infrastructure;
using SouthernTravels.Core.Models;
using static SouthernTravels.Core.Models.G;
using static SouthernTravels.Areas.Finance.Models.FinanceG;

namespace SouthernTravels.Areas.Finance.Reports;

/// <summary>Vendor KYC report.</summary>
public sealed partial class FinanceReports
{
    sealed record KycRow(Kyc K, Vendor V, int N, int P, string Es);

    public ReportVm VendorKyc()
    {
        string[] dn = ["PAN Copy", "GST Reg. Cert.", "Address Proof", "Cancelled Cheque", "Cert. of Incorp."];
        var rows = st.K.Select(k =>
        {
            var n = k.Docs.Count(x => x); var p = n * 20;
            return new KycRow(k, st.V[k.Vi], n, p, p == 100 ? "Empanelled" : p >= 60 ? "Pending Verification" : "Expired");
        }).Where(r => PerOk(r.K.Created)).ToList();
        var defs = new List<FDef<KycRow>>
        {
            new("es", "Empanelment Status", r => r.Es, ["Empanelled", "Pending Verification", "Expired"]),
            new("type", "Service Type", r => r.V.Type), new("state", "State", r => r.V.State),
            new("act", "Active Status", r => r.K.Active ? "Active" : "Inactive", ["Active", "Inactive"]),
        };
        var baseRows = rows;
        rows = ApplyF(rows, defs).Where(r => SearchOk(r.V.Id, r.V.Name, r.V.City)).ToList();
        var (skip, pager) = MkPager(rows.Count);
        var kpis = new List<Kpi>
        {
            K("Total Vendors", rows.Count, Strong), K("Empanelled", rows.Count(r => r.Es == "Empanelled"), Green),
            K("Pending Verification", rows.Count(r => r.Es == "Pending Verification"), Warn), K("Expired", rows.Count(r => r.Es == "Expired"), Red),
            K("Active Vendors", rows.Count(r => r.K.Active), Blue),
        };
        static Fact F(string l, string v, bool full = false) => new() { L = l, V = v, Full = full };

        var gridRows = rows.Skip(skip).Take(10).Select(r =>
        {
            var (k, v) = (r.K, r.V);
            var detail = new Detail
            {
                PanelCols = "1fr 1fr 1.2fr",
                Panels =
                [
                    Pan("Contact & business", "var(--action)", [F("Form No.", k.Form), F("Address", k.Addr), F("Contact 1", k.Contact), F("Mobile", k.Mobile), F("Email", k.Email, true), F("PAN No.", k.Pan), F("GST No.", k.Gstn)]),
                    Pan("Bank & payment terms", "var(--calc-ink)", [F("Bank Name", k.Bank), F("Account No.", k.Acc), F("IFSC Code", k.Ifsc), F("TDS % / Terms", k.Terms), F("Contracted By", k.By), F("Approved By", k.Appr != "" ? k.Appr : "-"), F("Date of Creation", Fmt.Fd(k.Created), true)]),
                    new Panel { Title = "KYC documents", Strip = "var(--nav-bg)", Docs = dn.Select((n, j) => new DocTile { Name = n, Received = k.Docs[j], Pop = k.Docs[j] ? Pop("doc", ("k", k.Vi), ("n", n)) : null }).ToList() },
                ],
            };
            return new Row
            {
                Key = v.Id, Detail = detail,
                Cells =
                [
                    Txt("left", v.Id, Link, 700), Txt("left", v.Name, Strong, 600), C(Pill(v.Type)), Txt("left", v.City, Mut), Txt("left", v.State, Mut),
                    C(Chip(k.Active ? "Active" : "Inactive", k.Active ? "ok" : "off")), C(Chip(r.Es, r.Es == "Empanelled" ? "ok" : r.Es == "Expired" ? "danger" : "warn")),
                    Txt("center", r.P + "%", r.P == 100 ? Green : Warn, 700, num: true), Txt("center", r.N + " / 5", num: true), Txt("left", Fmt.Fd(k.Expiry), num: true),
                ],
            };
        }).ToList();

        return new ReportVm
        {
            Title = "Vendor KYC Report", Perm = perm, Kpis = kpis, Query = q,
            Desc = "Centralised KYC and vendor-onboarding register. Top line shows empanelment status and completeness; expand a vendor for contact, bank and document detail side by side. Contact, bank and document values are sample data — no KYC source is connected yet.",
            Qph = "Search Vendor Code, Name, City...", Filters = MkFilters(baseRows, defs), FCount = FCount(defs, false), Count = rows.Count + " vendors",
            HasExport = false, HasExpand = false,
            Cols = Cols("", "Vendor Code", "Vendor Name", "Type", "City", "State", "Active", "Empanelment Status", ("KYC %", "center"), ("Docs Received", "center"), "Contract Expiry"),
            Rows = gridRows, EmptyText = "No vendors found.", Pager = pager, ClearAllUrl = ClearAll,
        };
    }
}
