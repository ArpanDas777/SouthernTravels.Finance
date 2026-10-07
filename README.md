# Southern Travels Finance

ASP.NET Core 8 MVC (Razor `.cshtml`) build of the **Finance Module** design from Claude Design. It has five reports:

| Report | URL |
|---|---|
| Vendor Payable Report | `/Finance/VendorPayable` (default page) |
| Advance Report | `/Finance/Advance` |
| Approved Vendor Payable | `/Finance/ApprovedVendorPayable` |
| Payment Request Report | `/Finance/PaymentRequest` |
| Vendor KYC Report | `/Finance/VendorKyc` |

## Run

```bash
dotnet run
```

Then open http://localhost:5180.

**Visual Studio:** open `SouthernTravels.Finance.sln` (Visual Studio 2022 17.8 or later, for .NET 8) and press F5. The `SouthernTravels.Finance` launch profile opens the browser at http://localhost:5180.

## How it fits together

- `Data/FinanceStore.cs`: in-memory sample data (vendors, tickets, services, bills, advances, KYC). It is a singleton, so it resets on restart. Replace it with a real data source.
- `Reports/FinanceReports.cs`: builds each report from the store: KPIs, filters, grouping, rows and expanded detail.
- `Views/Finance/Report.cshtml`: one view renders every report (title, KPIs, tabs, toolbar, filters, grid, pager).
- `Views/Finance/Popups/*.cshtml`: Raise/Reupload Bill, Bill Details, Vendor Bills, Payment Submission Advice, Advance payment, Payment Request payment and the document preview. `site.js` fetches them into a modal, and their forms post back to `FinanceController`.
- `wwwroot/css/tokens.css`: design-system tokens (unchanged from the design bundle). `site.css` holds the component styles.
- `wwwroot/js/site.js`: row expand/collapse, Expand All, filter panel, bulk selection, tooltips, toasts and popup forms (validation, TDS calculation).

### Advance Advice (print / PDF)

`Views/Finance/AdvanceAdvice.cshtml` (styles in `wwwroot/css/advice.css`) is the printable A4 advance advice. It opens at `/Finance/AdvanceAdvice?sid=…` from **Print Advice** in the Advance Report or the Payment Details popup. It is only available for requests approved at both L1 and L2. It shows the vendor, service and guest, and financial details for that advance. Once a payment is recorded with **Update Payment**, the Payment Details section fills in (UTR or cheque number, bank, vendor account and IFSC from KYC, date, mode, and amount paid after TDS). **Download PDF** opens the browser's print dialog; choose "Save as PDF". The company address, GSTIN, phone and email in the header are still placeholders.

### Payment Advice (Payment Request Report)

Each vendor row in the Payment Request Report shows:
- **Approval Status:** Approved only when every ticket in the drill-down has management approval; otherwise Pending.
- **Payment Status:** Paid only when every ticket is paid; otherwise Pending.

Hovering over Pending shows the count, for example "1 of 3 paid".

**Download Advice** opens `Views/Finance/PaymentAdvice.cshtml` (`/Finance/PaymentAdvice?vi=…&sids=…`). It lists the vendor's **unpaid** tickets from the drill-down, with the selling price and vendor payable for each, along with vendor details, bank details from KYC, totals, blank payment-details boxes and signatures. The button is disabled once every ticket for that vendor is paid. Each drill-down line also has its own **Download Advice**, which gives the same document for just that ticket (disabled once it is paid). Long lists continue onto extra A4 pages when printed.

Report state (search, view, filters, sort, page) is kept in the query string. The demo user and the period filter are kept in cookies. Use **Switch user** to try **Vijay Gupta**, whose reports are read only.

### Recording payments

Both payment popups ask for the following, with mandatory fields marked with a red *:

| Field | Update Payment (Advance Report) | Update Payment Details (Payment Request) |
|---|---|---|
| Payment Date | Entered by the user; cannot be in the future | Same |
| Added Date & Time | Set by the system on save | Same |
| Amount Paid | Pre-filled with the net payable after TDS; editable | Pre-filled with the approved payable; editable |
| Payment Slip | Upload required | Upload required |
| Reference number | Cheque No. or UTR, depending on cheque type | Cheque No. is required when the mode is Cheque; UTR is required for NEFT, RTGS and IMPS; neither is required for Cash |

The Advance Advice shows the entered Payment Date, Amount Paid, the slip's file name and the Added On time.

## Not wired yet

- Uploaded bill files and payment slips: only the file name is stored.
- Document preview: shows a placeholder.
- Export and Print Advice: only show a confirmation toast.
- Header search and notifications: visual only, as in the design.
