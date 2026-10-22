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

## Project structure

The app is split into **Core** (shared by everything) and **Areas** (one per business module). Finance is the first area; add another module as a sibling folder under `Areas/`.

```
Core/                              shared by every area
  Controllers/  AppController.cs   base class: signed-in user, period, toast, area menu, Back()
                SessionController  the period filter (/Session/...)
  Infrastructure/  Fmt, ReportQuery, Clock, DemoSession
  Models/       LayoutCtx, GridModels (KPIs, columns, rows), G (grid-cell helpers), DocModels
  Views/Shared/ _Layout, _Modal, _AdviceLayout, _Facts, _DocHead, _DocSigns, Report (the report grid)
Areas/Finance/
  FinanceArea.cs                   the area's name, side menu (one entry per report), home page
  Controllers/  FinanceControllerBase, HomeController (/Finance, Seen)
                VendorPayableController          /Finance/VendorPayable
                AdvanceController                /Finance/Advance
                ApprovedVendorPayableController  /Finance/ApprovedVendorPayable
                PaymentRequestController         /Finance/PaymentRequest
                VendorKycController              /Finance/VendorKyc
  Data/         FinanceStore (in-memory sample data)
  Models/       Domain, PopupModels, FinanceG, FinanceOptions
  Reports/      FinanceReports (shared helpers) + one file per report
  Views/<Report>/ ...              each report's pop-ups and printable pages
wwwroot/                           css, js and images, shared
```

Each report controller owns its report page (`Index`), its pop-ups (`/Finance/<Report>/Popup?t=...`), its printable pages and its POST actions. `FinanceG.Pop(...)` builds a pop-up URL against the controller that owns that pop-up.

**Adding another area** (for example HR): create `Areas/HR/` with its own `Controllers`, `Models`, `Views` and an `HrArea.cs` (name, menu, home page); derive a base controller from `AppController`, mark it `[Area("HR")]` and give each controller a `[Route("HR/<Page>")]`. Its views only need an `_ViewImports.cshtml` and `_ViewStart.cshtml` like the Finance ones. The layout, pop-up frame and report grid come from Core. Register the area's data store in `Program.cs`.

## API flow (same as STPLCostSheet)

Data is read and saved through an API, in the same layers as STPLCostSheet:

```
Controller  ->  IFinanceService  ->  FinanceApiService : BaseApiService  ->  IApiClientHelper.PostAsync  ->  Finance API
                                     (URL = FinanceAPI:ApiUrl + "Finance/" + action, user + token from the session)
```

| Piece | File | STPLCostSheet equivalent |
|---|---|---|
| HTTP POST + error handling | `Core/Helpers/ApiClientHelper.cs`, `IApiClientHelper.cs` | `Helpers/ApiClientHelper.cs` |
| Base URL + `BuildUrl` + `Fail` | `Core/Common/BaseApiService.cs` | `COMMON/BaseApiService.cs` |
| API error body | `Core/Common/ApiErrorResponse.cs` | `COMMON/ApiErrorResponse.cs` |
| Session user/token | `Core/Security/SessionSecurityService.cs` | `Security/SessionSecurityService.cs` |
| Session + error middleware | `Core/Common/SessionMiddleware.cs`, `GlobalExceptionMiddleware.cs` | `COMMON/...Middleware.cs` |
| Endpoint names | `Areas/Finance/Common/FinanceApiEndpoints.cs` | `COMMON/ApiEndpoints.cs` |
| Request / response models | `Areas/Finance/Models/Api/` | `Models/Operation/`, `ApiResponseViewModel` |
| Service interface + API service | `Areas/Finance/Interfaces/IFinanceService.cs`, `Services/FinanceApiService.cs` | `IOperationService`, `OperationService` |

- **Switch:** `appsettings.json` → `FinanceSettings:UseApi`. `false` (default) uses `FinanceDemoService`, the in-memory sample data, so the UI runs before the API exists. `true` uses `FinanceApiService`.
- **Endpoint names are placeholders.** Rename the constants in `FinanceApiEndpoints.cs` and the JSON field names in `Models/Api/` to match the backend when it is built.
- **Every request** carries `TenantID`, `UserId`, `UserName` and `SecurityKey` from the session, like STPLCostSheet. They are set at login (below).
- **Reading:** before each Finance action, `FinanceControllerBase` calls `GetStoreAsync()`. The API service loads the nine lists in parallel and `FinanceApiMapper` turns them into the `FinanceStore` shape the reports already use, so the report code is unchanged. If the API fails, the page shows empty with an error toast.
- **Saving:** each POST action calls the matching service method (Raise Bill, Submit to Payment Request, Create RTGS, Update Payment, RTGS payment status, Advance payment, Mark seen). A failed save shows the API's message as a toast.

