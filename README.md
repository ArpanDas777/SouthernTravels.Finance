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
  Infrastructure/  Fmt, ReportQuery, Clock, PeriodFilter
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

- **Switch:** `appsettings.json` â†’ `FinanceSettings:UseApi`. `false` (default) uses `FinanceDemoService`, the in-memory sample data, so the UI runs before the API exists. `true` uses `FinanceApiService`.
- **Endpoint names are placeholders.** Rename the constants in `FinanceApiEndpoints.cs` and the JSON field names in `Models/Api/` to match the backend when it is built.
- **Every request** carries `TenantID`, `UserId`, `UserName` and `SecurityKey` from the session, like STPLCostSheet. They are set at login (below).
- **Reading:** before each Finance action, `FinanceControllerBase` calls `GetStoreAsync()`. The API service loads the nine lists in parallel and `FinanceApiMapper` turns them into the `FinanceStore` shape the reports already use, so the report code is unchanged. If the API fails, the page shows empty with an error toast.
- **Saving:** each POST action calls the matching service method (Raise Bill, Submit to Payment Request, Create RTGS, Update Payment, RTGS payment status, Advance payment, Mark seen). A failed save shows the API's message as a toast.

## Login (OTP, same as STPLCostSheet)

Every page needs a login. Without one, `Core/Common/SessionMiddleware.cs` redirects to `/Account/Login?returnUrl=â€¦` (pop-ups get 401 and `site.js` sends the browser there), and after logging in you land back on the page you asked for.

1. Enter your email â†’ **Send OTP** calls `AuthAPI:ApiUrl` + `Account/SendOTP`.
2. Enter the 6-digit OTP â†’ **Verify & Login** calls `Account/ValidateUserOTP`.
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
`Areas/Finance/Views/Advance/AdvanceAdvice.cshtml` (styles in `wwwroot/css/doc.css`) is the printable A4 advance advice. It opens at `/Finance/Advance/AdvanceAdvice?sid=â€¦` from **Print Advice** in the Advance Report or the Payment Details popup. It is only available for requests approved at both L1 and L2. It shows the vendor, service and guest, and financial details for that advance. Once payment details are added (**Add Payment Details**), the Payment Details section fills in (UTR or cheque number, bank, vendor account and IFSC from KYC, date, mode, and amount paid after TDS). **Download PDF** opens the browser's print dialog; choose "Save as PDF". The company address, GSTIN, phone and email in the header are still placeholders.

### Vendor Payable Report (24–26 Oct 2026)

