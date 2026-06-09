# BaggageDelivery

Passenger-facing PWA + ASP.NET Core service for lost-baggage delivery
confirmation and tracking. After SITA WorldTracer pushes a Baggage Delivery
Order (BDO) to IntegrationManager and a Despatch job is created (ON HOLD), the
passenger receives a magic-link SMS/email. They open the PWA, confirm their
delivery address, pick a time slot, set Authority-to-Leave, and the job flips
to READY FOR DISPATCH. They then track the delivery live.

## Stack

- ASP.NET Core 10 (.NET SDK 10.0.102) — mirrors `intergrationmanager`
- React 19 + Vite + MUI v7 + `vite-plugin-pwa`
- SQL Server (existing Despatch DB; new tables prefixed `BagDel*`)
- AWS Secrets Manager + Data Protection in SSM (prod) / local file (dev)
- Serilog + OpenTelemetry, Polly resilience, xUnit.v3 + NSubstitute

## Solution layout

```
src/
  BaggageDelivery.Api/        ASP.NET Core host (controllers + workers)
  BaggageDelivery.Core/       Domain, EF, HTTP clients, magic-link service
  BaggageDelivery.PaxPortal/  Vite + React + MUI PWA
tests/
  BaggageDelivery.UnitTests/
  BaggageDelivery.IntegrationTests/
```

## Local dev

```
dotnet run --project src/BaggageDelivery.Api
cd src/BaggageDelivery.PaxPortal && npm run dev
```

Env vars required (see `appsettings.Development.json`):
`Domain`, `JWTSecretKey`, `Issuer`, `Audience`, `ClaimsKey`,
`SQLCredentials`, `WebAPIUrl`, `BaggageDeliveryPublicBaseUrl`,
`TwilioAccountSid`, `TwilioAuthToken`, `SendGridApiKey`.

## Migrations

Lives in [`dbmigrationsv2`](../dbmigrationsv2). New `BagDel*` tables defined in
`20260610090000_AddBaggageDeliveryTables.sql`.
