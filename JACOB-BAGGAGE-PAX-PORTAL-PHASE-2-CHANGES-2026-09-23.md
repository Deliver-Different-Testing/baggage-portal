# Jacob — Baggage Portal Phase 2 changes (2026-09-23)

## Source
Steve, 23 Sep 2026. Code reviewed at `710bdcc` (GitLab `master` as mirrored to GitHub today).

## Why
Baggage is rolling out nationwide on **economy run** as the base speed. Outside economy-run
coverage (e.g. Hamilton) the delivery has to go on a **schedule**, priced on the schedule, with the
schedule's windows shown to the passenger.

Today a passenger who changes their address to somewhere schedule-only either dead-ends into the
"contact the airline" email, or has the speed/schedule swapped on the existing job with Auckland
economy-run windows and the original price. There is also no way to change the address after
confirming.

This note supersedes the schedule-fallback item (§6) in
`JACOB-BAGGAGE-PAX-PORTAL-DELIVERY-WINDOW-UPDATE-2026-08-13.md`, which is still outstanding.

---

## Decisions (Steve, 23 Sep)

1. **Airline pays the difference.** No approval step. The passenger never sees a price. The
   price change must be traceable for invoicing.
2. **Service order for a new address:** if the address is covered by a schedule, offer the
   schedules **before** Standard / Road Run. Economy Run stays the default where the address is
   still inside economy-run coverage.
3. **Rebook path:** book the new job through the **job book API** (the step after get-rates),
   then cancel/void the original. No in-place speed swap when the service changes.
4. **Address change after confirmation:** allowed up to **30 min before book time**
   (`Despatch:BookingLeadTimeMinutes`), **for all of NZ including Auckland**.

---

## Current state (what exists / what's wrong)

| Area | Where | State |
|---|---|---|
| Rates lookup for new address | `ServiceAvailabilityService.GetAllowedServicesAsync` → `BagDel_stpAvailableServices` | ✅ Keep |
| Composite schedule id decode (`scheduleId*1000+speedId`) | `ServiceAvailabilityService.cs:69`, `CandidateService.SpeedId` | ✅ Keep |
| Service picker UI + `scheduleId` passed to timeslots | `ServiceSection.tsx`, `useConfirmForm.ts:140-152` | ✅ Keep, reorder |
| **Name filter kills schedules** | `AllowedServicePolicy.cs:42` — non-`"Standard"` names rejected *before* the `IsScheduled` branch at `:47`. Schedule rows carry the schedule name (see `PaxBookingService.cs:678`), so every schedule is dropped. Economy Run also dropped by name. | ❌ Change |
| **Timeslots ignore the schedule** | `PaxBookingService.GetTimeslotsAsync` (`:362`) uses `scheduleId` only for window minutes; windows always come from `tucClient.EconomyRun1..8` / `tblEcoSetting` | ❌ Change |
| **No repricing** | `CandidateService` drops `Rate`/`SaleRate`; `ConfirmAsync` never touches `UcjbAmount`/`Reprice` | ❌ Replaced by rebook |
| **Service change = in-place update** | `ConfirmAsync` `ExecuteUpdateAsync` sets `UcjbSpeed`/`ScheduleId`/`ScheduleName` (`:702-705`) | ❌ Replace with rebook |
| **No address edit after confirm** | `AmendBookingRequest` (`PaxDtos.cs:93`) has no address; edit mode renders `ReadOnlyAddressSection` (`ConfirmForm.tsx:108`) | ❌ Add |
| Portal can't create jobs | Only updates `tucJob` | ❌ Add book-API client |

Note: nothing in the code gates editing by region — once address edit is added it applies
nationwide automatically. Don't add region logic.

---

## Required changes

### 1) Service policy: rank instead of "Standard only"

**File:** `src/BaggageDelivery.Core/Services/AllowedServicePolicy.cs`

Keep the existing hard exclusions exactly as they are:
- not green-available (`IsGreenAvailable`)
- `Name == "DIRECT"`
- `DeniedJobTypeIds` (10) — applied to `SpeedId`, so scheduled rows over a denied speed stay denied
- `DeniedSystemNames` (`UT`, `MR`)

Replace the `Name == Standard` gate with an allow-list + rank:

```csharp
public enum ServiceRank { EconomyRun = 0, Schedule = 1, Standard = 2, RoadRun = 3 }

public static ServiceRank? RankOf(CandidateService service, AllowedServiceOptions options, ClientServiceFlags flags);

public static IReadOnlyList<CandidateService> Filter(
    IEnumerable<CandidateService> candidates, AllowedServiceOptions options, ClientServiceFlags flags);
    // = hard exclusions, then RankOf != null, ordered by rank, then BookDateUtc asc
```

