# Jacob — Baggage pax portal delivery-window update (2026-08-13)

## Purpose
This is an update to the earlier spec:
- `JACOB-BAGGAGE-PAX-PORTAL-CHANGES-2026-06-26.md`

This note covers the latest requested passenger-portal changes for the baggage app, based on:
- Steve's screenshot / markup
- the current **real GitLab baggage app** at `urgent-couriers/baggagedelivery`
- Jacob's latest baggage-app implementation relevant to delivery slots

---

## Executive summary
There are **five asks** here:

1. Change the header narration from **`Ref`** to **`File Reference`**.
2. Change the section heading from **`Delivery time`** to **`Delivery window`**.
3. Stop showing raw eco-run start times as if they are windows; where only a run start exists, show a **3-hour window** (example: `9:00 AM - 12:00 PM`).
4. Confirm / implement next-day slot availability when the current day is already exhausted.
5. Check Jacob's latest GitLab baggage work to see whether he already added schedule-based delivery-slot logic for jobs not covered by eco runs.

The current GitLab source shows:
- **`Delivery window` is already used in at least one place**, but **`Delivery time` still exists in the main edit section**.
- The portal is still showing **`REF · {summary.jobId}`**, not a file reference field.
- Jacob's slot-generation work currently builds windows from **`tblEcoSetting.EconomyRun1..5` only**.
- I do **not** see schedule-based fallback logic in the latest baggage-app code for deliveries not covered by eco runs.

---

## Current real implementation (GitLab)

### Frontend
File:
- `src/BaggageDelivery.PaxPortal/src/pages/PaxMobile.tsx`

Current confirmed behaviour:
- Header still renders:
  - `REF · {summary.jobId}`
- Main editable section still uses:
  - `title="Delivery time"`
- Confirmation card later in the flow already says:
  - `Delivery window`
- Validation message still says:
  - `Please pick a delivery time slot.`

### Backend
File:
- `src/BaggageDelivery.Core/Services/PaxBookingService.cs`

Current confirmed behaviour:
- `GetSummaryAsync(...)` currently returns:
  - `Reference: jobId.ToString()`
- `GetTimeslotsAsync(...)` currently:
  - reads the **first** `tblEcoSetting` row
  - uses `EconomyRun1..5`
  - pairs consecutive values into windows
    - `Run1 -> Run2`
    - `Run2 -> Run3`
    - etc
  - anchors them to **today** (or a supplied `localDate`)
  - marks the first future one as `FirstAvailable`
  - formats labels as:
    - `h:mm tt - h:mm tt`

### Relevant Jacob slot commit
Latest clearly relevant Jacob baggage commit found in GitLab:
- `f89da60` — `feat(pax): build delivery slots from tblEcoSetting EconomyRun columns`

What that commit does:
- replaces hardcoded slot stubs
- builds slots directly from `tblEcoSetting` economy-run columns
- does **not** introduce any visible schedule lookup / schedule fallback for uncovered deliveries

---

## Required changes

## 1) Change `Ref` narration to `File Reference`

### Problem
The passenger portal header is currently showing the wrong narration and the wrong value source for this use case.

Current UI:
- `REF · {summary.jobId}`

Per Steve's screenshot, this needs to show the actual baggage file reference (example given: `AKLA2633476`) and the narration should be:
- `File Reference`

### Current gap
The backend summary currently sets:
- `Reference: jobId.ToString()`

So even though the DTO has a `reference` field, the app is not currently using a real baggage reference, and the UI is also bypassing `summary.reference` and printing `summary.jobId` directly.

### Required implementation
1. Update the summary backend to return the **real file reference**.
2. Update the frontend header to render that returned reference.
3. Change the narration text from `REF` to `File Reference`.

### Likely backend fields to inspect
The EF model already exposes several candidate reference fields on `tucJob`, including:
- `UcjbClientRefa`
- `UcjbClientRefb`
- `UcjbClientRefc`
- `UcjbOurRef`
- `TextRef1..4`
- `ShopRef1..5`

Jacob should confirm which one is the baggage file reference actually used by this flow, then map that into:
- `BookingSummary.Reference`

