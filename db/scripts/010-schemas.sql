-- Creates the schemas that carry no EF entities yet (ADR-02). Migrations create
-- the rest as a side effect of creating their tables.
-- Idempotent: re-executed on every start.

DECLARE @schemas TABLE (name sysname);
INSERT INTO @schemas (name) VALUES (N'org'), (N'cfg'), (N'mdm'), (N'fin'), (N'co'),
                                   (N'wf'), (N'sec'), (N'rpt'), (N'intg'), (N'audit');

DECLARE @name sysname, @sql nvarchar(200);
DECLARE schema_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT s.name FROM @schemas s
    WHERE NOT EXISTS (SELECT 1 FROM sys.schemas x WHERE x.name = s.name);

OPEN schema_cursor;
FETCH NEXT FROM schema_cursor INTO @name;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'CREATE SCHEMA ' + QUOTENAME(@name);
    EXEC sys.sp_executesql @sql;
    FETCH NEXT FROM schema_cursor INTO @name;
END
CLOSE schema_cursor;
DEALLOCATE schema_cursor;