`RankOf` rules:
- `IsScheduled` → `Schedule` if `options.AllowScheduledServices`, else null.
- `SystemName == "ER"` → `EconomyRun` if `flags.EconomyActive && flags.EconomyRuns`.
- `SystemName == "EC"` → `EconomyRun` if `flags.EconomyActive && !flags.EconomyRuns`.
- `Name == "Standard"` → `Standard`.
- Road Run → `RoadRun`. **Road Run's system name / speed id is not in this repo** — take it from
  `tucJobType` and add it as `AllowedServiceOptions.RoadRunSystemNames` (default to the confirmed
  code) rather than hard-coding a name string.
- Anything else → null (still denied by default).

Economy Run only appears if the proc returns it green for the new address, which is what tells
us the address is still inside economy-run coverage. No separate coverage check needed.

**Default selection** — `PaxBookingService.DefaultService` currently picks `"Standard"`. Change it
to "first item of the ranked list". Frontend: preselect the first service in the picker.

**Show Standard / Road Run below schedules** as alternatives (don't hide them) — passenger may
want a different day.

**Tests to update** (`AllowedServicePolicyTests.cs`):
- `A_scheduled_non_standard_service_is_denied` → flips to allowed (rank `Schedule`).
- `Economy_run_is_denied_now_only_the_standard_speed_is_enabled` → flips to allowed when flags say so.
- Keep: DIRECT, denied speed 10, UT/MR, amber/red, `A_scheduled_service_over_a_denied_speed_is_still_denied`, `...when_scheduling_is_off`.
- Add: ordering test — ER, then schedules by `BookDateUtc`, then Standard, then Road Run.

### 2) Schedule-based timeslots

**File:** `src/BaggageDelivery.Core/Services/PaxBookingService.cs` — `GetTimeslotsAsync`

When `scheduleId` is supplied, build slots from the schedule, not the client's economy runs:

```csharp
private async Task<IReadOnlyList<TimeOnly>> LoadScheduleRunsAsync(
    int scheduleId, DayOfWeek day, CancellationToken ct);   // cached per (scheduleId, day), ReferenceDataTtl
```

- Source: `tblBulkRunSchedule` day rows for the schedule, joined to `tblBulkRunScheduleHeader`
  (exclude `RetiredUtc IS NOT NULL`). Scaffold the two tables into `BaggageDeliveryContext` via
  `efpt.config.json` (read-only use).
- Schedules are **day-specific**, so walk business days (`IDespatchCalendar`, same
  `MaxDaysWalked`/`TargetSlotCount`) and only emit a slot on days the schedule runs. Honour the
  schedule's cutoff if it has one.
- Window length stays `tucJobType.Minutes` of the underlying speed (`scheduleId` branch already
  computes `speedId`).
- Reuse `AddSlot`, `LeadTime`, day/window label formatting unchanged.
- If a schedule yields zero slots, return empty and log a warning — frontend already handles an
  empty slot list.

⚠️ **Confirm the schedule id space first.** The schedule tables are mid-migration (see
`routed-operations/docs/STEVE-REGRESSION-REVIEW-SCHEDULE-CLIENT-LINK-2026-09-09.md`): header PK
`tblBulkRunScheduleHeader.ScheduleId` vs legacy day-row PK `tblBulkRunSchedule.BulkRunScheduleId`.
Check which one `BagDel_stpAvailableServices` packs into `JobTypeID / 1000`, and key both the slot
lookup and `tucJob.ScheduleId` on the same one. Write down the answer in the MR.

### 3) Address change after confirmation (all NZ, incl. Auckland)

**API** — `src/BaggageDelivery.Api/DTOs/Pax/PaxDtos.cs`:

```csharp
public sealed record AmendBookingRequest(
    [Required] DateTime? DeliveryTimeUtc,
    int? AtlOptionId,
    [MaxLength(120)] string? AccessNotes,
    [Required, MaxLength(100)] string PassengerName,
    [MaxLength(40)] string? PassengerPhone,
    [MaxLength(100), EmailAddress] string? PassengerEmail,
    AddressDto? Address,            // new — null = unchanged
    int? ServiceJobTypeId);         // new — composite id from /services when address changed
```

`AmendBookingInput` gets the same two fields (`AddressUpdateDto? Address`, `int? ServiceJobTypeId`).

**Service** — `PaxBookingService.AmendAsync`:
- Existing window guard stays as-is (`IsPastChanging`, booked time − `LeadTime` > now, new time −
  `LeadTime` > now). That's the 30-minute rule.
- If `Address` is supplied and differs (`HasAddressChanged`), run the same guard-rail path as
  confirm: country check, `ResolveChosenServiceAsync`, `PaxServiceNotAllowedException` / "contact
  the airline" when nothing is allowed.
- Then apply the same **in-place vs rebook** rule as §4.
- Journey entry: `FieldName = "ucjbToAddr"`, old/new composed address (same as confirm).

**Frontend** — `ConfirmForm.tsx` / `useConfirmForm.ts`:
- In edit mode render the editable `AddressSection` (with `AddressGate` + service picker) instead of
  `ReadOnlyAddressSection`.
- Send `address` + `serviceJobTypeId` in the amend call only when the address was touched.
- Review modal: if the change will rebook, say so plainly ("Your delivery will move to the
  Hamilton schedule — Thu 25 Sep, 1:00 PM – 4:00 PM"). No price shown.

### 4) Cancel and rebook when the service changes

**Rule** (applies to both `ConfirmAsync` and `AmendAsync`):

| New address result | Action |
|---|---|
| Chosen `SpeedId` **and** `ScheduleId` equal the job's current `UcjbSpeed` / `ScheduleId` | In-place update (address, time, contact, ATL) — as today, minus the speed/schedule setters |
| Anything different (e.g. ER → Hamilton schedule, ER → Standard) | **Rebook** |

Remove the `UcjbSpeed` / `ScheduleId` / `ScheduleName` setters from `ConfirmAsync`'s
`ExecuteUpdateAsync`. The speed never changes on an existing job again.

**New pieces:**

```csharp
// Core/Interfaces/IJobBookingClient.cs — typed HttpClient over the job book API
public interface IJobBookingClient
{
    Task<BookedJob> BookAsync(RebookRequest request, CancellationToken ct);
}
public sealed record BookedJob(int JobId, string JobNumber, decimal? Amount);

// Core/Services/RebookService.cs
internal interface IRebookService
{
    Task<RebookResult> RebookAsync(int oldJobId, CandidateService chosen,
        AddressUpdateDto address, DateTime deliveryTimeUtc, PaxDetails pax, CancellationToken ct);
}
public sealed record RebookResult(int NewJobId, string NewToken);
```

The book API is the call that follows get-rates in the api repo — use the same request shape
the api repo sends after `BagDel_stpAvailableServices`. Its contract isn't in this repo; add it
under `Core/Http/Models` and config under `Despatch:JobBookingApi` (`BaseUrl`, auth).

**New table** (this service's first owned table — README notes it owns none today):

```sql
CREATE TABLE dbo.BagDel_JobRebook (
    RebookId        int IDENTITY(1,1) NOT NULL CONSTRAINT PK_BagDel_JobRebook PRIMARY KEY,
    OldJobId        int           NOT NULL,
    NewJobId        int           NULL,          -- null until book API returns
    OldSpeedId      int           NULL,
    OldScheduleId   int           NULL,
    NewSpeedId      int           NOT NULL,
    NewScheduleId   int           NULL,
    OldAmount       money         NULL,          -- tucJob.ucjbAmount at rebook time
    NewAmount       money         NULL,          -- from book API response
    Status          varchar(20)   NOT NULL,      -- Pending | Booked | Voided | Failed
    CreatedUtc      datetime2(0)  NOT NULL CONSTRAINT DF_BagDel_JobRebook_CreatedUtc DEFAULT SYSUTCDATETIME(),
    CompletedUtc    datetime2(0)  NULL,
    Error           nvarchar(500) NULL
);
CREATE UNIQUE INDEX UX_BagDel_JobRebook_OldJob_Active
    ON dbo.BagDel_JobRebook (OldJobId) WHERE Status IN ('Pending','Booked');
CREATE INDEX IX_BagDel_JobRebook_NewJob ON dbo.BagDel_JobRebook (NewJobId);
```

This is the price-difference trail for invoicing (decision 1), the idempotency guard, and the
old→new forward pointer for the passenger link. Don't overload `tucJob.ParentId` /
`BookingParentId` as the pointer — Despatch uses those for its own purposes. Set
`BookingParentId = OldJobId` on the new job **only** if you confirm that's safe in Despatch.

**Order of operations** (book first, void second — a failure must never leave the passenger with
no job):

1. Insert `BagDel_JobRebook` row, `Status = Pending`. Unique index rejects a double-submit.
2. Call `IJobBookingClient.BookAsync` with: client, `UcjbClientRefa` (WorldTracer ref), from
   address/suburb, new delivery address, chosen speed + schedule, delivery time, passenger
   name/phone/email, `DeliverToLeaveId`, size, 1 item.
3. Update row → `NewJobId`, `NewAmount`, `Status = Booked`.
4. Copy delivery notes (`tucNote`, DeliveryNotes type) to the new job.
5. Write a `JobDeliveryJourney` **on the new job**, `ChangeType = BaggageDeliveryBooking` (so
   `FindConfirmedAtAsync` treats it as confirmed). Comment: "Rebooked from job {OldJobNumber}
   ({old service} → {new service}) by passenger; airline charged new rate".
6. Void the old job: `ucjbStatus = 1000`, `ucjbVoid = 1`. If the book API has a cancel
   endpoint, use it instead. Journey on the old job: "Voided — rebooked as {NewJobNumber}".
7. Row → `Status = Voided`, `CompletedUtc`.
8. Return the new job's token (`IEncryptionService.EncryptId(newJobId)`).

Failures:
- Step 2 fails → row `Failed` + `Error`, old job untouched, passenger gets the existing
  "service no longer available, choose again" message.
- Step 6 fails after the new job exists → leave row at `Booked`, log at **Error** with both job
  numbers, and email `UnserviceableAddressNotifyEmail` so ops void it by hand. Never retry the
  booking.

**Guard before rebooking:** same as the amend window (`IsPastChanging`, 30-min lead time). If the
old job is already allocated to a driver/run (`UcjbCourierId > 0` or status ≥ Dispatched), don't
rebook — route to the "contact the airline" email path.

### 5) Passenger link after a rebook

The passenger's SMS link is `EncryptId(oldJobId)`. After a rebook:
- `GetSummaryAsync` / controller: if the job is void **and** `BagDel_JobRebook` has an
  active row for it, respond with the new token so the PWA redirects to `/c/{newToken}`
  (extend `useRedirectOnNotFound`, or add a `redirectToken` field on the summary response).
- Send a fresh confirmation to the passenger via the existing `SendBookingLinkAsync` with the new
  link ("Your delivery has been updated").
- Tracking link: `IJobTrackingLinkService.GetTrackingUrlAsync(newJobId)`.

### 6) Frontend service picker

- Render in the order the API returns (the API does the ranking now).
- Schedules: show the schedule name + first window (`bookDateUtc` already renders via
  `windowLabel`).
- Preselect the first item.
- Timeslots query already keys on `jobTypeId` + `scheduleId` — no change, but it will now get
  schedule windows back.

---

## Acceptance criteria

1. Auckland address, ER client, address unchanged → Economy Run, economy-run windows, no rebook (unchanged behaviour).
2. Passenger changes address within economy-run coverage → Economy Run offered first and preselected; same speed → in-place update, no rebook.
3. Passenger changes address to Hamilton (schedule-covered) → Hamilton schedule(s) listed first, Standard / Road Run below; windows come from the schedule's days/times, not Auckland runs.
4. Picking the schedule → new job booked via the book API at the schedule rate, old job voided, `BagDel_JobRebook` row with old/new amounts, journey entries on both jobs.
5. Old SMS link opens the new booking.
6. Confirmed booking (any region, incl. Auckland): address editable until 30 min before book time; after that the existing "can no longer be changed online" response.
7. Address with no allowed service → existing "contact the airline" email, no rebook.
8. Double-tap on save → one new job only.
9. Book API down → old job untouched, passenger told to choose again.

## Tests
- `AllowedServicePolicyTests` — rank/order cases above.
- `PaxBookingServiceTimeslotTests` — schedule path: day-specific rows, cutoff, lead time, empty schedule.
- `RebookServiceTests` — happy path, book failure, void failure, duplicate submit (unique index), dispatched-job guard.
- `PaxBookingServiceAmendTests` — address change in-place vs rebook, window closed.
- Integration: `PaxAmendAddressTests` against `PaxApiFactory` with a stubbed `IJobBookingClient`.
- Frontend: edit mode shows editable address; picker order; rebook wording in review modal; redirect on voided-job token.

## Confirm before starting (reply in the MR)
1. Road Run system name / `tucJobType` id.
2. Job book API contract (endpoint, auth, request/response) and whether it has a cancel/void endpoint.
3. Which schedule id `BagDel_stpAvailableServices` emits (header `ScheduleId` vs `BulkRunScheduleId`), and the day/time/cutoff columns on `tblBulkRunSchedule`.
4. Whether Despatch reprices or re-manifests anything on void that we'd double up.
5. Whether `tucJob.BookingParentId` is safe to set on the new job.

## Suggested MR split
1. Policy ranking + tests (small, can ship alone — schedules become visible).
2. Schedule timeslots.
3. `IJobBookingClient` + `BagDel_JobRebook` + `RebookService`, wired into `ConfirmAsync`.
4. Post-confirmation address edit (API + frontend) using the rebook service.
5. Link redirect + passenger re-notification.

Don't ship MR 1 to prod before MR 2 — otherwise passengers see schedules with Auckland windows.
