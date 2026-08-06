-- Partitions the universal journal line table by fiscal year.
--
-- Done now rather than later on purpose: adding partitioning to a populated
-- table of hundreds of millions of rows means a full rebuild in a maintenance
-- window, and that window grows every year the decision is deferred. On an empty
-- table it is a metadata operation.
--
-- Scope: fin.JournalEntryLine only. fin.JournalEntryHeader and audit.AuditLog
-- also warrant partitioning, but their clustered keys do not currently carry a
-- partition column — Header's unique index on DocumentNumberFormatted would have
-- to take FiscalYear, and AuditLog would need a persisted year column. Both are
-- key changes, so they belong in a reviewed migration rather than in a script
-- that runs at startup.
--
-- Idempotent: checks whether the clustered index already sits on the scheme.

DECLARE @firstYear int = 2024;
DECLARE @lastYear  int = 2035;

IF NOT EXISTS (SELECT 1 FROM sys.partition_functions WHERE name = N'pf_FiscalYear')
BEGIN
    DECLARE @boundaries nvarchar(max) = N'';
    DECLARE @year int = @firstYear;
    WHILE @year <= @lastYear
    BEGIN
        SET @boundaries = @boundaries + CASE WHEN LEN(@boundaries) > 0 THEN N', ' ELSE N'' END
                        + CAST(@year AS nvarchar(4));
        SET @year = @year + 1;
    END

    -- RANGE RIGHT: boundary value 2026 starts the 2026 partition.
    -- sp_executesql takes an nvarchar variable, never a concatenated expression.
    DECLARE @createFunction nvarchar(max) =
        N'CREATE PARTITION FUNCTION pf_FiscalYear (smallint) AS RANGE RIGHT FOR VALUES ('
        + @boundaries + N');';
    EXEC sys.sp_executesql @createFunction;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.partition_schemes WHERE name = N'ps_FiscalYear')
    CREATE PARTITION SCHEME ps_FiscalYear AS PARTITION pf_FiscalYear ALL TO ([PRIMARY]);
GO

-- Move fin.JournalEntryLine onto the scheme if it is not already there.
IF EXISTS (
    SELECT 1
    FROM sys.indexes i
    JOIN sys.tables t ON t.object_id = i.object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = N'fin' AND t.name = N'JournalEntryLine' AND i.index_id = 1
      AND i.data_space_id <> (SELECT data_space_id FROM sys.partition_schemes WHERE name = N'ps_FiscalYear'))
BEGIN
    IF EXISTS (SELECT 1 FROM fin.JournalEntryLine)
    BEGIN
        RAISERROR (N'fin.JournalEntryLine already holds rows; partition it through a reviewed migration, not at startup.', 16, 1);
    END
    ELSE
    BEGIN
        DECLARE @fkOpenItem nvarchar(200) = (
            SELECT TOP 1 QUOTENAME(fk.name)
            FROM sys.foreign_keys fk
            WHERE fk.referenced_object_id = OBJECT_ID(N'fin.JournalEntryLine'));

        DECLARE @dropFk nvarchar(300);
        IF @fkOpenItem IS NOT NULL
        BEGIN
            SET @dropFk = N'ALTER TABLE fin.OpenItem DROP CONSTRAINT ' + @fkOpenItem + N';';
            EXEC sys.sp_executesql @dropFk;
        END

        -- Realign the non-clustered indexes so partition switching stays possible.
        DROP INDEX IF EXISTS IX_JournalEntryLine_Account   ON fin.JournalEntryLine;
        DROP INDEX IF EXISTS IX_JournalEntryLine_Partner   ON fin.JournalEntryLine;
        DROP INDEX IF EXISTS IX_JournalEntryLine_CostCenter ON fin.JournalEntryLine;
        DROP INDEX IF EXISTS IX_JournalEntryLine_ProfitCenter ON fin.JournalEntryLine;

        DECLARE @pk sysname = (
            SELECT name FROM sys.key_constraints
            WHERE parent_object_id = OBJECT_ID(N'fin.JournalEntryLine') AND type = 'PK');

        DECLARE @dropPk nvarchar(300) =
            N'ALTER TABLE fin.JournalEntryLine DROP CONSTRAINT ' + QUOTENAME(@pk) + N';';
        EXEC sys.sp_executesql @dropPk;

        EXEC sys.sp_executesql N'
            ALTER TABLE fin.JournalEntryLine ADD CONSTRAINT PK_JournalEntryLine
                PRIMARY KEY CLUSTERED
                ([TenantId], [CompanyCodeId], [FiscalYear], [DocumentNumber], [LedgerId], [LineNumber])
                ON ps_FiscalYear([FiscalYear]);';

        EXEC sys.sp_executesql N'
            CREATE NONCLUSTERED INDEX IX_JournalEntryLine_Account
                ON fin.JournalEntryLine ([TenantId], [CompanyCodeId], [LedgerId], [GLAccountId], [FiscalYear])
                INCLUDE ([LocalAmount], [GroupAmount], [DocumentAmount])
                ON ps_FiscalYear([FiscalYear]);';

        EXEC sys.sp_executesql N'
            CREATE NONCLUSTERED INDEX IX_JournalEntryLine_Partner
                ON fin.JournalEntryLine ([TenantId], [CompanyCodeId], [BusinessPartnerId], [FiscalYear])
                WHERE [BusinessPartnerId] IS NOT NULL;';

        EXEC sys.sp_executesql N'
            CREATE NONCLUSTERED INDEX IX_JournalEntryLine_CostCenter
                ON fin.JournalEntryLine ([TenantId], [CostCenterId], [FiscalYear])
                WHERE [CostCenterId] IS NOT NULL;';

        EXEC sys.sp_executesql N'
            CREATE NONCLUSTERED INDEX IX_JournalEntryLine_ProfitCenter
                ON fin.JournalEntryLine ([TenantId], [ProfitCenterId], [FiscalYear])
                WHERE [ProfitCenterId] IS NOT NULL;';

        EXEC sys.sp_executesql N'
            ALTER TABLE fin.OpenItem ADD CONSTRAINT FK_OpenItem_JournalEntryLine
                FOREIGN KEY ([TenantId], [CompanyCodeId], [FiscalYear], [DocumentNumber], [LedgerId], [LineNumber])
                REFERENCES fin.JournalEntryLine
                    ([TenantId], [CompanyCodeId], [FiscalYear], [DocumentNumber], [LedgerId], [LineNumber]);';
    END
END