## Login (OTP, same as STPLCostSheet)

Every page needs a login. Without one, `Core/Common/SessionMiddleware.cs` redirects to `/Account/Login?returnUrl=…` (pop-ups get 401 and `site.js` sends the browser there), and after logging in you land back on the page you asked for.

1. Enter your email → **Send OTP** calls `AuthAPI:ApiUrl` + `Account/SendOTP`.
2. Enter the 6-digit OTP → **Verify & Login** calls `Account/ValidateUserOTP`.
3. On success the session gets the same keys STPLCostSheet sets: `UserId`, `UserName`, `Email`, `HandlingBranch`, `accessToken`, `refreshToken`, `IsAdmin`, `TenantId`, `LoginTime`. The access token is the `SecurityKey` sent to the Finance API.

| Piece | File |
|---|---|
| Login page, send / validate OTP, logout | `Core/Controllers/AccountController.cs`, `Core/Views/Account/Login.cshtml`, `wwwroot/css/login.css`, `wwwroot/js/login.js` |
| Account API calls | `Core/Services/AccountService.cs` (`IAccountService`), `Core/Common/AccountApiEndpoints.cs` |
| Request / response models | `Core/Models/Auth/LoginModels.cs` |

- The signed-in name, role (Administrator / User) and branch show at the bottom of the side menu, with a **Log out** button.
- The session lasts 30 minutes of inactivity.
- Send OTP and Verify are limited to 5 tries per minute per IP address (`loginLimit` in `Program.cs`).
- Every signed-in user can use the action buttons (`LayoutCtx.Perm`) until roles come from the API.

## How it fits together

- `Areas/Finance/Data/FinanceStore.cs`: the report data. Its parameterless constructor seeds the in-memory sample data (a singleton in demo mode, so it resets on restart); with the API on, a new snapshot is built from the API for each request.
- `Areas/Finance/Reports/`: builds each report from the store: KPIs, filters, grouping, rows and expanded detail.
- `Core/Views/Shared/Report.cshtml`: one view renders every report (title, KPIs, tabs, toolbar, filters, grid, pager).
- `Areas/Finance/Views/<Report>/Popups/*.cshtml`: the pop-ups. `site.js` fetches them into a modal; their forms post back to the owning controller.
- `wwwroot/css/tokens.css`: design-system tokens (unchanged from the design bundle). `site.css` holds the component styles.
- `wwwroot/js/site.js`: row expand/collapse, Expand All, filter panel, bulk selection, tooltips, toasts and popup forms (validation, TDS calculation).

### Advance Advice (print / PDF)

**Printing:** all printable documents share `wwwroot/css/doc.css`: portrait A4. The Advance Advice, Payment Advice and RTGS letters use the `.doc.color` variant (navy and blue). Both keep dark text, ruled borders and sizes that read on a black-and-white printout: solid ruled boxes instead of coloured fills or white-on-dark headings (those vanish when a printer drops background colours), nothing smaller than 10.5px, no watermark, and no meaning carried by colour alone. The pages set `@page { size: A4 portrait }`, so the print dialog defaults to portrait.
`Areas/Finance/Views/Advance/AdvanceAdvice.cshtml` (styles in `wwwroot/css/doc.css`) is the printable A4 advance advice. It opens at `/Finance/Advance/AdvanceAdvice?sid=…` from **Print Advice** in the Advance Report or the Payment Details popup. It is only available for requests approved at both L1 and L2. It shows the vendor, service and guest, and financial details for that advance. Once a payment is recorded with **Update Payment**, the Payment Details section fills in (UTR or cheque number, bank, vendor account and IFSC from KYC, date, mode, and amount paid after TDS). **Download PDF** opens the browser's print dialog; choose "Save as PDF". The company address, GSTIN, phone and email in the header are still placeholders.

### Vendor Payable drill-down