### Files to update
- `src/BaggageDelivery.Core/Services/PaxBookingService.cs`
- `src/BaggageDelivery.PaxPortal/src/pages/PaxMobile.tsx`
- possibly `src/BaggageDelivery.Api/DTOs/Pax/PaxDtos.cs` only if the API shape changes (it may not need to)

### Acceptance criteria
- Header no longer shows `REF · 179252`-style job id text.
- Header instead shows `File Reference · AKLA2633476` (or the true mapped file reference for that job).
- The value comes from the real baggage reference field, not `jobId.ToString()`.

---

## 2) Change `Delivery time` heading to `Delivery window`

### Problem
The UI is inconsistent.

Current confirmed state in `PaxMobile.tsx`:
- one section title still says `Delivery time`
- another later card already says `Delivery window`

### Required implementation
Change the editable slot-selection section heading to:
- `Delivery window`

Also review adjacent wording so the terminology is consistent across the page.

### Files to update
- `src/BaggageDelivery.PaxPortal/src/pages/PaxMobile.tsx`

### Acceptance criteria
- No passenger-facing heading says `Delivery time` for this slot picker.
- The page consistently uses `Delivery window`.

---

## 3) Where there is only an eco-run start, show a 3-hour delivery window

### Problem
Steve's screenshot suggests the options are being interpreted as run-start times, not true customer-facing delivery windows.

Current Jacob slot logic pairs consecutive `EconomyRun` values.
That means if the data is effectively just a sequence of run starts, the passenger is seeing the eco-run timetable rather than a real promised window.

Steve's requested behaviour:
- if the run is `9:00 AM`, show:
  - `9:00 AM - 12:00 PM`

### Required implementation
For baggage pax portal delivery options that are currently derived from eco-run starts:
- treat each available run start as the **start of a customer-facing delivery window**
- compute the end from a configurable duration rather than assuming the next eco run is the boundary
- label accordingly

### Preferred implementation option
If this is easier / cleaner than manufacturing windows in baggage-app code, Jacob should add a **duration field** to the eco-run configuration table and use that to build the passenger-facing delivery window.

Recommended model:
- keep `EconomyRun1..5` as the run **start times**
- add a duration field representing the window length for each run
- baggage portal then shows:
  - `windowStart = runStart`
  - `windowEnd = runStart + configuredDuration`

Minimum acceptable first pass if he wants one simple field:
- one shared eco-run duration value for the tenant / eco-setting row
- example: `180` minutes

Better long-term option if operations need different windows per run:
- per-run duration fields aligned to each `EconomyRunN`

### Important decision
Jacob needs to confirm whether the current `EconomyRun1..5` values are:
1. actual consecutive window boundaries, or
2. just run start times

For this baggage flow, Steve's direction is that these should behave as customer-facing windows, and if easier than coding `+3 hours` on the fly, the configuration should explicitly carry a duration.

### Required code change
Current code:
- builds `Run1 -> Run2`, `Run2 -> Run3`, etc.

Required code for this flow:
- stop assuming the next run start is the passenger window end
- derive end from duration
- first acceptable implementation:
  - `Run1 -> Run1 + duration`
  - `Run2 -> Run2 + duration`
  - etc

while still excluding slots already expired for the relevant date.

### Files to update
- `src/BaggageDelivery.Core/Services/PaxBookingService.cs`
- possibly `src/BaggageDelivery.Core/Services/BookingTimeSlot.cs` if the slot shape needs extending (probably not)

### Acceptance criteria
- If an eco run starts at `9:00 AM`, the passenger sees `9:00 AM - 12:00 PM`.
- The page is no longer just echoing raw run-start behaviour as a pseudo-window.
- `FirstAvailable` still correctly identifies the next valid slot.

---

## 4) Check next-day availability at end of day

### Problem
Steve wants this checked explicitly:
- if the current day is effectively over, does the portal show the next day as well?

### Current code position
`GetTimeslotsAsync(int jobId, DateTime? localDate, ...)` supports a supplied `localDate`, and if absent it anchors to tenant **today**.

But the current passenger UI call is simply:
- `getTimeslots(bookingId)`

There is no confirmed passenger-side logic here that:
- retries with tomorrow
- requests multiple dates
- or automatically rolls to next day if today's windows have all ended

So based on the current code read, this behaviour is **not proven** and should be treated as an explicit implementation check / likely gap.