- **Tiles (6):** Total Payable Amount, Total No. Vendors, Total Paid Till Now, Balance Pending, Bill Pending, Disputed Tickets (tickets with a disputed bill).
- **Filters:** Service Type, Service City, **Ticket Status** (Confirmed / Pending / Cancelled), **Bill Status** (Pending / Uploaded / Disputed), Booking Executive and Branch. There is no OPS Status filter and no approval filter; every operated service of a confirmed ticket is listed.
- **Bill Status** reads **Uploaded / Pending / Disputed**. A bill shows *Uploaded* only after L1 and L2 have both approved it; until then (and when there is no bill) it shows *Pending*. This applies in every report (`Svc.BStat`).
- **Vendor Wise:** `Vendor ID · Vendor Name (+ legal name) · Total Services · Total Duration · Bills Pending · Total Business · Amount Paid · Balance Pending`. Its drill-down: `tick · Ticket Info · Service Type / City · Service / Details / Conf. No. · Voucher No. · Service Dates · Price Info · Amount as per Bill · Variance · Advance Paid · Balance Pending · Payment Status · Bill Status · Approval · Actions`.
- **Ticket Wise:** `Ticket Info · Passenger Info · Booking Executive · Base Price · Discount · GST · Grand Total · Balance · Ticket Status · Bills Pending · Total Business`; its drill-down starts with the vendor instead of the ticket.
- **Ticket Info** is the ticket no. (link), the MBK no., the file no. (`FN/001171/2026-27/VIZ0001` = `FN/` + running no. + financial year + first three letters of the branch code + ticket sequence) and the ticket status as bold coloured text. Ticket and MBK numbers carry no hyphen (`GRP1001`, `MBK48201`).
- **Variance** is the bill amount minus the negotiated amount: red above, green below, grey when equal or there is no bill. **Neg.** is highlighted in **Price Info**. **Advance Paid** shows the advance count only once something has been paid. **Voucher No.** (`VCH51000` + service number) is a placeholder link that shows a toast.
- **Upload** shows for every service without a bill; once a bill exists the row offers **View Bill Details** and **Reupload Bill**. On wide screens action buttons are icon-only with the text as tooltip; phones and tablets keep the text on the cards.
- **Accommodation** services show the hotel name, then `MEAL · Room category · nN` (hover for the full meal plan), then the occupancy (`Dbl ×2 · Sin ×1 · e.bed ×1 · CWOB ×1`). Meal plan, room category, occupancy and the adult/child split are **placeholders** derived from the sample data (`Svc.Meal`, `Svc.RoomCat`, `Svc.Occ`, `Ticket.PaxInfo`) until the API supplies them. One vendor can supply several service types: a service's own type is `ServiceDef.SType`.
- **Vendor legal name** (`Vendor.LegalName`, a placeholder `<name> Private Limited` until the API supplies it) is shown under every vendor name, in pop-ups, the cart, the RTGS selection and letters (the beneficiary is the legal name) and the Payment Advice.
- **Period** defaults to **This Month**; *Clear* returns to All time. "Today" is the real current date (`Clock.Today`).

### Upload Bill, Reupload Bill and Bill Details

All three open with a header: `GRP… · MBK… · FN/…` and a facts grid (Vendor Name, Vendor Legal Name, Hotel / Meal Plan / Room Category / Occupancy for accommodation or Service otherwise, Vendor Payable, Advance Paid, Balance Pending).

- **Fields:** Bill Date and Bill No. on one line; **Upload Bill** (the main document of the service type, required) and **Additional Docs** (optional), each with separate **Upload** and **Scan** buttons and a status line (`No file chosen` → `<file> · Ready to scan` → `Scanned ✓ · amount read ₹X`); **Amount as per Invoice**; **Mismatch Reason**, which appears only when the invoice is above the vendor payable. Document names per service type are placeholders (`FinanceOptions.DocTypes`).
- **Scan** uses the scanner service in `Scanner:Url` when no file is chosen and falls back to a simulated scan when it cannot be reached. Scanning the main bill fills Amount as per Invoice with the vendor payable (simulated; real reading comes later).
- **Raise Dispute** is enabled as soon as a file is chosen and the invoice is above the vendor payable (not on Reupload). **Submit** is disabled for a new upload while the invoice is above the payable. The recommended amount saved is the smaller of the invoice and the payable.
- A saved bill is *Pending* until L1 and L2 approve it (it starts at L1 *Waiting*, L2 *Not reached*).
- **Reupload Bill** pre-fills the last upload's date, bill no., amount and reason (the file must be chosen again) and lists every earlier upload at the bottom, newest first. Each reupload appends the replaced version to `Bill.Hist`. A reupload whose invoice is still above the payable is saved as a dispute.
- Document names in Bill Details and the history are links that show a toast (no files are stored yet).

