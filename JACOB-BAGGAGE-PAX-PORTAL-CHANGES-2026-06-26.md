# Jacob — Baggage pax portal changes (2026-06-26)

## Goal
Make the passenger baggage-confirmation flow match the requested UX changes:

1. airline-specific colours by airline code
2. auto-fill the on-hold job delivery address
3. Authority to Leave options/behaviour aligned with the booking app

This brief is based on the **real current GitLab source** in `urgent-couriers/baggagedelivery`, not the older GitHub mirror.

---

## Current real implementation

### Frontend
- `src/BaggageDelivery.PaxPortal/src/pages/PaxMobile.tsx`
  - header shows `summary.airlineLabel`
  - address fields are driven from `summary.deliveryAddress`
  - ATL UI is a switch + radio group driven from `summary.atlOptions`
- `src/BaggageDelivery.PaxPortal/src/styles/theme.ts`
  - app currently exports a single default blue theme via `export const theme = lightTheme`
  - comment already notes that a per-tenant override can be wired later
- `src/BaggageDelivery.PaxPortal/src/main.tsx`
  - mounts a fixed `ThemeProvider theme={theme}`

### Backend
- `src/BaggageDelivery.Core/Services/PaxBookingService.cs`
  - `GetSummaryAsync(...)` currently returns:
    - `AirlineLabel` from `tucClient.UcclName`
    - `AtlOptions` from `TblJobLeaveNotHomes`
    - **empty delivery address** (`Line1 = ""`, `City = ""`)
  - `ConfirmAsync(...)` writes the submitted address back to:
    - `tucJob.DeliveryAddressLine1`
    - `tucJob.DeliveryAddressLine2`
    - `tucJob.DeliveryAddressLine3`
    - `tucJob.DeliveryAddressLine4`
    - `tucJob.DeliveryAddressLine5`
    - `tucJob.DeliveryAddressLine6`
    - `tucJob.DeliveryLatitude`
    - `tucJob.DeliveryLongitude`
- `src/BaggageDelivery.Core/Models/TucJob.cs`
  - has `UcjbClientCode`
  - has `DeliveryAddressLine1..8`
- `src/BaggageDelivery.Core/Models/TucClient.cs`
  - has `UcclCode`
  - has `UcclName`

---

## Required changes

## 1) Airline-specific colours by airline code

### Problem
The passenger app currently renders with one fixed blue theme. The page shows airline name, but not airline branding.

### Required implementation
Use **airline code** as the branding key, not airline display name.

### Backend changes
Update `src/BaggageDelivery.Core/Services/PaxBookingService.cs` so the booking summary includes a stable airline code.

#### Add to the summary query
Include the client code from `tucJob` / `tucClient`:
- `j.UcjbClientCode`
- optional fallback: `j.UcjbClient.UcclCode`

#### DTO/API shape
Extend the booking summary payload to include a new field:
- `airlineCode`

Files to update:
- `src/BaggageDelivery.Core/Models/BookingSummary.cs` *(or the actual summary record/class used by `GetSummaryAsync`)*
- `src/BaggageDelivery.Api` response contract if separated
- `src/BaggageDelivery.PaxPortal/src/api/client.ts`

### Frontend changes
Create a small airline-brand map keyed by code, e.g.:
- `NZ`
- `QF`
- `SQ`
- fallback `DEFAULT`

Suggested new file:
- `src/BaggageDelivery.PaxPortal/src/styles/airlineBranding.ts`

Suggested shape:
```ts
export type AirlineBrand = {
  primary: string
  primaryDark: string
  primaryLight: string
  contrastText: string
}
```

Then:
1. create a helper `getAirlineBrand(airlineCode?: string)`
2. update `theme.ts` so a theme can be created from a supplied brand palette
3. update `main.tsx` / app shell so theme is chosen from booking data instead of using one fixed exported theme

### Minimum acceptable behaviour
- if airline code is known, render its configured colours
- if airline code is unknown/missing, fall back to current blue theme
- do **not** key branding off `airlineLabel` free text

---

## 2) Auto-fill the on-hold job address

### Problem
The current summary API intentionally returns a blank address object even though the confirmed submit path writes back to real `tucJob` delivery address fields.

That means the passenger sees empty address fields instead of the address already sitting on the on-hold job.

### Required implementation
When loading the booking summary, populate `deliveryAddress` from the existing job address fields.