### Required implementation
If all today's windows are already expired in tenant local time:
- the passenger should still be offered valid future windows
- at minimum, tomorrow's delivery windows should appear automatically

### Recommended implementation options
#### Option A — backend fallback (recommended)
If `localDate` is null and today's computed slots are all expired / non-selectable:
- automatically compute and return tomorrow's slots instead

#### Option B — frontend retry
If today's API response is empty or fully stale:
- call the timeslot endpoint again with `date = tomorrow`

### Recommendation
Do this in the **backend** so the passenger app gets valid slots by default without extra client orchestration.

### Files to update
- primary: `src/BaggageDelivery.Core/Services/PaxBookingService.cs`
- optional if client-assisted: `src/BaggageDelivery.PaxPortal/src/api/pax.ts`, `src/BaggageDelivery.PaxPortal/src/pages/PaxMobile.tsx`

### Acceptance criteria
- Late in the day, when today's windows are no longer usable, the portal still shows next-day delivery windows.
- Passenger does not get stranded with an empty or misleading slot list.

---

## 5) Check Jacob's latest GitLab commit for schedule logic outside eco runs

### Result of review
I checked Jacob's latest baggage-app commits in GitLab.

The relevant slotting commit is:
- `f89da60` — `feat(pax): build delivery slots from tblEcoSetting EconomyRun columns`

That implementation:
- reads `tblEcoSetting`
- uses `EconomyRun1..5`
- formats those into windows

I do **not** see code in the latest relevant baggage implementation that:
- looks up route schedules
- checks delivery schedules for jobs not covered by eco runs
- falls back to any schedule table / schedule assignment when eco runs do not cover the delivery

### What this means
Right now the baggage passenger slot logic appears to be:
- **eco-run driven only**

So if the business needs deliveries outside eco-run coverage to source windows from schedules, that is still outstanding and should be treated as **new work**, not as something already done in Jacob's latest baggage commit.

### Required follow-up
Jacob should explicitly confirm whether the intended non-eco source is:
- job schedule (`ScheduleId` / `ScheduleName`)
- route roster / dispatch schedule
- another operational timetable source

Then implement fallback rules for jobs not covered by eco runs.

### Acceptance criteria
- If a delivery is not covered by eco-run timing, the portal still derives a valid delivery window from the agreed schedule source.
- This logic is visible in code, not assumed.

---

## Recommended implementation order

1. **Fix file reference mapping + narration**
2. **Rename `Delivery time` to `Delivery window` everywhere**
3. **Change eco-run slot labels to duration-based windows**
   - preferred: add duration to eco-run config and use that
   - fallback: hardcode `+3 hours` only if schema change is not worth it
4. **Add automatic next-day fallback when today's windows are exhausted**
5. **Implement / confirm schedule fallback for non-eco deliveries**

---

## Exact file targets

### Frontend
- `src/BaggageDelivery.PaxPortal/src/pages/PaxMobile.tsx`
- `src/BaggageDelivery.PaxPortal/src/api/pax.ts` *(only if frontend helps with date fallback)*
- `src/BaggageDelivery.PaxPortal/src/api/client.ts` *(only if request/DTO shape changes)*

### Backend
- `src/BaggageDelivery.Core/Services/PaxBookingService.cs`
- `src/BaggageDelivery.Core/Services/BookingSummary.cs`
- `src/BaggageDelivery.Api/Controllers/Pax/PaxBookingController.cs` *(only if response behaviour changes)*
- `src/BaggageDelivery.Api/DTOs/Pax/PaxDtos.cs` *(only if API shape changes)*

---

## Done when
- Passenger header shows **File Reference** and the real baggage reference value.
- The slot-picker section says **Delivery window**.
- Eco-run-driven options display real customer-facing 3-hour windows.
- The portal can still show a valid next-day option set when today is finished.
- Jacob has either implemented non-eco schedule fallback or explicitly documented that it is still outstanding.

---

## Verification note
This update was written against the **real GitLab baggage app**, not the older GitHub mirror:
- GitLab repo: `urgent-couriers/baggagedelivery`
- inspected master head during review: `ddcffa9`
- latest clearly relevant Jacob slot commit: `f89da60`
