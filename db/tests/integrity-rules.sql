-- §23 mandatory rules, asserted at the database level.
--
-- Each test names the error it expects. A test that merely "threw something" is
-- not a pass — a harness misconfiguration throws too, and would otherwise read
-- as a green run.
--
-- Run with QUOTED_IDENTIFIER ON (sqlcmd -I): DML against filtered indexes
-- requires it.

SET NOCOUNT ON;
EXEC sys.sp_set_session_context N'IsCrossTenant', 1;

DECLARE @results TABLE (
    Id int IDENTITY, Rule_ nvarchar(80), Expected nvarchar(40),
    Actual nvarchar(200), Outcome char(4));

DECLARE @doc bigint = (SELECT TOP 1 DocumentNumber FROM fin.JournalEntryHeader
                       WHERE [Status] = 8 ORDER BY DocumentNumber);
DECLARE @cc bigint, @fy smallint, @led bigint, @cur bigint, @lcur bigint, @gl bigint;
SELECT TOP 1 @cc = CompanyCodeId, @fy = FiscalYear, @led = LedgerId,
             @cur = DocumentCurrencyId, @lcur = LocalCurrencyId
FROM fin.JournalEntryLine WHERE DocumentNumber = @doc;
SELECT TOP 1 @gl = Id FROM fin.GLAccount WHERE IsReconciliationAccount = 0;

-- 1 --------------------------------------------------------------------------
BEGIN TRY
    UPDATE fin.JournalEntryLine SET DocumentAmount = 99999
    WHERE DocumentNumber = @doc AND LineNumber = 1;
    INSERT @results VALUES (N'Posted journal line cannot be updated', N'50001', N'no error', 'FAIL');
END TRY BEGIN CATCH
    INSERT @results VALUES (N'Posted journal line cannot be updated', N'50001',
        CAST(ERROR_NUMBER() AS nvarchar(20)),
        CASE WHEN ERROR_NUMBER() = 50001 THEN 'PASS' ELSE 'FAIL' END);
END CATCH

-- 2 --------------------------------------------------------------------------
-- Pick a line with no open item, so the trigger is what refuses the delete
-- rather than the OpenItem foreign key.
DECLARE @freeLine smallint = (
    SELECT TOP 1 l.LineNumber FROM fin.JournalEntryLine l
    WHERE l.DocumentNumber = @doc
      AND NOT EXISTS (SELECT 1 FROM fin.OpenItem oi
                      WHERE oi.DocumentNumber = l.DocumentNumber
                        AND oi.LineNumber = l.LineNumber
                        AND oi.CompanyCodeId = l.CompanyCodeId)
    ORDER BY l.LineNumber);
BEGIN TRY
    DELETE FROM fin.JournalEntryLine WHERE DocumentNumber = @doc AND LineNumber = @freeLine;
    INSERT @results VALUES (N'Posted journal line cannot be deleted', N'50005', N'no error', 'FAIL');
END TRY BEGIN CATCH
    INSERT @results VALUES (N'Posted journal line cannot be deleted', N'50005',
        CAST(ERROR_NUMBER() AS nvarchar(20)),
        CASE WHEN ERROR_NUMBER() = 50005 THEN 'PASS' ELSE 'FAIL' END);
END CATCH

-- 3 --------------------------------------------------------------------------
BEGIN TRY
    DELETE FROM fin.JournalEntryHeader WHERE DocumentNumber = @doc;
    INSERT @results VALUES (N'Posted document cannot be deleted', N'50003', N'no error', 'FAIL');
END TRY BEGIN CATCH
    INSERT @results VALUES (N'Posted document cannot be deleted', N'50003',
        CAST(ERROR_NUMBER() AS nvarchar(20)),
        CASE WHEN ERROR_NUMBER() = 50003 THEN 'PASS' ELSE 'FAIL' END);
END CATCH