### Backend source of truth
Read from `tucJob`:
- `DeliveryAddressLine1` → line 1
- `DeliveryAddressLine2` → line 2
- `DeliveryAddressLine3` → suburb
- `DeliveryAddressLine4` → city
- `DeliveryAddressLine5` → postcode
- `DeliveryAddressLine6` → country
- `DeliveryLatitude` / `DeliveryLongitude` if present

### File to change
- `src/BaggageDelivery.Core/Services/PaxBookingService.cs`

### Required behaviour
- if the job already has a delivery address, show it on first load
- passenger can still edit it before confirmation
- if some fields are missing, prefill whatever exists and leave the rest editable
- keep current address autocomplete behaviour in `PaxMobile.tsx`

### Important note
This should be a **read fix only**. No schema change required.

---

## 3) Authority to Leave options should match the booking app

### Current state
`PaxBookingService.GetSummaryAsync(...)` already loads ATL options from:
- `TblJobLeaveNotHomes`
- filtered with `AllowLeave`
- ordered by `Sequence`, then `Name`

`PaxMobile.tsx` renders those options as:
- master switch
- then radio buttons when enabled

### Required implementation
Keep `TblJobLeaveNotHomes` as the source of truth, but make the baggage flow match the booking app in labels/selection behaviour.

### What Jacob should verify / adjust
1. **Labels**
   - use the exact `TblJobLeaveNotHomes.Name` values shown in booking
   - no baggage-specific renamed labels unless product explicitly wants divergence
2. **Ordering**
   - preserve `Sequence` order exactly
3. **Off state**
   - current off state is `None`
   - confirm this matches booking app semantics; if booking app uses a different null/off value, align baggage to that
4. **Submit value**
   - current confirm path parses `input.AtlOption` as an `int` and writes `DeliverToLeaveId`
   - make sure frontend sends the selected ATL option in a form that survives this parsing cleanly

### Important bug-risk callout
Right now the frontend stores ATL state as the **option name**, while `ConfirmAsync(...)` tries to parse `input.AtlOption` as an integer:

```csharp
var leaveId = int.TryParse(input.AtlOption, out var parsed) ? parsed : (int?)null;
```

So if the frontend posts the option name instead of the option id, `DeliverToLeaveId` will be null.

### Fix required
Change the frontend to store/post the ATL **id**, not the label text.

Files to update:
- `src/BaggageDelivery.PaxPortal/src/pages/PaxMobile.tsx`
- `src/BaggageDelivery.PaxPortal/src/api/client.ts`
- any confirm request DTO on the API/backend side if needed

Recommended shape:
- keep label for display
- post `atlOptionId: number | null`
- map that cleanly to `DeliverToLeaveId`

This is the most important functional fix in this brief, because it affects persisted ATL selection, not just UI appearance.

---

## Suggested implementation order

1. **Fix ATL id posting first**
   - avoids silently dropping `DeliverToLeaveId`
2. **Fill delivery address from `tucJob`**
   - immediate UX win, low risk
3. **Add airline code to summary + brand map**
   - then wire dynamic theme

---

## Acceptance checklist

### Address
- open a baggage confirmation link for an on-hold job with an existing address
- address fields load prefilled
- editing still works
- confirm still persists edits back to `tucJob`

### ATL
- ATL options match booking app labels/order
- selecting an ATL option persists a non-null `DeliverToLeaveId`
- turning ATL off clears the selection cleanly

### Branding
- NZ/QF/SQ (or whichever airline codes are configured) render their own colour theme
- unknown airline codes fall back safely to the existing blue theme
- no runtime crash if `airlineCode` is missing

---

## Exact file targets

### Backend
- `src/BaggageDelivery.Core/Services/PaxBookingService.cs`
- `src/BaggageDelivery.Core/Models/BookingSummary.cs` *(or equivalent summary DTO/record actually used in this repo)*
- any confirm request DTO carrying ATL selection

### Frontend
- `src/BaggageDelivery.PaxPortal/src/api/client.ts`
- `src/BaggageDelivery.PaxPortal/src/pages/PaxMobile.tsx`
- `src/BaggageDelivery.PaxPortal/src/styles/theme.ts`
- `src/BaggageDelivery.PaxPortal/src/main.tsx`
- `src/BaggageDelivery.PaxPortal/src/styles/airlineBranding.ts` *(new suggested file)*

---

## Final note
The GitHub `baggage-portal` repo is behind the real GitLab baggage app, so use this MD as the handover brief, but implement against the current GitLab source:
- GitLab repo: `urgent-couriers/baggagedelivery`
- inspected head: `ddcffa9` (`Merge branch 'ci/pipeline-setup' into 'master'`)