Bill Status reads **Uploaded / Pending / Disputed**; the row button is **Upload** (then **View Bill Details** and **Reupload Bill**). Rows show the ticket number (highlighted) with the file number and MBK number below it in small type. Selling, contracted and negotiated rates are one column, followed by a **Bill Amount** column (the invoice amount). Vendor lines show **Bills Uploaded** next to **Bills Pending**.

### Upload Bill, Reupload Bill and disputes

**Upload Bill** takes the bill date, bill no., invoice amount and recommended amount on one line, then an **Upload Bill** and an **Upload Voucher** document (all required). Each document can be chosen from disk or added with **Scan**, which calls the scanner service named in `Scanner:Url` in `appsettings.json` (a POST that returns the scan as PDF, PNG or JPEG) and puts the result in the field. Bill history appears only in **Reupload Bill**.

### Raise Bill and disputes

With several services in the **Raise Bill** pop-up, each service has its own **Raise Dispute — <ticket> only** button (enabled once that service's fields are filled), and the footer's **Raise Dispute (all)** disputes every service together. **Submit** raises all. Disputed bills are found in Vendor Payable → Filters → **Bill Status** → Dispute. **Reupload Bill** uses the same pop-up with empty fields and no dispute button; both show a collapsed **Bill history** table (the vendor's billed services).

### Approved Vendor Payable → Payment Request (spec of 13 Oct 2026)

- **Approved Vendor Payable:** billed, operated services of confirmed tickets. Services already sent to Payment Request stay listed as **Sent for payment** but cannot be ticked and are left out of the KPIs. Vendor Wise adds a **Total Unbilled Tickets** chip per vendor (tickets that still have an operated service without a bill), a red "New bill(s) added" dot (vendors with the dot sort first; expanding the row clears it), and an **Ageing sort** filter. Aging is grey up to 7 days, amber over 7 and red over 14.
- **Cart** (Approved Vendor Payable only): appears once something is ticked, grouped by vendor with a collapsible group row, Clear, a hide button (which leaves a "Cart (n)" pill), a total and **Submit**. On screens 1100px and wider it is a panel down the whole right edge of the page, and the page makes room for it. Below that it is a bottom sheet. On desktop (1280px and up) the rail steps aside to icons while the cart is open. The selection is kept while you filter, page or switch tabs, and is cleared when a Submit succeeds. **Vendor Payable** has no cart: ticking services shows a bulk **Raise Bill (n)** button in the toolbar.
- **Payment mode:** planned per vendor in the Payment Submission Advice (RTGS by default). In the Payment Request Report each vendor line shows its **Payment Mode** (RTGS by default) with an **Edit** button that opens a drop-down; changing it re-plans every line of that vendor that is still unpaid and not yet in an RTGS advice, and saves straight away.
- **Payment status** reads **Unpaid / Sent for payment / Paid**. In Fund Management (Approved Vendor Payable) Sent for payment means the service is in the Payment Request basket; in the Payment Request Report it means an RTGS advice has been created for the line.
- **Fund Management (Approved Vendor Payable)** is the new name of that report. Its tiles are Total Vendor, Total Payable Bills, Total Payable Value, Paid Bill and Unpaid Bills.
- **Scrolling:** the page scrolls as a whole; the title and tiles scroll away while the tabs, toolbar (and the filter panel when open) and the table header stay pinned. The pinned header follows the bar's height through `--stick-h`, set by `site.js`.
- **Paging:** every report shows 10 rows by default, with 10 / 25 / 50 / 100 choices next to the page buttons (`?ps=`). Vendor Wise and Ticket Wise pages count vendors and tickets.
- **RTGS letters:** **Download both (PDF)** (also **Both (PDF)** in RTGS Advice History) opens one printout with the Internal letter followed by the Bank letter.
- **Payment Submission Advice:** each vendor gets a planned **Payment Mode** (RTGS by default, Physical Cheque, E-Cheque, Credit Card). It is stored on every service sent to Payment Request. "Not included in this advice" lists the vendor's other billed services that were left out.
- **Payment Request Report:** vendor line (with Approval Status and Payment Status) and a drill-down with **Payment Mode** (plus the RTGS advice number once one exists), **Actual Payment Action**, **Mgmt. Approval**, and **Print** (opens the Payment Advice) and **Update** (while unpaid). Filters: Payment Status, Payment Mode, RTGS Advice, Service Type, Vendor.
- **Payment Advice** (pop-up from a ticket line's **Print**): a portrait A4 letter for one ticket line, addressed to the vendor. **Print** opens it on its own page (`/Finance/PaymentRequest/PaymentAdvicePdf?sid=…`) where Download PDF is the print dialog's Save as PDF. Paid shows the amount, mode, bank and transaction no.; unpaid says the payment is approved and scheduled by the planned mode.
- **RTGS advice:** **Create RTGS Advice** (the highlighted toolbar button) asks for the common details (Sender Ac No. bank, Cheque No., Cheque Date, Random No., optional Remarks) and the unpaid RTGS tickets not yet covered, then opens **RTGS Advice generated** with two PDF letters (portrait A4): **RTGS Internal** (with a "Total of amount" row) and **RTGS Bank** (without). The letter is in colour (navy and blue, still ruled and high-contrast so it reads on a black-and-white printout), and every table row carries its own Sender Ac No., Cheque No., Random No., Cheque Date, Beneficiary Ac No., IFSC, Amount, Beneficiary Name and Remarks. **View** shows the letter in a pop-up; **Get** (or **Download PDF** in the pop-up) opens it on its own page (`/Finance/PaymentRequest/RtgsPdf?no=…&kind=internal|bank`) where **Download PDF** opens the browser's print dialog — choose Save as PDF. **RTGS Advice History** lists every advice with Internal and Bank letters. Advice numbers run `RTGS-2610-201`, `-202`, …
- The sender account numbers on the RTGS letters are placeholders (`CBI-Delhi [A/c No.]`) until real ones are supplied.

### Responsive layout and shell

Breakpoints are CSS media queries in `wwwroot/css/site.css` (the "Responsive" section), with a small script in `wwwroot/js/site.js` for the drawer, filter sheet and cart.

| Width | Behaviour |
|---|---|
| under 480px | header title hidden |
| under 560px | header search hidden |
| under 768px (phone) | ☰ drawer (260px); no KPI tiles; full-width pop-ups |
| under 1100px | filters open as a bottom sheet (scrim, × or Esc closes it); the toolbar wraps; **every row is a card** (header with chevron/tick and the first fields, caption-and-value body, footer with the actions); the cart is a bottom sheet |
| under 1280px | the menu is an overlay drawer (216px; 260px on phones), closed by default; it closes on an outside tap, Esc or a menu click |
| 1280px and up | fixed rail that collapses to icons (clicking the main area collapses it; the toggle brings it back), inline filters, side cart |

The header is 48px with the logo, title, search and bell. The signed-in user's avatar, name and role sit at the bottom of the rail. KPIs are a strip of one-line 34px tiles. Every printable document (Advance Advice, RTGS letters, Payment Advice) is portrait A4 and scrolls sideways inside its frame on small screens.

Report state (search, view, filters, sort, page) is kept in the query string. The period filter (on the right of each report's title row) is kept in a cookie. The signed-in user comes from the OTP login (see Login).

### Recording payments

Both payment popups ask for the following, with mandatory fields marked with a red *:

| Field | Update Payment (Advance Report) | Update Payment Details (Payment Request) |
|---|---|---|
| Payment Date | Entered by the user; cannot be in the future | Same |
| Added Date & Time | Set by the system on save | Same |
| Amount Paid | Pre-filled with the net payable after TDS; editable | Pre-filled with the approved payable; editable |
| Payment Slip | Upload required | Upload required |
| Reference number | Cheque No. or UTR, depending on cheque type | Cheque No. is required when the mode is Cheque; UTR is required for NEFT, RTGS and IMPS; neither is required for Cash |

RTGS Advice History has an **Update Payment Status** button per advice (shown while any ticket is unpaid). It opens the same fields as **Update** and records them against every unpaid ticket in that advice, each paid its own approved payable. The Advance Report lists only fully approved (L1 and L2) requests; a paid entry stays and shows **Paid**.

The Advance Advice shows the entered Payment Date, Amount Paid, the slip's file name and the Added On time.

## Not wired yet

- Uploaded bill files and payment slips: only the file name is stored.
- Document preview: shows a placeholder.
- Export and Print Advice: only show a confirmation toast.
- Header search and notifications: visual only, as in the design.
