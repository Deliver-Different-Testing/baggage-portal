# Jacob — Baggage pax portal tweaks from Deane email (2026-08-18)

## Source
Deane email to Steve, 18 Aug 2026, subject `Baggage App`.

Deane’s two review notes were:

1. `We need to show the info from the Extra delivery information on the main screen and the confirmation screens.`
2. `Show our job number as the Booking Reference`

---

## Why this is a separate follow-up note
Yesterday’s tweak note (`JACOB-BAGGAGE-PAX-PORTAL-BOOKING-FLOW-TWEAKS-2026-08-17.md`) recommended keeping `Extra delivery information` hidden in the default compact address state unless the user chose to edit.

Deane’s feedback overrides that recommendation.

Treat this document as the newer follow-up and implement it on top of the earlier booking-flow tweaks.

---

## Required changes

### 1) Show `Extra delivery information` on the main screen

#### Problem
The portal currently does not surface the extra delivery info prominently enough.

That means baggage-specific delivery instructions can be missed by the passenger during booking/review.

#### Required behaviour
Show the `Extra delivery information` value on the main booking screen as part of the visible address / delivery details block.

This should be visible in the normal passenger flow without requiring the user to open an edit state first.

#### UX intent
This is supporting delivery clarity, not optional debug data.

If the booking has extra delivery instructions, the passenger should be able to see them clearly while reviewing the address.

#### Display expectation
Use the existing label text:
- `Extra delivery information`

If the field is empty, do not show an awkward blank placeholder.

#### Acceptance criteria
- Main booking screen shows `Extra delivery information` when present.
- It is visible in the normal review path, not only inside edit mode.
- Empty values do not create noisy blank UI.

---

### 2) Show `Extra delivery information` on the confirmation screens

#### Required behaviour
Wherever the passenger confirms the booking details, include `Extra delivery information` there too.

This applies to the confirmation step/screens, not just the initial booking form.

#### Acceptance criteria
- Confirmation screen(s) also show `Extra delivery information` when present.
- The value shown matches what was entered/saved on the booking.
- The field is omitted cleanly when blank.

---

### 3) Show Urgent job number as the `Booking Reference`

#### Problem
The current booking reference being shown is not the one Deane wants surfaced to the customer.

#### Required behaviour
Display the Urgent job number as the passenger-facing `Booking Reference`.

The label should remain:
- `Booking Reference`

But the value under that label should be the Urgent / internal job number.

#### Implementation intent
This is the number operations and the customer can most usefully refer to.

So the display value should come from the actual Urgent job number used operationally, not a less useful alternate identifier.

Jacob should map this from the real baggage booking payload/source field already holding the Urgent job number.

#### Acceptance criteria
- `Booking Reference` displays the Urgent job number.
- The same value is used consistently anywhere the booking reference is shown in the passenger flow.
- No stale/alternate reference is left in one screen while another screen uses the job number.

---

## Relationship to the 17 Aug tweak note
Implement this follow-up **in addition to** the earlier booking-flow tidy-up, with one explicit override:

### Override
The 17 Aug note said:
- keep `Extra delivery information` out of the default compact state unless editing

This 18 Aug Deane review now overrides that:
- `Extra delivery information` must be shown on the main screen and confirmation screens when present

Everything else from the earlier tweak note still stands unless Jacob finds a direct conflict in the real UI.

---

## Suggested implementation order
1. identify the real source field for the Urgent job number currently available to the pax portal
2. swap `Booking Reference` display to that job number everywhere in the passenger flow
3. surface `Extra delivery information` in the main booking/review screen
4. surface the same field in the confirmation screen(s)
5. verify empty-state behaviour stays clean

---

## Done check
This is done when all of the following are true:
- `Extra delivery information` shows on the main booking/review screen when present
- `Extra delivery information` shows on the confirmation screen(s) when present
- empty extra-info values are omitted cleanly
- `Booking Reference` shows the Urgent job number
- the same booking-reference value is used consistently across the passenger flow
- the new behaviour sits cleanly on top of the 17 Aug booking-flow tweaks