-- 4 --------------------------------------------------------------------------
BEGIN TRY
    UPDATE fin.JournalEntryHeader SET PostingDate = '2026-12-31' WHERE DocumentNumber = @doc;
    INSERT @results VALUES (N'Posted header key data is frozen', N'50002', N'no error', 'FAIL');
END TRY BEGIN CATCH
    INSERT @results VALUES (N'Posted header key data is frozen', N'50002',
        CAST(ERROR_NUMBER() AS nvarchar(20)),
        CASE WHEN ERROR_NUMBER() = 50002 THEN 'PASS' ELSE 'FAIL' END);
END CATCH

-- 5 / 6 ----------------------------------------------------------------------
BEGIN TRY
    UPDATE fin.JournalEntryHeader SET ReversedByDocumentNumber = 100000999
    WHERE DocumentNumber = @doc;
    INSERT @results VALUES (N'Reversal back-link may be written once', N'no error', N'no error', 'PASS');
END TRY BEGIN CATCH
    INSERT @results VALUES (N'Reversal back-link may be written once', N'no error',
        CAST(ERROR_NUMBER() AS nvarchar(20)), 'FAIL');
END CATCH

BEGIN TRY
    UPDATE fin.JournalEntryHeader SET ReversedByDocumentNumber = 100000888
    WHERE DocumentNumber = @doc;
    INSERT @results VALUES (N'Reversal back-link cannot be rewritten', N'50002', N'no error', 'FAIL');
END TRY BEGIN CATCH
    INSERT @results VALUES (N'Reversal back-link cannot be rewritten', N'50002',
        CAST(ERROR_NUMBER() AS nvarchar(20)),
        CASE WHEN ERROR_NUMBER() = 50002 THEN 'PASS' ELSE 'FAIL' END);
END CATCH

UPDATE fin.JournalEntryHeader SET ReversedByDocumentNumber = NULL WHERE 1 = 0; -- no-op

-- 7 --------------------------------------------------------------------------
BEGIN TRY
    UPDATE audit.AuditLog SET Summary = N'tampered';
    INSERT @results VALUES (N'Audit log is append-only', N'50004', N'no error', 'FAIL');
END TRY BEGIN CATCH
    INSERT @results VALUES (N'Audit log is append-only', N'50004',
        CAST(ERROR_NUMBER() AS nvarchar(20)),
        CASE WHEN ERROR_NUMBER() = 50004 THEN 'PASS' ELSE 'FAIL' END);
END CATCH

-- 8 -- check constraint 547 --------------------------------------------------
BEGIN TRY
    INSERT INTO fin.JournalEntryLine
        (TenantId, CompanyCodeId, FiscalYear, DocumentNumber, LedgerId, LineNumber,
         PostingKey, DebitCredit, AccountType, GLAccountId, DocumentAmount,
         DocumentCurrencyId, LocalAmount, LocalCurrencyId, IsTaxLine, SourceModule)
    VALUES (1, @cc, @fy, @doc, @led, 90, '40', 'D', 'S', @gl, -500, @cur, -500, @lcur, 0, 1);
    INSERT @results VALUES (N'Debit line with a negative amount is rejected', N'547', N'no error', 'FAIL');
END TRY BEGIN CATCH
    INSERT @results VALUES (N'Debit line with a negative amount is rejected', N'547',
        CAST(ERROR_NUMBER() AS nvarchar(20)),
        CASE WHEN ERROR_NUMBER() = 547 THEN 'PASS' ELSE 'FAIL' END);
END CATCH

-- 9 --------------------------------------------------------------------------
BEGIN TRY
    INSERT INTO fin.JournalEntryLine
        (TenantId, CompanyCodeId, FiscalYear, DocumentNumber, LedgerId, LineNumber,
         PostingKey, DebitCredit, AccountType, DocumentAmount,
         DocumentCurrencyId, LocalAmount, LocalCurrencyId, IsTaxLine, SourceModule)
    VALUES (1, @cc, @fy, @doc, @led, 91, '01', 'D', 'D', 500, @cur, 500, @lcur, 0, 1);
    INSERT @results VALUES (N'Customer line without a business partner is rejected', N'547', N'no error', 'FAIL');
