-- ADR-07 and the §23 mandatory rule "posted documents cannot be deleted".
--
-- §22 keeps business logic out of triggers, and that rule stands. These are not
-- business logic — they are integrity constraints that SQL Server cannot express
-- declaratively. A posted journal line has no legitimate UPDATE or DELETE, so the
-- database refuses both regardless of what any application, migration or DBA
-- session attempts.
--
-- Corrections go through reversal or an adjustment posting. Clearing state lives
-- in fin.OpenItem, which is freely updatable (ADR-09).
--
-- Idempotent: CREATE OR ALTER.

-- AFTER rather than INSTEAD OF: the line table is the target of a cascading
-- foreign key from the header, and SQL Server forbids INSTEAD OF UPDATE/DELETE
-- triggers on such a table.
CREATE OR ALTER TRIGGER fin.TR_JournalEntryLine_NoUpdate
ON fin.JournalEntryLine
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 50001, N'fin.JournalEntryLine is insert-only. Post a reversal or an adjustment document instead of changing a posted line.', 1;
END
GO

-- A direct DELETE against a line is always wrong. A cascade from deleting a
-- pre-posting header is legitimate, and is distinguishable because the header
-- row is already gone by the time this fires — the header's own trigger has
-- already refused the posted case.
CREATE OR ALTER TRIGGER fin.TR_JournalEntryLine_NoDelete
ON fin.JournalEntryLine
AFTER DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM deleted d
        JOIN fin.JournalEntryHeader h
          ON  h.TenantId       = d.TenantId
          AND h.CompanyCodeId  = d.CompanyCodeId
          AND h.FiscalYear     = d.FiscalYear
          AND h.DocumentNumber = d.DocumentNumber)
    BEGIN
        THROW 50005, N'A journal line cannot be deleted from an existing document. Post a reversal instead.', 1;
    END
END
GO

-- The header is mutable while pre-posting. Once posted, the only permitted
-- change is the one-time write of the reversal back-link.
CREATE OR ALTER TRIGGER fin.TR_JournalEntryHeader_PostedImmutable
ON fin.JournalEntryHeader
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Status 8 = Posted, 9 = Reversed.
    IF EXISTS (
        SELECT 1
        FROM deleted d
        JOIN inserted i
          ON  i.TenantId      = d.TenantId
          AND i.CompanyCodeId = d.CompanyCodeId
          AND i.FiscalYear    = d.FiscalYear
          AND i.DocumentNumber = d.DocumentNumber
        WHERE d.[Status] IN (8, 9)
          AND (   i.CompanyCodeId        <> d.CompanyCodeId
               OR i.DocumentTypeId       <> d.DocumentTypeId
               OR i.PostingDate          <> d.PostingDate
               OR i.DocumentDate         <> d.DocumentDate
               OR i.FiscalPeriod         <> d.FiscalPeriod
               OR i.DocumentCurrencyId   <> d.DocumentCurrencyId
               OR i.ExchangeRateToLocal  <> d.ExchangeRateToLocal
               OR i.ExchangeRateToGroup  <> d.ExchangeRateToGroup
               OR ISNULL(i.DocumentNumberFormatted, N'') <> ISNULL(d.DocumentNumberFormatted, N'')
               -- The back-link may be written once, never rewritten.
               OR (d.ReversedByDocumentNumber IS NOT NULL
                   AND ISNULL(i.ReversedByDocumentNumber, -1) <> d.ReversedByDocumentNumber)))
    BEGIN
        THROW 50002, N'A posted accounting document is immutable except for the one-time reversal back-link. Use FB08 to reverse it.', 1;
    END
END
GO

CREATE OR ALTER TRIGGER fin.TR_JournalEntryHeader_NoDeletePosted
ON fin.JournalEntryHeader
INSTEAD OF DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM deleted WHERE [Status] IN (8, 9))
        THROW 50003, N'A posted accounting document cannot be deleted. Post a reversal instead.', 1;

    DELETE h
    FROM fin.JournalEntryHeader h
    JOIN deleted d
      ON  h.TenantId       = d.TenantId
      AND h.CompanyCodeId  = d.CompanyCodeId
      AND h.FiscalYear     = d.FiscalYear
      AND h.DocumentNumber = d.DocumentNumber;
END
GO

-- The audit trail is append-only too: a business audit record that can be edited
-- is not evidence of anything.
CREATE OR ALTER TRIGGER audit.TR_AuditLog_AppendOnly
ON audit.AuditLog
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 50004, N'audit.AuditLog is append-only.', 1;
END
GO
