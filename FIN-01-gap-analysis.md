# FIN-01 Vendor Payable Report: FSD vs built UI (gap analysis)

- **FSD:** `docs-claude/planned/specs/FIN-01.md` (v0.1 Draft, 2026-10-27)
- **UI compared:** `SouthernTravels.Finance`: `VendorPayableReport.cs`, `VendorPayableController.cs`, `Raise.cshtml`, `ViewBill.cshtml`, `FinanceArea.cs`, `README.md`
- **Not done:** the app was not run; `site.js` was not opened.

Summary: the UI builds most of the User view (list, Enter bill) plus extras the FSD does not mention. It has no Approver view.

## 1. Built and matching the FSD
- Vendor Wise and Ticket Wise tabs, with services under each row.
- Fields shown: contracted, negotiated and selling price, advance paid, balance, bill status, L1/L2 approval chips.
- Enter bill: bill date, bill no., invoice amount, bill document. A saved bill starts at L1 Waiting ("awaiting L1").
- Reupload restarts the approval chain and keeps earlier versions visible.
- Search and paging.

## 2. Missing (FSD requires it, UI does not have it)

| FSD item | Status in UI |
|---|---|
| Approver view (screen 4): L2 queue with age and L1 remarks; Approve; Reject with required reason (AC7-AC10, AC14) | Not built. No approver screen, no Approve/Reject action, nothing changes L1 or L2 status. Chips are display only. |
| L1 approve / reject (open question 4) | Not built. |
| Record payment on this report (screen 3): amount, date, mode, reference; Paid / Part paid | Not on this report. Payment lives in Advance and Payment Request. Only a Payment Status column shows here. |
| Export to Excel and CSV (AC15) | Button shows a toast only. |
| Grand total and per-vendor totals for all money columns | Not built. Six KPI tiles and vendor-line sums only; no grand-total row; no totals for Variation, Net payable, Bill amount. |
| Sortable columns | None sortable. |
| Audit and history (rule 9, AC13): user, date, time, remarks per action | Not built. Bills store no acting user, remarks or approver names; approval detail is a tooltip only. |
| Drill-downs: bill status to approval history, paid amount to payment list | Not built. |
| Duplicate bill-number block per vendor (AC5) | Not built. Controller has no check. |
| Same bill saved twice (double-click, refresh) | No protection. |
| Server-side roles and row scope (AC4, ACC-01/02) | Not built. Every signed-in user can use every action. |
| Notifications (mail to L2 on arrival, mail to Finance on reject) | Not built. |
| Bill date not in the future; file type and size checks | Not enforced in the Raise pop-up. |

## 3. Built differently from the FSD
- **Variation:** FSD says negotiated minus contracted (open question 5). UI uses bill amount minus negotiated.
- **Net payable:** no Net payable column. Total Business is the sum of negotiated amounts; Balance Pending is the nearest equivalent.
- **Bill vs payable (open question 7):** UI blocks Submit when the invoice is above the payable and forces a dispute. FSD says flag the difference.
- **Edit bill:** FSD allows it only before L1 acts or after a rejection. UI allows Reupload whenever a bill exists.
- **Bill status:** FSD has 8 statuses (Not entered, Entered, L1 approved/rejected, L2 approved/rejected, Paid, Part paid). UI shows Pending / Uploaded / Disputed plus separate L1 and L2 chips.
- **Grouping:** FSD nests vendor > ticket > service. UI has two tabs of two levels each.
- **Filters:** FSD has date range, Vendor, Vendor type, Ticket/MBK/File, Branch, Bill status, Payment status, Vendor payment type. UI has no Payment status filter and no BTC/Advance filter; Vendor and Ticket/MBK/File are reachable only via the search box.
- **Date range:** FSD requires one, on service start date. UI's is optional and uses overlap (period defaults to This Month).
- **Columns:** no Bill No. column, no L1/L2 user and date columns, no separate Paid amount column.
- **Cancelled services (open question 11):** cancelled tickets stay in the list at full value; no cancellation-charge handling.

## 4. In the UI but not in the FSD
- Scan button
- Dispute flow with mismatch reason
- Additional docs upload
- Voucher No. link
- Vendor legal name
- Accommodation meal, room and occupancy details
- "Disputed Tickets" KPI
- Visibility rules (tickets hidden when pending or any service unconfirmed; OPS status New excluded)
- Booking Executive and Service City filters

The BA should add these to the FSD or confirm they are out of scope.

## 5. Caveats
- Many gaps depend on the FSD's 17 open questions (formulas, who holds L1/L2, BTC vs Advance, cancelled services, part payments). Until answered, the variation, net payable and bill-vs-payable differences cannot be called wrong.
- Scope is FIN-01 only. FIN-03 Approved Vendor Payable, APR-04 Bill Upload Approval and FIN-04 Payment Request are separate items and may be where the Approver view belongs (open question 1).