### Approved Vendor Payable â†’ Payment Request (spec of 13 Oct 2026)

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
- **Payment Advice** (pop-up from a ticket line's **Print**): a portrait A4 letter for one ticket line, addressed to the vendor. **Print** opens it on its own page (`/Finance/PaymentRequest/PaymentAdvicePdf?sid=â€¦`) where Download PDF is the print dialog's Save as PDF. Paid shows the amount, mode, bank and transaction no.; unpaid says the payment is approved and scheduled by the planned mode.
- **RTGS advice:** **Create RTGS Advice** (the highlighted toolbar button) asks for the common details (Sender Ac No. bank, Cheque No., Cheque Date, Random No., optional Remarks) and the unpaid RTGS tickets not yet covered, then opens **RTGS Advice generated** with two PDF letters (portrait A4): **RTGS Internal** (with a "Total of amount" row) and **RTGS Bank** (without). The letter is in colour (navy and blue, still ruled and high-contrast so it reads on a black-and-white printout), and every table row carries its own Sender Ac No., Cheque No., Random No., Cheque Date, Beneficiary Ac No., IFSC, Amount, Beneficiary Name and Remarks. **View** shows the letter in a pop-up; **Get** (or **Download PDF** in the pop-up) opens it on its own page (`/Finance/PaymentRequest/RtgsPdf?no=â€¦&kind=internal|bank`) where **Download PDF** opens the browser's print dialog â€” choose Save as PDF. **RTGS Advice History** lists every advice with Internal and Bank letters. Advice numbers run `RTGS-2610-201`, `-202`, â€¦
- The sender account numbers on the RTGS letters are placeholders (`CBI-Delhi [A/c No.]`) until real ones are supplied.


### Fund Management (Approved Vendor Payable) and Payment Request (26 Oct 2026 update)

- **Fund Management rows:** only bills that are *Uploaded* (not disputed) and approved at L1 and L2, on confirmed tickets (by bill: a ticket with one disputed bill still lists its other approved bills). Tiles: Total Vendors, Total Bills, Sent for Payment, Paid, Pending; the money tiles read `₹amount (count)`. **Payment Status** reads *Pending / Sent for payment / Paid*. Drill-down columns: `Ticket Info · Ticket Details · Service Type / City · Service / Conf. No. · Start Date · End Date · Duration · Bill Date / No. / Amount · Service Price Info (Sell, Neg.) · Variance · Profit · Adv. Paid / Balance · Aging · Payment Status · Bill (View Bill, View Docs)`. **View Docs** lists the documents expected for the service type (placeholder file names; they show a toast). Ticket Wise main row: `Ticket Info · Tour Base Price · Discount · GST · Grand Total · Paid / Balance Pending · Ticket Status · Status` (Paid only when every service is paid). Aging never goes below 0.
- **Payment Request, two steps per entry** (same as the Advance Report): **Add Payment Details** (Date of Payment, Cheque Type, Payment Mode, Bank A/C (STPL), Cheque / Request No.; no UTR) then **Update Payment Details** with a mandatory UTR, which marks the entry Paid. Until then the status reads `Details added · UTR awaited`. **Print Advice** and **Print Cheque** (toast only) stay disabled until details exist; Print Cheque is hidden once paid; paid entries show **View Payment Details** (read-only).
- **Create RTGS Advice** works like Bulk Payment: the button expands every vendor and ticks appear on eligible entries (RTGS mode, unpaid, no advice yet); **Proceed (n)** opens a pop-up with a totals strip, the payment details (Date of Advance, Cheque Type, Payment Mode, Bank A/C (STPL), Random No., Cheque No. / Request No.) and a TDS % per ticket. Submit saves the details on each entry (no UTR yet), creates the advice (amounts are net of TDS) and opens the two PDF letters. **RTGS Advice History → Update Details** takes a UTR per ticket (marks that ticket Paid) and is disabled once every ticket has one.
- **Sample data:** every original vendor V101–V112 has one billed October service; V113 Perfect Holidays also has five services on GRP1017; about every fourth non-disputed bill is invoiced ₹1,000 below the negotiated amount (green variance); the payment basket has October entries in each state (no details, details added, paid).
- The RTGS letter partials live in `Areas/Finance/Views/Shared` because Bulk Payment (Advance) uses them too.
- **Login bypass (Development only):** the login page has a **By Pass Login** button (accounts below). The OTP login is unchanged.


### Approver (L2) view and Vendor KYC approval (27 Oct 2026 update)

- **Who is an approver:** the dev bypass login has three accounts, username = password: `admin` (Finance view), `mounika` (Approver, L2) and `vijay` (Approver, read-only). The role is kept in the session (`SessionKeys.Role`); OTP users are Finance users. The approver's menu has Vendor Payable, Advance and Vendor KYC only (Approved Vendor Payable and Payment Request redirect to Vendor Payable), and titles and menu entries carry "(Approval)". The Advance Report (Approval) is only retitled.
- **Vendor Payable (Approval):** only bills L1 has approved (`Bill.L1.St == "A"`). **Approval Status** is Pending / Approved / Rejected or Disputed (L2 rejected it, or the bill is disputed). Vendors and bills are listed pending first. Vendor row: `Vendor ID · Vendor Name (+ legal) · Total Tickets · Approvals (Total / Pending / Done) · Total Business`. Bill rows: Ticket Info, Service Type / City, Service, Bill Date / No. / Amount, Variance, Approval Status, **Approval Levels** (L1, L2, L3 — L3 is a placeholder, always "Not reached"; a rejected L2 shows a red ✕) and Actions (a grid, three across and two down): View Bill, View Docs, **History** / **Approve**, **Reject** (pending bills only, hidden for read-only). The six tiles (Total Bills, Pending Approval, Approved, Rejected / Disputed, Total Value, Pending Value) are my choice, since the handoff gives no list.
- **Reject / Dispute** pop-up: Mark as Rejected or Disputed, reason required; saves `Bill.L2 = { St X, Kind, Reason, By, At }`. **Approval History** pop-up: the L1 / L2 / L3 lines and every upload of the bill.
- **Vendor KYC (Approval):** seven mandatory documents per vendor (`KycDocs.All`: PAN, GST, Aadhaar, Cancelled Cheque, Incorporation, Contract, Bank). PAN, Aadhaar and Bank can be verified by the vendor online (*System Approved*); the rest are *Approved* here. Per document: System Approved / Approved / Pending / Rejected / Not uploaded. A vendor's status is **Rejected** (any rejected) → **Incomplete** (any not uploaded) → **Approved** (all seven) → **Pending**. The vendor row shows legal name, type, `approved / 7` with counts, status and submitted date; the drill-down has a Status row (chip with ✓ and ✕ icon buttons) and a Details row with a View link. ✕ asks for a reason. Document states are sample data in `Kyc.Ap` (`FinanceStore.MkKycDocs`); the Finance KYC report still uses the old five-document view.
- **Period picker:** the Period control is a popover (Today · This Month · Custom) with calendars, Clear, Cancel and Apply. **This Month** now runs from the 1st to today, **Today** is today only, Clear = All time; This Week / Quarter / Year are gone. Default: This Month.
- API placeholders added: `ApproveVendorBill` (approve or reject a bill) and `UpdateKycDocument` (approve or reject a document).


### Switch user, Approver reports and read-only (27 Oct 2026, Part 4)

- **Switch user** (bottom of the sidebar): a *User | Approver* control, a single **U / A** button when the sidebar is collapsed. Anyone can use it; it posts to `/Session/SetMode` and the choice is kept in the session (`SessionKeys.Mode`), overriding the mode the login starts in (`mounika` and `vijay` start in Approver, `admin` in User). Switching returns to the page without its query (tabs reset); Approved Vendor Payable and Payment Request redirect to Vendor Payable in Approver mode.
- **Signed-in block:** in Approver mode the name is **Vijay Gupta**, role *Approver (L1 / L2 / L3)*, and that name is stamped on every approval. In User mode: admin → Administrator, mounika → Mounika / Finance Executive, vijay → Vijay Gupta / Finance Head.
- **Read-only (`vijay`):** in both modes he sees no checkboxes, cart, Upload, Print, payment or Bulk / RTGS actions (he can still view bills and payment details), and the server refuses every save with a toast ("Read-only access — nothing was saved."). Approve and Reject are hidden for him in Approver mode.
- **Vendor Payable (Approval):** tabs Vendor Wise and Ticket Wise. Tiles: Total No. Vendors, Total Approvals, Pending Approvals, Approvals Done, Pending Approval Amount (invoice amounts still pending) and Bills With Variance (invoice ≠ negotiated). Toasts: "L2 approved — <ticket> · <bill no>." and "L2 rejected / disputed — <ticket> · <bill no>." There is no L3 step, so Approve simply sets L2 to approved.
- **Advance Report (Approval):** every advance in the period; filter *Approval Status* (Pending / Approved / Rejected); tiles Total Requests, Pending My Approval (L2), Pending Approval Amount, Approved (L2), Rejected; no bulk ticks and no Payment Status column. Rows waiting for L2 have **Approve** (immediate) and **Reject**, which opens a pop-up where a reason is required (`AdvanceRequest.Rej` keeps level, reason, who and when). Others show *Awaiting L1*, *Approved* or a red *Rejected* chip; hovering the chip, or the red ✕ of the rejected level in Approvals, shows who rejected it, "Reason: …" (or "-") and when. The Finance view still lists only fully approved advances. Four sample advances were added (two waiting for L2, one awaiting L1, one rejected).
- **Vendor KYC (Approval):** filter *Approval Status*; tiles Total Vendors, Pending Approval, Incomplete, Approved, Rejected, Awaiting Vendor OTP (a PAN, Aadhaar or Bank document still pending); vendors sorted Pending → Rejected → Incomplete → Approved.


### Module launcher and pop-up wording (27 Oct 2026, missing-items handoff)

- **Module launcher:** a nine-dot button right after the bell in the header opens a panel with a search box ("Find a module"), a 5-column grid of 12 modules (Contracts, Insights, Products, EMS, Quotation (MBK), Sales, Operations, Finance, Marketing, Admin, Approvals, Access mgmt.) and up to three quick actions for the selected module. Finance is selected by default. Clicking a tile only selects it; a quick action only shows the toast "<Action> — opens in the <Module> module." There are no routes, no role-based visibility and no keyboard support beyond Escape. It is built in `site.js` (`MODS`) and styled at the end of `site.css`.
- **Pop-ups reworded to the handoff:** *Reject / Dispute Bill* (button "Mark Rejected / Disputed"), *Approval History* (table: Step, Action By, Status, Date & Time, Remarks; every upload, then L1 Finance, L2 Management, L3 Senior Management), *Bill Documents* (Bill / Additional rows with Get and View toasts), *Reject <document>* (toast "<doc> rejected — <vendor id> · <name>."), *Re-Upload Bill*, *Update Details · <advice no.>* (toast "UTR updated for n ticket(s) — marked as paid."), and the Payment Request payment toasts. The letters stay A4 portrait and the advice documents stay PDF, as you decided earlier (the handoff describes landscape / xlsx / csv).
- View Docs in the approver's Vendor Payable works: pop-up requests are no longer redirected away for the approver.

### Responsive layout and shell

Breakpoints are CSS media queries in `wwwroot/css/site.css` (the "Responsive" section), with a small script in `wwwroot/js/site.js` for the drawer, filter sheet and cart.

| Width | Behaviour |
|---|---|
| under 480px | header title hidden |
| under 560px | header search hidden |
| under 768px (phone) | â˜° drawer (260px); no KPI tiles; full-width pop-ups |
| under 1100px | filters open as a bottom sheet (scrim, Ã— or Esc closes it); the toolbar wraps; the cart is a bottom sheet |
| under 1280px | the menu is an overlay drawer (216px; 260px on phones), closed by default; it closes on an outside tap, Esc or a menu click; **every report row is a card** (header with chevron/tick and the first fields, caption-and-value body, footer with the actions), so a wide table never scrolls sideways |
| under 768px (pop-ups) | pop-up tables become stacked cards (each value labelled with its column), and printable A4 sheets shrink to fit the screen width |
| 1280px and up | fixed rail that collapses to icons (clicking the main area collapses it; the toggle brings it back), inline filters, side cart |

The header is 48px with the logo, title, search and bell. The signed-in user's avatar, name and role sit at the bottom of the rail. KPIs are a strip of one-line 34px tiles. Every printable document (Advance Advice, RTGS letters, Payment Advice) is portrait A4 and scrolls sideways inside its frame on small screens.

Report state (search, view, filters, sort, page) is kept in the query string. The period filter (on the right of each report's title row) is kept in a cookie. The signed-in user comes from the OTP login (see Login).

### Advance Report: two-step payment and Bulk Payment (24–26 Oct 2026)

- **Rows:** only advances approved at both L1 and L2. **Tiles:** Total Requests, Completed Advances (UTR added), Pending Advances, Total Advance Paid (UTR added only), Outstanding Balance.
- **Columns:** `Ticket Info · Passenger Info · Tour Period · Service Details · Service Start · Due Date · Service Price Info · Adv. Requested · Requested By · Approvals · Payment Status · Action`.
- **Payment is two steps.** (1) **Add Payment Details** collects the TDS %, Date of Advance, Cheque Type (Physical Cheque / E-Cheque), Payment Mode (RTGS / NEFT / IMPS), Bank A/C (STPL) and the Cheque No. (Request No. for an E-Cheque); there is no UTR yet. Saving enables **Print Advice** (the real Advance Advice, with the details filled in and the UTR left blank) and **Print Cheque** (still a toast). (2) The button becomes **Update Payment Details**: the same pop-up, pre-filled, with a mandatory **UTR / Transaction No.**, then **Mark as Paid**. An advance is *Paid* only once it has a UTR; until then the chip reads `Pending · Details added · UTR awaited`. Paid rows show **View Payment Details**.
- The pop-up header shows the ticket, MBK and file nos., vendor and legal name, service, total vendor payable, **Advance Paid Earlier** (count and amount of advances already paid with a UTR) and **Balance Pending**.
- **Bulk Payment:** the toolbar button shows tick boxes on every approved advance without payment details; **Proceed (n)** (or **Cancel**) opens the **Bulk Payment** pop-up with a totals strip (advances, total advance, TDS, net), a table with a **TDS % per advance**, and the shared payment details (Date of Advance, Cheque Type, Payment Mode, Bank A/C (STPL), Cheque / Request No., Random No.; no UTR). **Submit** saves the details on each advance (still *Pending*) and creates a **Bulk Payment Advice** (`BPA-2610-201`, `-202`, …) with two PDF letters, **Bulk Payment Internal** and **Bulk Payment Bank**, built from the RTGS letter views under the name "Bulk Payment" (amounts are the net payable; the beneficiary is the vendor's legal name).
- **Bulk Payment History (n)** lists every advice with **View Internal Copy**, **View Bank Copy**, **Both (PDF)** and **Update UTR / Transaction**, which has a UTR box per advance; **Save UTR (n)** marks each advance with a UTR as Paid (partial saves are fine).
- Files: `Areas/Finance/Controllers/AdvanceController.cs`, `Views/Advance/Popups/{AdvancePayment,BulkPayment,BulkHistory,BulkUtr}.cshtml`, `FinanceStore.PayAdvance / CreateBulk / SaveBulkUtrs`, and `site.js` (bulk mode, TDS rows, UTR counter).

### Recording payments (Payment Request)

**Update Payment Details** (Payment Request) asks for the Payment Date (not in the future), the amount (pre-filled with the approved payable), a payment slip and a reference number: a Cheque No. for the Cheque mode, a UTR for NEFT, RTGS and IMPS, neither for Cash. RTGS Advice History has an **Update Payment Status** button per advice (shown while any ticket is unpaid) that records the same details against every unpaid ticket in the advice, each paid its own approved payable. Row actions in the Payment Request Report are **Print Advice**, **Print Cheque** (toast only) and **Update Payment Details**.

## Not wired yet

- Uploaded bill files and payment slips: only the file name is stored.
- Document preview: shows a placeholder.
- Export, Print Cheque and the Voucher No. link: only show a confirmation toast.
- Header search and notifications: visual only, as in the design.

