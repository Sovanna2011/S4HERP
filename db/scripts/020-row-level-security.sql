-- ADR-03, second layer. The EF global query filter covers LINQ; this covers
-- everything the ORM cannot see — raw SQL, the SE16N table browser, reporting
-- views. A defect in application code then costs an error, not a cross-tenant
-- disclosure.
--
-- The predicate reads SESSION_CONTEXT, which TenantSessionInterceptor sets on
-- every connection open. A connection that sets nothing sees nothing: unset
-- session context denies rather than allows, so a code path that forgets to
-- identify its tenant fails loudly instead of leaking quietly.
--
-- Idempotent: rebuilt on every start so newly migrated tables join the policy.

IF OBJECT_ID(N'sec.fn_TenantAccessPredicate', N'IF') IS NULL
BEGIN
    EXEC sys.sp_executesql N'
        CREATE FUNCTION sec.fn_TenantAccessPredicate(@TenantId bigint)
        RETURNS TABLE
        WITH SCHEMABINDING
        AS RETURN
            SELECT 1 AS accessGranted
            WHERE
                -- Migration, seeding and cross-tenant administration.
                TRY_CAST(SESSION_CONTEXT(N''IsCrossTenant'') AS bit) = 1
                -- Normal request scope.
                OR @TenantId = TRY_CAST(SESSION_CONTEXT(N''TenantId'') AS bigint);';
END
GO

-- Rebuild the policy so tables added by later migrations are covered.
IF EXISTS (SELECT 1 FROM sys.security_policies WHERE name = N'TenantIsolationPolicy')
    DROP SECURITY POLICY sec.TenantIsolationPolicy;
GO

DECLARE @predicates nvarchar(max) = N'';

SELECT @predicates = @predicates
     + N'    ADD FILTER PREDICATE sec.fn_TenantAccessPredicate([TenantId]) ON '
     + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N',' + CHAR(13) + CHAR(10)
     + N'    ADD BLOCK PREDICATE sec.fn_TenantAccessPredicate([TenantId]) ON '
     + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' AFTER INSERT,' + CHAR(13) + CHAR(10)
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.columns c ON c.object_id = t.object_id AND c.name = N'TenantId'
WHERE s.name IN (N'org', N'cfg', N'mdm', N'fin', N'co', N'wf', N'sec', N'rpt', N'intg', N'audit')
  AND t.is_ms_shipped = 0
ORDER BY s.name, t.name;

IF LEN(@predicates) > 0
BEGIN
    -- Trim the trailing comma and newline.
    SET @predicates = LEFT(@predicates, LEN(@predicates) - 3);

    DECLARE @sql nvarchar(max) =
        N'CREATE SECURITY POLICY sec.TenantIsolationPolicy' + CHAR(13) + CHAR(10)
        + @predicates + CHAR(13) + CHAR(10)
        + N'    WITH (STATE = ON, SCHEMABINDING = ON);';

    EXEC sys.sp_executesql @sql;
END
