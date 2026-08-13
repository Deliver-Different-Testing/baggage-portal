# BaggageDelivery

Passenger-facing PWA + ASP.NET Core service for lost-baggage delivery
confirmation and tracking. SITA WorldTracer pushes a Baggage Delivery
Order (BDO) into IntegrationManager, which creates an ON HOLD courier job
in Despatch and then POSTs to this service's admin API to mint a passenger
link. The passenger opens the PWA, confirms the delivery address, picks a
time slot, sets Authority-to-Leave, and the courier job flips out of ON
HOLD (ready for dispatch) in the same `PATCH api/Jobs/{id}/delivery`
that records the address. They then follow the live tracking page.

This service does **not** talk to SITA or own a copy of the booking —
`tucJob` in the Despatch DB is the canonical record. The URL encrypts the
`JobId`; reads and writes hit Despatch directly via EF Core
(`BaggageDeliveryContext`). One BaggageDelivery deployment per tenant,
matching inboundagent.

## Stack

- ASP.NET Core 10 (.NET SDK pinned to `10.0.102`, C# 14)
- EF Core 10 against existing SQL Server (Despatch DB, read-mostly)
- React 19 + Vite 8 + Mantine v9 + `vite-plugin-pwa`
  - `package.json` pins a `sharp` override (`^0.35.3`): `@vite-pwa/assets-generator`
    still peers `sharp ^0.33.5`, which carries the libvips advisories in
    GHSA-f88m-g3jw-g9cj. Drop the override once the generator moves up.
- Serilog, AWS Secrets Manager, Polly on the trackingpage HTTP client,
  Data Protection keys in AWS SSM (prod) / local file (dev)
- xUnit.v3 + NSubstitute + `MockQueryable.NSubstitute`; integration tests
  via `WebApplicationFactory` + SQLite in-memory
- Single-deployable Docker image: API serves the built PWA from `wwwroot`

## Solution layout

```
src/
  BaggageDelivery.Api/        ASP.NET Core host (controllers, auth, security headers)
    Controllers/Admin/        SC-JWT-authed link mint endpoint
    Controllers/Pax/          Anonymous booking / tracking / address-lookup endpoints
  BaggageDelivery.Core/       Domain, EF, HTTP clients, encryption, notifications
    Http/                     TrackingPageClient (Polly-wrapped)
    Security/                 AES-256-CBC EncryptionService, JWT helpers, options
    Services/                 PaxBookingService, PaxTrackingService
    Notifications/            MJML/SMS renderer + TucManualMessage sender
    AddressLookup/            HERE Maps autocomplete client
  BaggageDelivery.PaxPortal/  Vite + React + MUI PWA (lazy-loaded routes)
tests/
  BaggageDelivery.UnitTests/
  BaggageDelivery.IntegrationTests/
```

## Routes

| Path                       | Auth        | Notes                                          |
| -------------------------- | ----------- | ---------------------------------------------- |
| `/c/:id`                   | URL-encrypted | Passenger confirmation flow                  |
| `/t/:id`                   | URL-encrypted | Live tracking page                           |
| `/internal/process-map`    | SC-JWT      | Internal ops view                              |
| `/expired`                 | none        | Fallback when token can't be decrypted         |
| `POST /api/v1/admin/booking-links` | SC-JWT bearer | Mint passenger link + send notifications |
| `GET  /api/v1/pax/{id}/booking`    | anonymous | Read booking summary by encrypted token |
| `GET  /api/v1/pax/{id}/booking/timeslots` | anonymous | Available time slots             |
| `POST /api/v1/pax/{id}/booking/confirm`   | anonymous | Confirm delivery details         |
| `GET  /api/v1/pax/{id}/tracking`   | anonymous | Tracking data                           |
| `GET  /api/v1/pax/{id}/address/*`  | anonymous | HERE Maps autocomplete proxy            |
| `GET  /healthz`            | anonymous   | Liveness probe                                 |

The `:id` segment is AES-256-CBC of the integer `JobId`, base64-url-safe.
Anyone holding the URL can act on the booking — no expiry, no revoke, no
single-use. This mirrors the inboundagent model.

## Local dev

First-time setup:

1. Add `127.0.0.1 baggagedelivery.local.deliverdifferent.com` to your hosts
   file (`C:\Windows\System32\drivers\etc\hosts`).
2. Copy `src/BaggageDelivery.Api/Properties/launchSettings.Template.json` to
   `launchSettings.json` in the same folder and fill in the placeholders.
   `launchSettings.json` is gitignored, so this step is per-machine.
3. `cd src/BaggageDelivery.PaxPortal && npm install`

Then run the API — from your IDE, or:

```
dotnet run --project src/BaggageDelivery.Api
```

**The app lives on :5173, not :5298.** Port 5298 is the API only: there is no
`wwwroot` in local dev, so `/` and every SPA route return an empty 404 there
(`Program.cs` skips static files and the SPA fallback when `wwwroot` is
absent — the Dockerfile is what populates it for the deployed image).

`Microsoft.AspNetCore.SpaProxy` bridges the two. The
`ASPNETCORE_HOSTINGSTARTUPASSEMBLIES=Microsoft.AspNetCore.SpaProxy` env var in
the launch profile is what activates it — without that one variable the API
starts fine but Vite never launches and :5298 dead-ends on a blank 404. With it
set, starting the API also runs `npm run dev` (see `SpaProxyServerUrl` /
`SpaProxyLaunchCommand` in `BaggageDelivery.Api.csproj`) and redirects `:5298/`
to `:5173/`.

That redirect covers the root only. A pax deep link pasted against :5298
(`:5298/c/<token>`) still 404s — use :5173 for those, which is what the startup
log and `/dev/links` already hand you.

To run the dev server by hand instead:

```
cd src/BaggageDelivery.PaxPortal && npm run dev
```

PWA dev server runs on `http://baggagedelivery.local.deliverdifferent.com:5173`
(also `http://localhost:5173`). The API CORS policy whitelists both.

In Development, `/` serves a landing page listing the magic links for
`DevTesting:JobId` (default 67), backed by `GET /api/v1/dev/links`. The same
URLs are logged at startup by `DevStartup.LogTestMagicLinks`. Neither the route
nor the endpoint exists outside Development, where `/` redirects to `/expired`.

Required env vars (in addition to `appsettings.Development.json`):

- `ConnectionStrings__DefaultConnection` — Despatch DB (the
  BaggageDelivery SQL user; requires SELECT on `tucJob`, `tucClient`,
  `JobDeliveryJourney`, `tblJobLeaveNotHome`, `tblEcoSetting`; UPDATE on
  `tucJob`; SELECT + INSERT on `tucManualMessage`)
- `BaggageDeliveryEncryptionKey` / `BaggageDeliveryEncryptionIV` — base64,
  32-byte key + 16-byte IV (startup validates)
- `JWTSecretKey`, `Issuer`, `Audience` — SC-JWT inbound validation
  (IM → admin booking-link mint)
- `TimeZone` — IANA timezone for the deployment's tenant
  (`Pacific/Auckland`, `Australia/Sydney`, …)
- `TrackingPageUrl` — trackingpage base URL
- `BaggageDeliveryPublicBaseUrl` — the URL prefix burned into minted links
- `Domain` — cookie domain shared with the deliverdifferent family
- `AppUrl` — origin allowed by CORS (PWA host)
- HERE Maps API key (see `HereMapsOptions`)

In development, Data Protection keys persist to
`%LocalAppData%\DeliverDifferent\DataProtection-Keys`. In prod, they live
in AWS SSM at `/Hub/DataProtection` under the `DeliverDifferent`
application name (shared with hub / IM / inboundagent).

## Migrations

Schema lives in [`dbmigrationsv2`](../dbmigrationsv2), not in this repo.
This service currently owns no `BagDel*` tables — `tucJob` is the
canonical record. Any future schema additions must follow the legacy DB
policy in `~/.claude/CLAUDE.md` (no qualifying objects on legacy
`tuc*`/`tbl*`/`UTL_*`/`MARS_*` tables; every new proc/trigger starts with
`SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;`).

## Tests

```
dotnet test
cd src/BaggageDelivery.PaxPortal && npm test
```

EF tracking pitfall: `BaggageDeliveryContext` runs with
`QueryTrackingBehavior.NoTracking`. Any read-then-mutate flow must use
`.AsTracking()`, `FindAsync()`, or `ExecuteUpdateAsync` — see the rules
in `CLAUDE.md`. Test fixtures must mirror that NoTracking setting;
`tests/BaggageDelivery.UnitTests/Helpers/InMemoryDb.cs` is the template.
