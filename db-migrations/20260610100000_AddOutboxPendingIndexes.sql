-- DRAFT — move this file to dbmigrationsv2/DatabaseScripts/Migrations and
-- adjust the timestamp prefix to land after the latest migration in that repo.
--
-- Filtered indexes covering the drain query on both outbox tables:
--   WHERE Status = 'Pending' AND NextAttemptUtc <= @now
--   ORDER BY NextAttemptUtc
--
-- Safe to use filtered indexes here — BagDelConfirmationOutbox and
-- BagDelNotificationLog are new in 20260610090000_AddBaggageDeliveryTables.sql
-- and no legacy QI-OFF procedures touch them.

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BagDelConfirmationOutbox_Pending')
BEGIN
    CREATE NONCLUSTERED INDEX IX_BagDelConfirmationOutbox_Pending
        ON dbo.BagDelConfirmationOutbox (NextAttemptUtc)
        INCLUDE (BookingId, JobId, TenantId, AttemptCount)
        WHERE Status = 'Pending';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BagDelNotificationLog_Pending')
BEGIN
    CREATE NONCLUSTERED INDEX IX_BagDelNotificationLog_Pending
        ON dbo.BagDelNotificationLog (NextAttemptUtc)
        INCLUDE (BookingId, Channel, Recipient, AttemptCount)
        WHERE Status = 'Pending';
END
GO