END TRY BEGIN CATCH
    INSERT @results VALUES (N'Customer line without a business partner is rejected', N'547',
        CAST(ERROR_NUMBER() AS nvarchar(20)),
        CASE WHEN ERROR_NUMBER() = 547 THEN 'PASS' ELSE 'FAIL' END);
END CATCH

-- 10 -------------------------------------------------------------------------
BEGIN TRY
    UPDATE fin.GLAccount SET IsReconciliationAccount = 1, ReconciliationAccountType = NULL
    WHERE AccountNumber = '4000000000';
    INSERT @results VALUES (N'Reconciliation account must declare its subledger', N'547', N'no error', 'FAIL');
END TRY BEGIN CATCH
    INSERT @results VALUES (N'Reconciliation account must declare its subledger', N'547',
        CAST(ERROR_NUMBER() AS nvarchar(20)),
        CASE WHEN ERROR_NUMBER() = 547 THEN 'PASS' ELSE 'FAIL' END);
END CATCH

-- 11 -- tenant isolation -----------------------------------------------------
EXEC sys.sp_set_session_context N'IsCrossTenant', 0;
EXEC sys.sp_set_session_context N'TenantId', 999;
DECLARE @leaked int = (SELECT COUNT(*) FROM mdm.BusinessPartner);
INSERT @results VALUES (N'Wrong tenant sees no business partners', N'0 rows',
    CAST(@leaked AS nvarchar(20)) + N' rows', CASE WHEN @leaked = 0 THEN 'PASS' ELSE 'FAIL' END);

EXEC sys.sp_set_session_context N'TenantId', NULL;
EXEC sys.sp_set_session_context N'IsCrossTenant', NULL;
DECLARE @unset int = (SELECT COUNT(*) FROM mdm.BusinessPartner);
INSERT @results VALUES (N'Unset tenant context denies rather than allows', N'0 rows',
    CAST(@unset AS nvarchar(20)) + N' rows', CASE WHEN @unset = 0 THEN 'PASS' ELSE 'FAIL' END);

-- 12 -- balancing ------------------------------------------------------------
EXEC sys.sp_set_session_context N'IsCrossTenant', 1;
DECLARE @unbalanced int = (
    SELECT COUNT(*) FROM (
        SELECT DocumentNumber, CompanyCodeId, LedgerId, DocumentCurrencyId
        FROM fin.JournalEntryLine
        GROUP BY DocumentNumber, CompanyCodeId, LedgerId, DocumentCurrencyId
        HAVING SUM(DocumentAmount) <> 0 OR SUM(LocalAmount) <> 0) x);
INSERT @results VALUES (N'Every document balances in every currency', N'0 documents',
    CAST(@unbalanced AS nvarchar(20)) + N' documents',
    CASE WHEN @unbalanced = 0 THEN 'PASS' ELSE 'FAIL' END);

-- 13 -- subledger reconciles to the journal ----------------------------------
DECLARE @arJournal decimal(19,4) = (
    SELECT ISNULL(SUM(LocalAmount), 0) FROM fin.JournalEntryLine WHERE AccountType = 'D');
DECLARE @arOpen decimal(19,4) = (
    SELECT ISNULL(SUM(OpenAmountLocal), 0) FROM fin.OpenItem WHERE AccountType = 'D');
INSERT @results VALUES (N'AR open items reconcile to the journal',
    CAST(@arJournal AS nvarchar(20)), CAST(@arOpen AS nvarchar(20)),
    CASE WHEN @arJournal = @arOpen THEN 'PASS' ELSE 'FAIL' END);

SELECT Id, Outcome, Rule_, Expected, Actual FROM @results ORDER BY Id;
SELECT CAST(SUM(CASE WHEN Outcome = 'PASS' THEN 1 ELSE 0 END) AS nvarchar(10)) + N'/'
     + CAST(COUNT(*) AS nvarchar(10)) + N' passed'  AS Summary
FROM @results;
