# Jacob — Baggage pax portal booking-flow tweaks (2026-08-17)

## Purpose
This is a small follow-up change note for the baggage passenger portal based on:
- Deane's review comments
- Steve's markup / screenshot
- the current baggage pax portal behaviour

This should be treated as a tight UX tidy-up on top of the earlier baggage portal work, not a new product direction.

Related earlier doc:
- `JACOB-BAGGAGE-PAX-PORTAL-DELIVERY-WINDOW-UPDATE-2026-08-13.md`

---

## Executive summary
There are four requested changes:

1. **Remove the phone number from the bottom of the page.**
2. **Change the countdown timer** so it counts from the current time to the **start time of the first available run**, with the note:
   - `Make sure you confirm your booking before the chosen run time`
3. **Remove `Letter Box` as a delivery-location option** and make **`Front Door` the default**.
4. **Change the address-edit UX** so the already-known address is treated as the default path, with editing behind an explicit `Edit` action because most bags will go to the saved address.

---

## 1) Remove phone number from the bottom

### Required change
Remove the passenger phone number display from the bottom area of the booking page.

### Intent
This is unnecessary noise in the confirm flow and does not need to be visible there.

### Acceptance criteria
- The bottom section no longer shows the passenger phone number.
- Removing it does not leave an awkward blank gap or broken spacing.

---

## 2) Countdown timer must count down to the first run start time

### Problem
The countdown behaviour needs to be anchored to the **start time of the first available run**, not to some other page/session timing concept.

### Required behaviour
The timer should:
- use the **current local time** as the countdown start
- count down to the **start time of the first available run / first selectable slot**
- clearly communicate urgency around confirming before that run starts

### Required note
Add this note beside or under the timer:
- `Make sure you confirm your booking before the chosen run time`

### Implementation intent
If the first selectable delivery option starts at `9:00 AM`, the countdown should reflect the remaining time from **now** until `9:00 AM`.

This is about the **selected/first available run start boundary**, not the delivery-window end.

### Acceptance criteria
- Countdown is based on `now -> first available run start`.
- The supporting note is visible and uses the wording above.
- If the first run has already passed, it should not count down to an expired run.
- The countdown stays aligned with the same slot-selection logic used by the page.

---

## 3) Remove `Letter Box` and default to `Front Door`

### Required change
In the delivery-location / leave-location options:
- remove `Letter Box` as a selectable option
- make `Front Door` the default option

### Intent
`Letter Box` is not a sensible baggage-delivery default/path here.
`Front Door` should be the normal preselected handoff point.

### Acceptance criteria
- `Letter Box` is no longer shown anywhere in the passenger-facing delivery-location choices.
- `Front Door` is preselected by default.
- Existing validation/submission logic still works when `Front Door` remains unchanged.

---

## 4) Change the address-edit UX so the saved address is the default path

### Problem
Most baggage deliveries will go to the address already on the booking, so the UI should not force the passenger through a full edit-style address form by default.

### Required UX direction
Use the existing saved address as the primary/default state.

#### Default state
Show a compact confirmation block first:
- `This address is correct`
- helper copy: `We'll deliver your bag here.`
- visible `Edit` control on the right

In this default state, show the key saved address fields as read-first / confirm-first content rather than leading with full edit inputs.

#### Edit state
If the passenger clicks `Edit`, then expand/show the editable address form.
That editable state should include:
- Search address
- Street address
- Extra delivery information
- Suburb
- City
- Postcode
- Country

### Important UX intent
The flow should assume:
- **95% of bags will go to the address already entered**
- editing is the exception path, not the primary path

So Jacob should optimise for:
- confirm existing address first
- edit only when needed

### Extra delivery information
Steve's markup explicitly questions whether this needs to be shown by default.
Recommendation for this change:
- **do not show `Extra delivery information` in the default collapsed state**
- only show it when the user enters the edit path

That keeps the primary flow compact while still preserving the field when needed.

### Acceptance criteria
- The page no longer opens with the full address-edit experience as the primary/default presentation.
- The saved address is treated as the default destination path.
- `Edit` switches the user into the editable form state.
- `Extra delivery information` is not shown in the default compact state.
- Expanded edit state still allows full address correction when needed.

---

## Implementation notes for Jacob

This should be treated as a **UX tidy-up pass** on the real baggage pax portal, not a broad rewrite.

Priority order:
1. remove bottom phone number
2. correct countdown logic + message
3. remove `Letter Box`, default `Front Door`
4. reshape address section to confirm-first / edit-second

Keep the new behaviour aligned with the current passenger booking flow and avoid introducing extra steps for the common path.

---

## Done check

This change is done when all of the following are true:
- phone number is removed from the bottom of the page
- countdown targets the first available run start time from the current time
- note is visible: `Make sure you confirm your booking before the chosen run time`
- `Letter Box` is removed
- `Front Door` is the default option
- address UX is confirm-first with editing behind `Edit`
- expanded edit state still supports full address changes cleanly
