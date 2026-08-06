-- ADR-14. The table browser and reporting run on a principal that physically
-- cannot write.
--
-- Every other control around SE16N is application logic, and application logic
-- has bugs. This one is enforced by the database engine: if a defect in the
-- query builder ever let a crafted statement through, the worst outcome is an
-- error rather than a modification of a posted document.
--
-- The password comes from the S4HERP_READONLY_PASSWORD environment variable in
-- real environments. This script creates the user only when the login already
-- exists, so a development stack without the login is simply skipped rather
-- than given a hard-coded credential.

DECLARE @loginName sysname = N's4herp_readonly';

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @loginName)
BEGIN
    PRINT 'Login s4herp_readonly does not exist; skipping read-only user setup. '
        + 'Create it during environment provisioning, not from application code.';
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @loginName)
    EXEC sys.sp_executesql N'CREATE USER [s4herp_readonly] FOR LOGIN [s4herp_readonly];';

-- Read on the business schemas.
GRANT SELECT ON SCHEMA::org   TO [s4herp_readonly];
GRANT SELECT ON SCHEMA::cfg   TO [s4herp_readonly];
GRANT SELECT ON SCHEMA::mdm   TO [s4herp_readonly];
GRANT SELECT ON SCHEMA::fin   TO [s4herp_readonly];
GRANT SELECT ON SCHEMA::co    TO [s4herp_readonly];
GRANT SELECT ON SCHEMA::wf    TO [s4herp_readonly];
GRANT SELECT ON SCHEMA::rpt   TO [s4herp_readonly];
GRANT SELECT ON SCHEMA::intg  TO [s4herp_readonly];

-- Never the security or audit schemas, at any authorisation level. This is the
-- third independent layer behind the deny-list and the field checks.
DENY SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::sec   TO [s4herp_readonly];
DENY SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::audit TO [s4herp_readonly];

-- No writes anywhere, regardless of any future grant.
DENY INSERT, UPDATE, DELETE, ALTER, EXECUTE ON SCHEMA::org  TO [s4herp_readonly];
DENY INSERT, UPDATE, DELETE, ALTER, EXECUTE ON SCHEMA::cfg  TO [s4herp_readonly];
DENY INSERT, UPDATE, DELETE, ALTER, EXECUTE ON SCHEMA::mdm  TO [s4herp_readonly];
DENY INSERT, UPDATE, DELETE, ALTER, EXECUTE ON SCHEMA::fin  TO [s4herp_readonly];
DENY INSERT, UPDATE, DELETE, ALTER, EXECUTE ON SCHEMA::co   TO [s4herp_readonly];
DENY INSERT, UPDATE, DELETE, ALTER, EXECUTE ON SCHEMA::wf   TO [s4herp_readonly];
DENY INSERT, UPDATE, DELETE, ALTER, EXECUTE ON SCHEMA::rpt  TO [s4herp_readonly];
DENY INSERT, UPDATE, DELETE, ALTER, EXECUTE ON SCHEMA::intg TO [s4herp_readonly];
