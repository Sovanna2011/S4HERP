using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace S4HERP.Host.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "co");

            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "sec");

            migrationBuilder.EnsureSchema(
                name: "org");

            migrationBuilder.EnsureSchema(
                name: "mdm");

            migrationBuilder.EnsureSchema(
                name: "fin");

            migrationBuilder.EnsureSchema(
                name: "cfg");

            migrationBuilder.CreateTable(
                name: "AuditLog",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: true),
                    Action = table.Column<int>(type: "int", nullable: false),
                    ObjectType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ObjectId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SourceScreen = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    SourceApi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TransactionCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceIpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuthorizationObject",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthorizationObject", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessArea",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessArea", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPartnerGroup",
                schema: "mdm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    NumberRangeCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    UseSameNumber = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPartnerGroup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPartnerRole",
                schema: "mdm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    PrerequisiteRoleCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    RequiresCompanyCode = table.Column<bool>(type: "bit", nullable: false),
                    RequiresSalesArea = table.Column<bool>(type: "bit", nullable: false),
                    RequiresPurchasingOrganization = table.Column<bool>(type: "bit", nullable: false),
                    ReconciliationAccountType = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPartnerRole", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChartOfAccounts",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    MaintenanceLanguage = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    AccountNumberLength = table.Column<byte>(type: "tinyint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartOfAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Currency",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DecimalPlaces = table.Column<byte>(type: "tinyint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentType",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    NumberRangeCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    AllowedAccountTypes = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    IsReversalAllowed = table.Column<bool>(type: "bit", nullable: false),
                    ReversalDocumentTypeCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    RequiresReference = table.Column<bool>(type: "bit", nullable: false),
                    IsIntercompany = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRateType",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    QuotationDirection = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRateType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FiscalYearVariant",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    NormalPeriods = table.Column<byte>(type: "tinyint", nullable: false),
                    SpecialPeriods = table.Column<byte>(type: "tinyint", nullable: false),
                    IsCalendarYear = table.Column<bool>(type: "bit", nullable: false),
                    IsYearDependent = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalYearVariant", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FunctionalArea",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunctionalArea", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InternalOrderType",
                schema: "co",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    NumberRangeCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    BudgetControlEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DefaultSettlementReceiver = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalOrderType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ledger",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    LedgerType = table.Column<int>(type: "int", nullable: false),
                    AccountingStandard = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FiscalYearVariantId = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ledger", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoginHistory",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    AttemptedUserName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AttemptedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    SourceIpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permission",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permission", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PostingIdempotency",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: false),
                    FiscalYear = table.Column<short>(type: "smallint", nullable: false),
                    DocumentNumber = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostingIdempotency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PostingKey",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    IsDebit = table.Column<bool>(type: "bit", nullable: false),
                    IsSalesRelated = table.Column<bool>(type: "bit", nullable: false),
                    IsReversal = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostingKey", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PostingPeriodVariant",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostingPeriodVariant", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Role",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Segment",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Segment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SegregationOfDutiesRule",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Rationale = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    ConflictingAuthorizationObjectA = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ConflictingAuthorizationObjectB = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SegregationOfDutiesRule", x => x.Id);
                    table.CheckConstraint("CK_SodRule_DistinctObjects", "[ConflictingAuthorizationObjectA] <> [ConflictingAuthorizationObjectB]");
                });

            migrationBuilder.CreateTable(
                name: "TableBrowserLog",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    ExecutedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SelectedFields = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FilterJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SortJson = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowsReturned = table.Column<int>(type: "int", nullable: false),
                    WasExported = table.Column<bool>(type: "bit", nullable: false),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceIpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TableBrowserLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxCode",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(23,6)", precision: 19, scale: 4, nullable: false),
                    IsDeductible = table.Column<bool>(type: "bit", nullable: false),
                    TaxAccountId = table.Column<long>(type: "bigint", nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false, defaultValue: new DateOnly(9999, 12, 31)),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxCode", x => x.Id);
                    table.CheckConstraint("CK_TaxCode_Rate", "[Rate] >= 0 AND [Rate] <= 100");
                });

            migrationBuilder.CreateTable(
                name: "Tenant",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DefaultLanguage = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenant", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransactionCode",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    Target = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DefaultParameters = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    AuthorizationObjectCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Keywords = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false, defaultValue: new DateOnly(9999, 12, 31)),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionCode", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "User",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BusinessPartnerId = table.Column<long>(type: "bigint", nullable: true),
                    UserType = table.Column<int>(type: "int", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DefaultCompanyCodeId = table.Column<long>(type: "bigint", nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PasswordChangedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    MfaEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    LockedUntilUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                    LastLoginAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false, defaultValue: new DateOnly(9999, 12, 31)),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                    table.CheckConstraint("CK_User_Credential", "[UserType] NOT IN (1,6) OR [PasswordHash] IS NOT NULL");
                });

            migrationBuilder.CreateTable(
                name: "AuditLogField",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    AuditLogId = table.Column<long>(type: "bigint", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogField", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogField_AuditLog_AuditLogId",
                        column: x => x.AuditLogId,
                        principalSchema: "audit",
                        principalTable: "AuditLog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuthorizationField",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthorizationObjectId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthorizationField", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuthorizationField_AuthorizationObject_AuthorizationObjectId",
                        column: x => x.AuthorizationObjectId,
                        principalSchema: "sec",
                        principalTable: "AuthorizationObject",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPartner",
                schema: "mdm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerNumber = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    PartnerGroupId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    SearchTerm1 = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    SearchTerm2 = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Industry = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    Language = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    CountryOfOrigin = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    IsBlocked = table.Column<bool>(type: "bit", nullable: false),
                    IsMarkedForDeletion = table.Column<bool>(type: "bit", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false, defaultValue: new DateOnly(9999, 12, 31)),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPartner", x => x.Id);
                    table.CheckConstraint("CK_BusinessPartner_Name", "([Category] = 2 AND [LastName] IS NOT NULL) OR ([Category] IN (1,3) AND [Name] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_BusinessPartner_BusinessPartnerGroup_PartnerGroupId",
                        column: x => x.PartnerGroupId,
                        principalSchema: "mdm",
                        principalTable: "BusinessPartnerGroup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPartnerRoleCategory",
                schema: "mdm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerRoleId = table.Column<long>(type: "bigint", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPartnerRoleCategory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerRoleCategory_BusinessPartnerRole_PartnerRoleId",
                        column: x => x.PartnerRoleId,
                        principalSchema: "mdm",
                        principalTable: "BusinessPartnerRole",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FinancialStatementVersion",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ChartOfAccountsId = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialStatementVersion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialStatementVersion_ChartOfAccounts_ChartOfAccountsId",
                        column: x => x.ChartOfAccountsId,
                        principalSchema: "fin",
                        principalTable: "ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GLAccountGroup",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ChartOfAccountsId = table.Column<long>(type: "bigint", nullable: false),
                    FromAccount = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ToAccount = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GLAccountGroup", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GLAccountGroup_ChartOfAccounts_ChartOfAccountsId",
                        column: x => x.ChartOfAccountsId,
                        principalSchema: "fin",
                        principalTable: "ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Company",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    GroupCurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Company", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Company_Currency_GroupCurrencyId",
                        column: x => x.GroupCurrencyId,
                        principalSchema: "cfg",
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreditControlArea",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditControlArea", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditControlArea_Currency_CurrencyId",
                        column: x => x.CurrencyId,
                        principalSchema: "cfg",
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRate",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExchangeRateTypeId = table.Column<long>(type: "bigint", nullable: false),
                    FromCurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    ToCurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(23,6)", precision: 19, scale: 4, nullable: false),
                    FromRatio = table.Column<int>(type: "int", nullable: false),
                    ToRatio = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRate", x => x.Id);
                    table.CheckConstraint("CK_ExchangeRate_Positive", "[Rate] > 0");
                    table.ForeignKey(
                        name: "FK_ExchangeRate_Currency_FromCurrencyId",
                        column: x => x.FromCurrencyId,
                        principalSchema: "cfg",
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExchangeRate_Currency_ToCurrencyId",
                        column: x => x.ToCurrencyId,
                        principalSchema: "cfg",
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExchangeRate_ExchangeRateType_ExchangeRateTypeId",
                        column: x => x.ExchangeRateTypeId,
                        principalSchema: "cfg",
                        principalTable: "ExchangeRateType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ControllingArea",
                schema: "co",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    ChartOfAccountsId = table.Column<long>(type: "bigint", nullable: false),
                    FiscalYearVariantId = table.Column<long>(type: "bigint", nullable: false),
                    CrossCompanyCodeCostAccounting = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControllingArea", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControllingArea_Currency_CurrencyId",
                        column: x => x.CurrencyId,
                        principalSchema: "cfg",
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ControllingArea_FiscalYearVariant_FiscalYearVariantId",
                        column: x => x.FiscalYearVariantId,
                        principalSchema: "cfg",
                        principalTable: "FiscalYearVariant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FiscalYearPeriod",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FiscalYearVariantId = table.Column<long>(type: "bigint", nullable: false),
                    FiscalYear = table.Column<short>(type: "smallint", nullable: false),
                    Period = table.Column<byte>(type: "tinyint", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsSpecialPeriod = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalYearPeriod", x => x.Id);
                    table.CheckConstraint("CK_FiscalYearPeriod_Dates", "[EndDate] >= [StartDate]");
                    table.ForeignKey(
                        name: "FK_FiscalYearPeriod_FiscalYearVariant_FiscalYearVariantId",
                        column: x => x.FiscalYearVariantId,
                        principalSchema: "cfg",
                        principalTable: "FiscalYearVariant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostingPeriod",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostingPeriodVariantId = table.Column<long>(type: "bigint", nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    FiscalYear = table.Column<short>(type: "smallint", nullable: false),
                    Period = table.Column<byte>(type: "tinyint", nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    RestrictedToAuthorizationGroup = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostingPeriod", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostingPeriod_PostingPeriodVariant_PostingPeriodVariantId",
                        column: x => x.PostingPeriodVariantId,
                        principalSchema: "cfg",
                        principalTable: "PostingPeriodVariant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoleAuthorization",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    AuthorizationObjectId = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleAuthorization", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleAuthorization_AuthorizationObject_AuthorizationObjectId",
                        column: x => x.AuthorizationObjectId,
                        principalSchema: "sec",
                        principalTable: "AuthorizationObject",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleAuthorization_Role_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "sec",
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermission",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    PermissionId = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermission", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermission_Permission_PermissionId",
                        column: x => x.PermissionId,
                        principalSchema: "sec",
                        principalTable: "Permission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermission_Role_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "sec",
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoleTransactionCode",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    TransactionCodeId = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleTransactionCode", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleTransactionCode_Role_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "sec",
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PasswordHistory",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordHistory_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "sec",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserCompanyCode",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCompanyCode", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCompanyCode_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "sec",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRole",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false, defaultValue: new DateOnly(9999, 12, 31)),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRole", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRole_Role_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "sec",
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRole_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "sec",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSession",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    SourceIpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSession", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSession_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "sec",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSubstitution",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    SubstituteUserId = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false, defaultValue: new DateOnly(9999, 12, 31)),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSubstitution", x => x.Id);
                    table.CheckConstraint("CK_UserSubstitution_NotSelf", "[UserId] <> [SubstituteUserId]");
                    table.ForeignKey(
                        name: "FK_UserSubstitution_User_SubstituteUserId",
                        column: x => x.SubstituteUserId,
                        principalSchema: "sec",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserSubstitution_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "sec",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPartnerAddress",
                schema: "mdm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerId = table.Column<long>(type: "bigint", nullable: false),
                    AddressType = table.Column<int>(type: "int", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    Line1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Line2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false, defaultValue: new DateOnly(9999, 12, 31)),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPartnerAddress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerAddress_BusinessPartner_PartnerId",
                        column: x => x.PartnerId,
                        principalSchema: "mdm",
                        principalTable: "BusinessPartner",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPartnerBank",
                schema: "mdm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerId = table.Column<long>(type: "bigint", nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    BankKey = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: false),
                    AccountHolder = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Iban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    Swift = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false, defaultValue: new DateOnly(9999, 12, 31)),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPartnerBank", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerBank_BusinessPartner_PartnerId",
                        column: x => x.PartnerId,
                        principalSchema: "mdm",
                        principalTable: "BusinessPartner",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPartnerIdentification",
                schema: "mdm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerId = table.Column<long>(type: "bigint", nullable: false),
                    IdentificationType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IdentificationNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    IssuingAuthority = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false, defaultValue: new DateOnly(9999, 12, 31)),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPartnerIdentification", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerIdentification_BusinessPartner_PartnerId",
                        column: x => x.PartnerId,
                        principalSchema: "mdm",
                        principalTable: "BusinessPartner",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPartnerRelationship",
                schema: "mdm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourcePartnerId = table.Column<long>(type: "bigint", nullable: false),
                    TargetPartnerId = table.Column<long>(type: "bigint", nullable: false),
                    RelationshipType = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false, defaultValue: new DateOnly(9999, 12, 31)),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPartnerRelationship", x => x.Id);
                    table.CheckConstraint("CK_BpRelationship_NotSelf", "[SourcePartnerId] <> [TargetPartnerId]");
                    table.ForeignKey(
                        name: "FK_BusinessPartnerRelationship_BusinessPartner_SourcePartnerId",
                        column: x => x.SourcePartnerId,
                        principalSchema: "mdm",
                        principalTable: "BusinessPartner",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerRelationship_BusinessPartner_TargetPartnerId",
                        column: x => x.TargetPartnerId,
                        principalSchema: "mdm",
                        principalTable: "BusinessPartner",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPartnerRoleAssignment",
                schema: "mdm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerId = table.Column<long>(type: "bigint", nullable: false),
                    PartnerRoleId = table.Column<long>(type: "bigint", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false, defaultValue: new DateOnly(9999, 12, 31)),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPartnerRoleAssignment", x => x.Id);
                    table.CheckConstraint("CK_BpRoleAssignment_Validity", "[ValidTo] > [ValidFrom]");
                    table.ForeignKey(
                        name: "FK_BusinessPartnerRoleAssignment_BusinessPartnerRole_PartnerRoleId",
                        column: x => x.PartnerRoleId,
                        principalSchema: "mdm",
                        principalTable: "BusinessPartnerRole",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerRoleAssignment_BusinessPartner_PartnerId",
                        column: x => x.PartnerId,
                        principalSchema: "mdm",
                        principalTable: "BusinessPartner",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPartnerTaxNumber",
                schema: "mdm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerId = table.Column<long>(type: "bigint", nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    TaxNumberType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TaxNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPartnerTaxNumber", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerTaxNumber_BusinessPartner_PartnerId",
                        column: x => x.PartnerId,
                        principalSchema: "mdm",
                        principalTable: "BusinessPartner",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FinancialStatementItem",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FinancialStatementVersionId = table.Column<long>(type: "bigint", nullable: false),
                    ParentItemId = table.Column<long>(type: "bigint", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    FromAccount = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ToAccount = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialStatementItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialStatementItem_FinancialStatementItem_ParentItemId",
                        column: x => x.ParentItemId,
                        principalSchema: "fin",
                        principalTable: "FinancialStatementItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialStatementItem_FinancialStatementVersion_FinancialStatementVersionId",
                        column: x => x.FinancialStatementVersionId,
                        principalSchema: "fin",
                        principalTable: "FinancialStatementVersion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GLAccount",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountNumber = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ChartOfAccountsId = table.Column<long>(type: "bigint", nullable: false),
                    GLAccountGroupId = table.Column<long>(type: "bigint", nullable: false),
                    ShortText = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LongText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccountType = table.Column<int>(type: "int", nullable: false),
                    IsReconciliationAccount = table.Column<bool>(type: "bit", nullable: false),
                    ReconciliationAccountType = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    IsRetainedEarningsAccount = table.Column<bool>(type: "bit", nullable: false),
                    IsBlocked = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GLAccount", x => x.Id);
                    table.CheckConstraint("CK_GLAccount_ReconciliationType", "([IsReconciliationAccount] = 0 AND [ReconciliationAccountType] IS NULL) OR ([IsReconciliationAccount] = 1 AND [ReconciliationAccountType] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_GLAccount_ChartOfAccounts_ChartOfAccountsId",
                        column: x => x.ChartOfAccountsId,
                        principalSchema: "fin",
                        principalTable: "ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GLAccount_GLAccountGroup_GLAccountGroupId",
                        column: x => x.GLAccountGroupId,
                        principalSchema: "fin",
                        principalTable: "GLAccountGroup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPartnerCreditProfile",
                schema: "mdm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerId = table.Column<long>(type: "bigint", nullable: false),
                    CreditControlAreaId = table.Column<long>(type: "bigint", nullable: false),
                    CreditLimit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    RiskClass = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    NextReviewDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsBlocked = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPartnerCreditProfile", x => x.Id);
                    table.CheckConstraint("CK_BpCreditProfile_Limit", "[CreditLimit] >= 0");
                    table.ForeignKey(
                        name: "FK_BusinessPartnerCreditProfile_BusinessPartner_PartnerId",
                        column: x => x.PartnerId,
                        principalSchema: "mdm",
                        principalTable: "BusinessPartner",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerCreditProfile_CreditControlArea_CreditControlAreaId",
                        column: x => x.CreditControlAreaId,
                        principalSchema: "org",
                        principalTable: "CreditControlArea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerCreditProfile_Currency_CurrencyId",
                        column: x => x.CurrencyId,
                        principalSchema: "cfg",
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyCode",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    LocalCurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    ChartOfAccountsId = table.Column<long>(type: "bigint", nullable: false),
                    FiscalYearVariantId = table.Column<long>(type: "bigint", nullable: false),
                    PostingPeriodVariantId = table.Column<long>(type: "bigint", nullable: false),
                    FieldStatusVariantId = table.Column<long>(type: "bigint", nullable: true),
                    CreditControlAreaId = table.Column<long>(type: "bigint", nullable: true),
                    AddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddressLine2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TaxNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyCode", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyCode_ChartOfAccounts_ChartOfAccountsId",
                        column: x => x.ChartOfAccountsId,
                        principalSchema: "fin",
                        principalTable: "ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyCode_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "org",
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyCode_CreditControlArea_CreditControlAreaId",
                        column: x => x.CreditControlAreaId,
                        principalSchema: "org",
                        principalTable: "CreditControlArea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyCode_Currency_LocalCurrencyId",
                        column: x => x.LocalCurrencyId,
                        principalSchema: "cfg",
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyCode_FiscalYearVariant_FiscalYearVariantId",
                        column: x => x.FiscalYearVariantId,
                        principalSchema: "cfg",
                        principalTable: "FiscalYearVariant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyCode_PostingPeriodVariant_PostingPeriodVariantId",
                        column: x => x.PostingPeriodVariantId,
                        principalSchema: "cfg",
                        principalTable: "PostingPeriodVariant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AllocationCycle",
                schema: "co",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ControllingAreaId = table.Column<long>(type: "bigint", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    AssessmentCostElementId = table.Column<long>(type: "bigint", nullable: true),
                    IsIterative = table.Column<bool>(type: "bit", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false, defaultValue: new DateOnly(9999, 12, 31)),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllocationCycle", x => x.Id);
                    table.CheckConstraint("CK_AllocationCycle_Assessment", "([Method] = 2 AND [AssessmentCostElementId] IS NOT NULL) OR [Method] = 1");
                    table.ForeignKey(
                        name: "FK_AllocationCycle_ControllingArea_ControllingAreaId",
                        column: x => x.ControllingAreaId,
                        principalSchema: "co",
                        principalTable: "ControllingArea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProfitCenter",
                schema: "co",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ControllingAreaId = table.Column<long>(type: "bigint", nullable: false),
                    SegmentId = table.Column<long>(type: "bigint", nullable: true),
                    ParentProfitCenterId = table.Column<long>(type: "bigint", nullable: true),
                    ResponsiblePerson = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsLockedForActualPosting = table.Column<bool>(type: "bit", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false, defaultValue: new DateOnly(9999, 12, 31)),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfitCenter", x => x.Id);
                    table.CheckConstraint("CK_ProfitCenter_Validity", "[ValidTo] > [ValidFrom]");
                    table.ForeignKey(
                        name: "FK_ProfitCenter_ControllingArea_ControllingAreaId",
                        column: x => x.ControllingAreaId,
                        principalSchema: "co",
                        principalTable: "ControllingArea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProfitCenter_ProfitCenter_ParentProfitCenterId",
                        column: x => x.ParentProfitCenterId,
                        principalSchema: "co",
                        principalTable: "ProfitCenter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProfitCenter_Segment_SegmentId",
                        column: x => x.SegmentId,
                        principalSchema: "org",
                        principalTable: "Segment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StatisticalKeyFigure",
                schema: "co",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ControllingAreaId = table.Column<long>(type: "bigint", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsTotalsValue = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatisticalKeyFigure", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatisticalKeyFigure_ControllingArea_ControllingAreaId",
                        column: x => x.ControllingAreaId,
                        principalSchema: "co",
                        principalTable: "ControllingArea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoleAuthorizationValue",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleAuthorizationId = table.Column<long>(type: "bigint", nullable: false),
                    AuthorizationFieldId = table.Column<long>(type: "bigint", nullable: false),
                    FromValue = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ToValue = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    IsWildcard = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleAuthorizationValue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleAuthorizationValue_AuthorizationField_AuthorizationFieldId",
                        column: x => x.AuthorizationFieldId,
                        principalSchema: "sec",
                        principalTable: "AuthorizationField",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleAuthorizationValue_RoleAuthorization_RoleAuthorizationId",
                        column: x => x.RoleAuthorizationId,
                        principalSchema: "sec",
                        principalTable: "RoleAuthorization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPartnerCommunication",
                schema: "mdm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerAddressId = table.Column<long>(type: "bigint", nullable: false),
                    CommunicationType = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPartnerCommunication", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerCommunication_BusinessPartnerAddress_PartnerAddressId",
                        column: x => x.PartnerAddressId,
                        principalSchema: "mdm",
                        principalTable: "BusinessPartnerAddress",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Branch",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branch", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Branch_CompanyCode_CompanyCodeId",
                        column: x => x.CompanyCodeId,
                        principalSchema: "org",
                        principalTable: "CompanyCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPartnerCompanyCode",
                schema: "mdm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: false),
                    ReconciliationAccountId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentTerms = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    PaymentMethods = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    DunningProcedure = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    ToleranceGroup = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    WithholdingTaxCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    IsPaymentBlocked = table.Column<bool>(type: "bit", nullable: false),
                    IsPostingBlocked = table.Column<bool>(type: "bit", nullable: false),
                    IsMarkedForDeletion = table.Column<bool>(type: "bit", nullable: false),
                    CorrespondenceType = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    Accountant = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPartnerCompanyCode", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerCompanyCode_BusinessPartner_PartnerId",
                        column: x => x.PartnerId,
                        principalSchema: "mdm",
                        principalTable: "BusinessPartner",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerCompanyCode_CompanyCode_CompanyCodeId",
                        column: x => x.CompanyCodeId,
                        principalSchema: "org",
                        principalTable: "CompanyCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerCompanyCode_GLAccount_ReconciliationAccountId",
                        column: x => x.ReconciliationAccountId,
                        principalSchema: "fin",
                        principalTable: "GLAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClearingHeader",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: false),
                    FiscalYear = table.Column<short>(type: "smallint", nullable: false),
                    ClearingDocumentNumber = table.Column<long>(type: "bigint", nullable: false),
                    ClearingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    DifferenceDocumentNumber = table.Column<long>(type: "bigint", nullable: true),
                    IsReset = table.Column<bool>(type: "bit", nullable: false),
                    ResetAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClearingHeader", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClearingHeader_CompanyCode_CompanyCodeId",
                        column: x => x.CompanyCodeId,
                        principalSchema: "org",
                        principalTable: "CompanyCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClearingHeader_Currency_CurrencyId",
                        column: x => x.CurrencyId,
                        principalSchema: "cfg",
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ControllingAreaCompanyCode",
                schema: "co",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ControllingAreaId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false, defaultValue: new DateOnly(9999, 12, 31)),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControllingAreaCompanyCode", x => x.Id);
                    table.CheckConstraint("CK_ControllingAreaCompanyCode_Validity", "[ValidTo] > [ValidFrom]");
                    table.ForeignKey(
                        name: "FK_ControllingAreaCompanyCode_CompanyCode_CompanyCodeId",
                        column: x => x.CompanyCodeId,
                        principalSchema: "org",
                        principalTable: "CompanyCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ControllingAreaCompanyCode_ControllingArea_ControllingAreaId",
                        column: x => x.ControllingAreaId,
                        principalSchema: "co",
                        principalTable: "ControllingArea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GLAccountCompanyCode",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GLAccountId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: false),
                    AccountCurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    IsOpenItemManaged = table.Column<bool>(type: "bit", nullable: false),
                    IsLineItemDisplayed = table.Column<bool>(type: "bit", nullable: false),
                    OnlyBalancesInLocalCurrency = table.Column<bool>(type: "bit", nullable: false),
                    FieldStatusGroup = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    DefaultTaxCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    IsTaxRelevant = table.Column<bool>(type: "bit", nullable: false),
                    IsPostingBlocked = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RequiresCostObject = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GLAccountCompanyCode", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GLAccountCompanyCode_CompanyCode_CompanyCodeId",
                        column: x => x.CompanyCodeId,
                        principalSchema: "org",
                        principalTable: "CompanyCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GLAccountCompanyCode_Currency_AccountCurrencyId",
                        column: x => x.AccountCurrencyId,
                        principalSchema: "cfg",
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GLAccountCompanyCode_GLAccount_GLAccountId",
                        column: x => x.GLAccountId,
                        principalSchema: "fin",
                        principalTable: "GLAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntryHeader",
                schema: "fin",
                columns: table => new
                {
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: false),
                    FiscalYear = table.Column<short>(type: "smallint", nullable: false),
                    DocumentNumber = table.Column<long>(type: "bigint", nullable: false),
                    DocumentNumberFormatted = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DocumentTypeId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PostingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FiscalPeriod = table.Column<byte>(type: "tinyint", nullable: false),
                    EntryDateUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    TranslationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DocumentCurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    ExchangeRateTypeId = table.Column<long>(type: "bigint", nullable: false),
                    ExchangeRateToLocal = table.Column<decimal>(type: "decimal(23,6)", precision: 19, scale: 4, nullable: false),
                    ExchangeRateToGroup = table.Column<decimal>(type: "decimal(23,6)", precision: 19, scale: 4, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    HeaderText = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SourceModule = table.Column<int>(type: "int", nullable: false),
                    TransactionCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ReversalOfDocumentNumber = table.Column<long>(type: "bigint", nullable: true),
                    ReversedByDocumentNumber = table.Column<long>(type: "bigint", nullable: true),
                    ReversalReasonCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    IntercompanyTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PostedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PostedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntryHeader", x => new { x.TenantId, x.CompanyCodeId, x.FiscalYear, x.DocumentNumber });
                    table.CheckConstraint("CK_JournalHeader_Period", "[FiscalPeriod] BETWEEN 1 AND 16");
                    table.CheckConstraint("CK_JournalHeader_Rates", "[ExchangeRateToLocal] > 0 AND [ExchangeRateToGroup] > 0");
                    table.CheckConstraint("CK_JournalHeader_Reversal", "[ReversalOfDocumentNumber] IS NULL OR [ReversedByDocumentNumber] IS NULL OR [ReversalOfDocumentNumber] <> [ReversedByDocumentNumber]");
                    table.ForeignKey(
                        name: "FK_JournalEntryHeader_CompanyCode_CompanyCodeId",
                        column: x => x.CompanyCodeId,
                        principalSchema: "org",
                        principalTable: "CompanyCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalEntryHeader_Currency_DocumentCurrencyId",
                        column: x => x.DocumentCurrencyId,
                        principalSchema: "cfg",
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalEntryHeader_DocumentType_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalSchema: "cfg",
                        principalTable: "DocumentType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalEntryHeader_ExchangeRateType_ExchangeRateTypeId",
                        column: x => x.ExchangeRateTypeId,
                        principalSchema: "cfg",
                        principalTable: "ExchangeRateType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NumberRange",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ObjectType = table.Column<int>(type: "int", nullable: false),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: true),
                    FiscalYear = table.Column<short>(type: "smallint", nullable: false),
                    FromNumber = table.Column<long>(type: "bigint", nullable: false),
                    ToNumber = table.Column<long>(type: "bigint", nullable: false),
                    CurrentNumber = table.Column<long>(type: "bigint", nullable: false),
                    IsGapless = table.Column<bool>(type: "bit", nullable: false),
                    IsExternal = table.Column<bool>(type: "bit", nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PaddingLength = table.Column<byte>(type: "tinyint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberRange", x => x.Id);
                    table.CheckConstraint("CK_NumberRange_Bounds", "[ToNumber] >= [FromNumber] AND [CurrentNumber] >= [FromNumber] - 1 AND [CurrentNumber] <= [ToNumber]");
                    table.ForeignKey(
                        name: "FK_NumberRange_CompanyCode_CompanyCodeId",
                        column: x => x.CompanyCodeId,
                        principalSchema: "org",
                        principalTable: "CompanyCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Plant",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plant", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Plant_CompanyCode_CompanyCodeId",
                        column: x => x.CompanyCodeId,
                        principalSchema: "org",
                        principalTable: "CompanyCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchasingOrganization",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchasingOrganization", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchasingOrganization_CompanyCode_CompanyCodeId",
                        column: x => x.CompanyCodeId,
                        principalSchema: "org",
                        principalTable: "CompanyCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrganization",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: false),
                    CurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrganization", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesOrganization_CompanyCode_CompanyCodeId",
                        column: x => x.CompanyCodeId,
                        principalSchema: "org",
                        principalTable: "CompanyCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrganization_Currency_CurrencyId",
                        column: x => x.CurrencyId,
                        principalSchema: "cfg",
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CostCenter",
                schema: "co",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ControllingAreaId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    ProfitCenterId = table.Column<long>(type: "bigint", nullable: true),
                    FunctionalAreaId = table.Column<long>(type: "bigint", nullable: true),
                    ParentCostCenterId = table.Column<long>(type: "bigint", nullable: true),
                    ResponsiblePerson = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    IsLockedForActualPosting = table.Column<bool>(type: "bit", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false, defaultValue: new DateOnly(9999, 12, 31)),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostCenter", x => x.Id);
                    table.CheckConstraint("CK_CostCenter_Validity", "[ValidTo] > [ValidFrom]");
                    table.ForeignKey(
                        name: "FK_CostCenter_CompanyCode_CompanyCodeId",
                        column: x => x.CompanyCodeId,
                        principalSchema: "org",
                        principalTable: "CompanyCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostCenter_ControllingArea_ControllingAreaId",
                        column: x => x.ControllingAreaId,
                        principalSchema: "co",
                        principalTable: "ControllingArea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostCenter_CostCenter_ParentCostCenterId",
                        column: x => x.ParentCostCenterId,
                        principalSchema: "co",
                        principalTable: "CostCenter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostCenter_Currency_CurrencyId",
                        column: x => x.CurrencyId,
                        principalSchema: "cfg",
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostCenter_FunctionalArea_FunctionalAreaId",
                        column: x => x.FunctionalAreaId,
                        principalSchema: "org",
                        principalTable: "FunctionalArea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostCenter_ProfitCenter_ProfitCenterId",
                        column: x => x.ProfitCenterId,
                        principalSchema: "co",
                        principalTable: "ProfitCenter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPartnerCustomer",
                schema: "mdm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerCompanyCodeId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerAccountNumber = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AccountGroup = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    IsOneTimeAccount = table.Column<bool>(type: "bit", nullable: false),
                    HeadOfficeAccount = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPartnerCustomer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerCustomer_BusinessPartnerCompanyCode_PartnerCompanyCodeId",
                        column: x => x.PartnerCompanyCodeId,
                        principalSchema: "mdm",
                        principalTable: "BusinessPartnerCompanyCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPartnerVendor",
                schema: "mdm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerCompanyCodeId = table.Column<long>(type: "bigint", nullable: false),
                    VendorAccountNumber = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AccountGroup = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    IsOneTimeAccount = table.Column<bool>(type: "bit", nullable: false),
                    CheckDoubleInvoice = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPartnerVendor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerVendor_BusinessPartnerCompanyCode_PartnerCompanyCodeId",
                        column: x => x.PartnerCompanyCodeId,
                        principalSchema: "mdm",
                        principalTable: "BusinessPartnerCompanyCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntryLine",
                schema: "fin",
                columns: table => new
                {
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: false),
                    FiscalYear = table.Column<short>(type: "smallint", nullable: false),
                    DocumentNumber = table.Column<long>(type: "bigint", nullable: false),
                    LedgerId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<short>(type: "smallint", nullable: false),
                    PostingKey = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DebitCredit = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    GLAccountId = table.Column<long>(type: "bigint", nullable: true),
                    BusinessPartnerId = table.Column<long>(type: "bigint", nullable: true),
                    BusinessPartnerRole = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AssetId = table.Column<long>(type: "bigint", nullable: true),
                    AssetSubNumber = table.Column<int>(type: "int", nullable: true),
                    AssetTransactionType = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    CostCenterId = table.Column<long>(type: "bigint", nullable: true),
                    ProfitCenterId = table.Column<long>(type: "bigint", nullable: true),
                    InternalOrderId = table.Column<long>(type: "bigint", nullable: true),
                    CostElementId = table.Column<long>(type: "bigint", nullable: true),
                    ActivityTypeId = table.Column<long>(type: "bigint", nullable: true),
                    BusinessAreaId = table.Column<long>(type: "bigint", nullable: true),
                    FunctionalAreaId = table.Column<long>(type: "bigint", nullable: true),
                    SegmentId = table.Column<long>(type: "bigint", nullable: true),
                    PlantId = table.Column<long>(type: "bigint", nullable: true),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    PartnerCompanyCodeId = table.Column<long>(type: "bigint", nullable: true),
                    PartnerProfitCenterId = table.Column<long>(type: "bigint", nullable: true),
                    PartnerSegmentId = table.Column<long>(type: "bigint", nullable: true),
                    DocumentAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    DocumentCurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    LocalAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    LocalCurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    GroupAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    GroupCurrencyId = table.Column<long>(type: "bigint", nullable: true),
                    ControllingAreaAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    ControllingAreaCurrencyId = table.Column<long>(type: "bigint", nullable: true),
                    HardCurrencyAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    HardCurrencyId = table.Column<long>(type: "bigint", nullable: true),
                    IndexCurrencyAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    IndexCurrencyId = table.Column<long>(type: "bigint", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(23,6)", precision: 19, scale: 4, nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    TaxCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    TaxBaseAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    TaxAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    IsTaxLine = table.Column<bool>(type: "bit", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BaselineDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PaymentTerms = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    PaymentMethod = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    PaymentBlock = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    Assignment = table.Column<string>(type: "nvarchar(18)", maxLength: 18, nullable: true),
                    LineText = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Reference1 = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Reference2 = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Reference3 = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    SourceModule = table.Column<int>(type: "int", nullable: false),
                    SourceDocumentType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    SourceDocumentNumber = table.Column<long>(type: "bigint", nullable: true),
                    SourceLineNumber = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntryLine", x => new { x.TenantId, x.CompanyCodeId, x.FiscalYear, x.DocumentNumber, x.LedgerId, x.LineNumber });
                    table.CheckConstraint("CK_JournalEntryLine_AccountObject", "([AccountType] = 'S' AND [GLAccountId] IS NOT NULL) OR ([AccountType] IN ('D','K') AND [BusinessPartnerId] IS NOT NULL) OR ([AccountType] = 'A' AND [AssetId] IS NOT NULL) OR ([AccountType] = 'M')");
                    table.CheckConstraint("CK_JournalEntryLine_LineNumber", "[LineNumber] > 0");
                    table.CheckConstraint("CK_JournalEntryLine_LocalSign", "([DebitCredit] = 'D' AND [LocalAmount] >= 0) OR ([DebitCredit] = 'C' AND [LocalAmount] <= 0)");
                    table.CheckConstraint("CK_JournalEntryLine_Sign", "([DebitCredit] = 'D' AND [DocumentAmount] >= 0) OR ([DebitCredit] = 'C' AND [DocumentAmount] <= 0)");
                    table.ForeignKey(
                        name: "FK_JournalEntryLine_JournalEntryHeader_TenantId_CompanyCodeId_FiscalYear_DocumentNumber",
                        columns: x => new { x.TenantId, x.CompanyCodeId, x.FiscalYear, x.DocumentNumber },
                        principalSchema: "fin",
                        principalTable: "JournalEntryHeader",
                        principalColumns: new[] { "TenantId", "CompanyCodeId", "FiscalYear", "DocumentNumber" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NumberRangeGap",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NumberRangeId = table.Column<long>(type: "bigint", nullable: false),
                    GapNumber = table.Column<long>(type: "bigint", nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberRangeGap", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NumberRangeGap_NumberRange_NumberRangeId",
                        column: x => x.NumberRangeId,
                        principalSchema: "cfg",
                        principalTable: "NumberRange",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPartnerPurchasingOrganization",
                schema: "mdm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerId = table.Column<long>(type: "bigint", nullable: false),
                    PurchasingOrganizationId = table.Column<long>(type: "bigint", nullable: false),
                    PurchasingGroup = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    OrderCurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    Incoterms = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    PaymentTerms = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    GoodsReceiptBasedInvoiceVerification = table.Column<bool>(type: "bit", nullable: false),
                    AutomaticPurchaseOrderAllowed = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPartnerPurchasingOrganization", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerPurchasingOrganization_BusinessPartner_PartnerId",
                        column: x => x.PartnerId,
                        principalSchema: "mdm",
                        principalTable: "BusinessPartner",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerPurchasingOrganization_Currency_OrderCurrencyId",
                        column: x => x.OrderCurrencyId,
                        principalSchema: "cfg",
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerPurchasingOrganization_PurchasingOrganization_PurchasingOrganizationId",
                        column: x => x.PurchasingOrganizationId,
                        principalSchema: "org",
                        principalTable: "PurchasingOrganization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPartnerSalesArea",
                schema: "mdm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerId = table.Column<long>(type: "bigint", nullable: false),
                    SalesOrganizationId = table.Column<long>(type: "bigint", nullable: false),
                    DistributionChannel = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Division = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    PriceGroup = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    PriceList = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    Incoterms = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    ShippingConditions = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    CustomerGroup = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    IsDeliveryBlocked = table.Column<bool>(type: "bit", nullable: false),
                    IsBillingBlocked = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPartnerSalesArea", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerSalesArea_BusinessPartner_PartnerId",
                        column: x => x.PartnerId,
                        principalSchema: "mdm",
                        principalTable: "BusinessPartner",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerSalesArea_Currency_CurrencyId",
                        column: x => x.CurrencyId,
                        principalSchema: "cfg",
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerSalesArea_SalesOrganization_SalesOrganizationId",
                        column: x => x.SalesOrganizationId,
                        principalSchema: "org",
                        principalTable: "SalesOrganization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InternalOrder",
                schema: "co",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    InternalOrderTypeId = table.Column<long>(type: "bigint", nullable: false),
                    ControllingAreaId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: false),
                    ResponsibleCostCenterId = table.Column<long>(type: "bigint", nullable: false),
                    ProfitCenterId = table.Column<long>(type: "bigint", nullable: true),
                    CurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    Budget = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    WarningTolerancePercent = table.Column<decimal>(type: "decimal(23,6)", precision: 19, scale: 4, nullable: true),
                    BlockingTolerancePercent = table.Column<decimal>(type: "decimal(23,6)", precision: 19, scale: 4, nullable: true),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    ClosedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalOrder", x => x.Id);
                    table.CheckConstraint("CK_InternalOrder_Tolerances", "([WarningTolerancePercent] IS NULL OR [WarningTolerancePercent] BETWEEN 0 AND 200) AND ([BlockingTolerancePercent] IS NULL OR [BlockingTolerancePercent] BETWEEN 0 AND 200)");
                    table.ForeignKey(
                        name: "FK_InternalOrder_CompanyCode_CompanyCodeId",
                        column: x => x.CompanyCodeId,
                        principalSchema: "org",
                        principalTable: "CompanyCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InternalOrder_ControllingArea_ControllingAreaId",
                        column: x => x.ControllingAreaId,
                        principalSchema: "co",
                        principalTable: "ControllingArea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InternalOrder_CostCenter_ResponsibleCostCenterId",
                        column: x => x.ResponsibleCostCenterId,
                        principalSchema: "co",
                        principalTable: "CostCenter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InternalOrder_Currency_CurrencyId",
                        column: x => x.CurrencyId,
                        principalSchema: "cfg",
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InternalOrder_InternalOrderType_InternalOrderTypeId",
                        column: x => x.InternalOrderTypeId,
                        principalSchema: "co",
                        principalTable: "InternalOrderType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InternalOrder_ProfitCenter_ProfitCenterId",
                        column: x => x.ProfitCenterId,
                        principalSchema: "co",
                        principalTable: "ProfitCenter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpenItem",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: false),
                    FiscalYear = table.Column<short>(type: "smallint", nullable: false),
                    DocumentNumber = table.Column<long>(type: "bigint", nullable: false),
                    LedgerId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<short>(type: "smallint", nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    BusinessPartnerId = table.Column<long>(type: "bigint", nullable: true),
                    BusinessPartnerRole = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    GLAccountId = table.Column<long>(type: "bigint", nullable: true),
                    OriginalAmountDocument = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    OpenAmountDocument = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    DocumentCurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    OriginalAmountLocal = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    OpenAmountLocal = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    LocalCurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PaymentTerms = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    ClearingStatus = table.Column<int>(type: "int", nullable: false),
                    ClearingDocumentNumber = table.Column<long>(type: "bigint", nullable: true),
                    ClearingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DunningLevel = table.Column<byte>(type: "tinyint", nullable: false),
                    LastDunningDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenItem", x => x.Id);
                    table.CheckConstraint("CK_OpenItem_ClearedConsistent", "([ClearingStatus] = 3 AND [OpenAmountDocument] = 0 AND [ClearingDocumentNumber] IS NOT NULL) OR ([ClearingStatus] <> 3)");
                    table.CheckConstraint("CK_OpenItem_OpenWithinOriginal", "ABS([OpenAmountDocument]) <= ABS([OriginalAmountDocument])");
                    table.ForeignKey(
                        name: "FK_OpenItem_JournalEntryLine_TenantId_CompanyCodeId_FiscalYear_DocumentNumber_LedgerId_LineNumber",
                        columns: x => new { x.TenantId, x.CompanyCodeId, x.FiscalYear, x.DocumentNumber, x.LedgerId, x.LineNumber },
                        principalSchema: "fin",
                        principalTable: "JournalEntryLine",
                        principalColumns: new[] { "TenantId", "CompanyCodeId", "FiscalYear", "DocumentNumber", "LedgerId", "LineNumber" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AllocationCycleSegment",
                schema: "co",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AllocationCycleId = table.Column<long>(type: "bigint", nullable: false),
                    SegmentNumber = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SenderCostCenterId = table.Column<long>(type: "bigint", nullable: false),
                    ReceiverCostCenterId = table.Column<long>(type: "bigint", nullable: true),
                    ReceiverInternalOrderId = table.Column<long>(type: "bigint", nullable: true),
                    FixedPercent = table.Column<decimal>(type: "decimal(23,6)", precision: 19, scale: 4, nullable: true),
                    FixedPortion = table.Column<decimal>(type: "decimal(23,6)", precision: 19, scale: 4, nullable: true),
                    StatisticalKeyFigureId = table.Column<long>(type: "bigint", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllocationCycleSegment", x => x.Id);
                    table.CheckConstraint("CK_AllocationSegment_OneReceiver", "(CASE WHEN [ReceiverCostCenterId] IS NULL THEN 0 ELSE 1 END +  CASE WHEN [ReceiverInternalOrderId] IS NULL THEN 0 ELSE 1 END) = 1");
                    table.CheckConstraint("CK_AllocationSegment_OneTracingFactor", "(CASE WHEN [FixedPercent] IS NULL THEN 0 ELSE 1 END +  CASE WHEN [FixedPortion] IS NULL THEN 0 ELSE 1 END +  CASE WHEN [StatisticalKeyFigureId] IS NULL THEN 0 ELSE 1 END) = 1");
                    table.ForeignKey(
                        name: "FK_AllocationCycleSegment_AllocationCycle_AllocationCycleId",
                        column: x => x.AllocationCycleId,
                        principalSchema: "co",
                        principalTable: "AllocationCycle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AllocationCycleSegment_CostCenter_ReceiverCostCenterId",
                        column: x => x.ReceiverCostCenterId,
                        principalSchema: "co",
                        principalTable: "CostCenter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AllocationCycleSegment_CostCenter_SenderCostCenterId",
                        column: x => x.SenderCostCenterId,
                        principalSchema: "co",
                        principalTable: "CostCenter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AllocationCycleSegment_InternalOrder_ReceiverInternalOrderId",
                        column: x => x.ReceiverInternalOrderId,
                        principalSchema: "co",
                        principalTable: "InternalOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AllocationCycleSegment_StatisticalKeyFigure_StatisticalKeyFigureId",
                        column: x => x.StatisticalKeyFigureId,
                        principalSchema: "co",
                        principalTable: "StatisticalKeyFigure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InternalOrderSettlementRule",
                schema: "co",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InternalOrderId = table.Column<long>(type: "bigint", nullable: false),
                    ReceiverType = table.Column<int>(type: "int", nullable: false),
                    ReceiverId = table.Column<long>(type: "bigint", nullable: false),
                    SharePercent = table.Column<decimal>(type: "decimal(23,6)", precision: 19, scale: 4, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false, defaultValue: new DateOnly(9999, 12, 31)),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalOrderSettlementRule", x => x.Id);
                    table.CheckConstraint("CK_SettlementRule_Share", "[SharePercent] > 0 AND [SharePercent] <= 100");
                    table.ForeignKey(
                        name: "FK_InternalOrderSettlementRule_InternalOrder_InternalOrderId",
                        column: x => x.InternalOrderId,
                        principalSchema: "co",
                        principalTable: "InternalOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClearingLine",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    ClearingHeaderId = table.Column<long>(type: "bigint", nullable: false),
                    OpenItemId = table.Column<long>(type: "bigint", nullable: false),
                    ClearedAmountDocument = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ClearedAmountLocal = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    IsResidual = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClearingLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClearingLine_ClearingHeader_ClearingHeaderId",
                        column: x => x.ClearingHeaderId,
                        principalSchema: "fin",
                        principalTable: "ClearingHeader",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClearingLine_OpenItem_OpenItemId",
                        column: x => x.OpenItemId,
                        principalSchema: "fin",
                        principalTable: "OpenItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AllocationCycle_ControllingAreaId",
                schema: "co",
                table: "AllocationCycle",
                column: "ControllingAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationCycle_TenantId_ControllingAreaId_Code_ValidFrom",
                schema: "co",
                table: "AllocationCycle",
                columns: new[] { "TenantId", "ControllingAreaId", "Code", "ValidFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AllocationCycleSegment_AllocationCycleId",
                schema: "co",
                table: "AllocationCycleSegment",
                column: "AllocationCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationCycleSegment_ReceiverCostCenterId",
                schema: "co",
                table: "AllocationCycleSegment",
                column: "ReceiverCostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationCycleSegment_ReceiverInternalOrderId",
                schema: "co",
                table: "AllocationCycleSegment",
                column: "ReceiverInternalOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationCycleSegment_SenderCostCenterId",
                schema: "co",
                table: "AllocationCycleSegment",
                column: "SenderCostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationCycleSegment_StatisticalKeyFigureId",
                schema: "co",
                table: "AllocationCycleSegment",
                column: "StatisticalKeyFigureId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationCycleSegment_TenantId_AllocationCycleId_SegmentNumber",
                schema: "co",
                table: "AllocationCycleSegment",
                columns: new[] { "TenantId", "AllocationCycleId", "SegmentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_CorrelationId",
                schema: "audit",
                table: "AuditLog",
                column: "CorrelationId",
                filter: "[CorrelationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_TenantId_ObjectType_ObjectId",
                schema: "audit",
                table: "AuditLog",
                columns: new[] { "TenantId", "ObjectType", "ObjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_TenantId_OccurredAtUtc",
                schema: "audit",
                table: "AuditLog",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_TenantId_UserName_OccurredAtUtc",
                schema: "audit",
                table: "AuditLog",
                columns: new[] { "TenantId", "UserName", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogField_AuditLogId",
                schema: "audit",
                table: "AuditLogField",
                column: "AuditLogId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogField_TenantId_AuditLogId",
                schema: "audit",
                table: "AuditLogField",
                columns: new[] { "TenantId", "AuditLogId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthorizationField_AuthorizationObjectId",
                schema: "sec",
                table: "AuthorizationField",
                column: "AuthorizationObjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthorizationField_TenantId_AuthorizationObjectId_Code",
                schema: "sec",
                table: "AuthorizationField",
                columns: new[] { "TenantId", "AuthorizationObjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuthorizationObject_TenantId_Code",
                schema: "sec",
                table: "AuthorizationObject",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Branch_CompanyCodeId",
                schema: "org",
                table: "Branch",
                column: "CompanyCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Branch_TenantId_Code",
                schema: "org",
                table: "Branch",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessArea_TenantId_Code",
                schema: "org",
                table: "BusinessArea",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartner_PartnerGroupId",
                schema: "mdm",
                table: "BusinessPartner",
                column: "PartnerGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartner_TenantId_NormalizedName",
                schema: "mdm",
                table: "BusinessPartner",
                columns: new[] { "TenantId", "NormalizedName" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartner_TenantId_PartnerNumber",
                schema: "mdm",
                table: "BusinessPartner",
                columns: new[] { "TenantId", "PartnerNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartner_TenantId_SearchTerm1",
                schema: "mdm",
                table: "BusinessPartner",
                columns: new[] { "TenantId", "SearchTerm1" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerAddress_PartnerId",
                schema: "mdm",
                table: "BusinessPartnerAddress",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerAddress_TenantId_PartnerId_AddressType",
                schema: "mdm",
                table: "BusinessPartnerAddress",
                columns: new[] { "TenantId", "PartnerId", "AddressType" });

            migrationBuilder.CreateIndex(
                name: "UX_BpAddress_OneDefaultPerType",
                schema: "mdm",
                table: "BusinessPartnerAddress",
                columns: new[] { "TenantId", "PartnerId", "AddressType", "IsDefault" },
                unique: true,
                filter: "[IsDefault] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerBank_PartnerId",
                schema: "mdm",
                table: "BusinessPartnerBank",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerBank_TenantId_PartnerId",
                schema: "mdm",
                table: "BusinessPartnerBank",
                columns: new[] { "TenantId", "PartnerId" });

            migrationBuilder.CreateIndex(
                name: "UX_BpBank_Account",
                schema: "mdm",
                table: "BusinessPartnerBank",
                columns: new[] { "TenantId", "CountryCode", "BankKey", "AccountNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerCommunication_PartnerAddressId",
                schema: "mdm",
                table: "BusinessPartnerCommunication",
                column: "PartnerAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerCommunication_TenantId_PartnerAddressId_CommunicationType",
                schema: "mdm",
                table: "BusinessPartnerCommunication",
                columns: new[] { "TenantId", "PartnerAddressId", "CommunicationType" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerCompanyCode_CompanyCodeId",
                schema: "mdm",
                table: "BusinessPartnerCompanyCode",
                column: "CompanyCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerCompanyCode_PartnerId",
                schema: "mdm",
                table: "BusinessPartnerCompanyCode",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerCompanyCode_ReconciliationAccountId",
                schema: "mdm",
                table: "BusinessPartnerCompanyCode",
                column: "ReconciliationAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerCompanyCode_TenantId_PartnerId_CompanyCodeId",
                schema: "mdm",
                table: "BusinessPartnerCompanyCode",
                columns: new[] { "TenantId", "PartnerId", "CompanyCodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerCreditProfile_CreditControlAreaId",
                schema: "mdm",
                table: "BusinessPartnerCreditProfile",
                column: "CreditControlAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerCreditProfile_CurrencyId",
                schema: "mdm",
                table: "BusinessPartnerCreditProfile",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerCreditProfile_PartnerId",
                schema: "mdm",
                table: "BusinessPartnerCreditProfile",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerCreditProfile_TenantId_PartnerId_CreditControlAreaId",
                schema: "mdm",
                table: "BusinessPartnerCreditProfile",
                columns: new[] { "TenantId", "PartnerId", "CreditControlAreaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerCustomer_PartnerCompanyCodeId",
                schema: "mdm",
                table: "BusinessPartnerCustomer",
                column: "PartnerCompanyCodeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerCustomer_TenantId_CustomerAccountNumber",
                schema: "mdm",
                table: "BusinessPartnerCustomer",
                columns: new[] { "TenantId", "CustomerAccountNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerGroup_TenantId_Code",
                schema: "mdm",
                table: "BusinessPartnerGroup",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerIdentification_PartnerId",
                schema: "mdm",
                table: "BusinessPartnerIdentification",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerIdentification_TenantId_PartnerId_IdentificationType",
                schema: "mdm",
                table: "BusinessPartnerIdentification",
                columns: new[] { "TenantId", "PartnerId", "IdentificationType" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerPurchasingOrganization_OrderCurrencyId",
                schema: "mdm",
                table: "BusinessPartnerPurchasingOrganization",
                column: "OrderCurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerPurchasingOrganization_PartnerId",
                schema: "mdm",
                table: "BusinessPartnerPurchasingOrganization",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerPurchasingOrganization_PurchasingOrganizationId",
                schema: "mdm",
                table: "BusinessPartnerPurchasingOrganization",
                column: "PurchasingOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerPurchasingOrganization_TenantId_PartnerId_PurchasingOrganizationId",
                schema: "mdm",
                table: "BusinessPartnerPurchasingOrganization",
                columns: new[] { "TenantId", "PartnerId", "PurchasingOrganizationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerRelationship_SourcePartnerId",
                schema: "mdm",
                table: "BusinessPartnerRelationship",
                column: "SourcePartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerRelationship_TargetPartnerId",
                schema: "mdm",
                table: "BusinessPartnerRelationship",
                column: "TargetPartnerId");

            migrationBuilder.CreateIndex(
                name: "UX_BpRelationship_Open",
                schema: "mdm",
                table: "BusinessPartnerRelationship",
                columns: new[] { "TenantId", "SourcePartnerId", "TargetPartnerId", "RelationshipType" },
                unique: true,
                filter: "[ValidTo] = '9999-12-31'");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerRole_TenantId_Code",
                schema: "mdm",
                table: "BusinessPartnerRole",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerRoleAssignment_PartnerId",
                schema: "mdm",
                table: "BusinessPartnerRoleAssignment",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerRoleAssignment_PartnerRoleId",
                schema: "mdm",
                table: "BusinessPartnerRoleAssignment",
                column: "PartnerRoleId");

            migrationBuilder.CreateIndex(
                name: "UX_BpRoleAssignment_Open",
                schema: "mdm",
                table: "BusinessPartnerRoleAssignment",
                columns: new[] { "TenantId", "PartnerId", "PartnerRoleId" },
                unique: true,
                filter: "[ValidTo] = '9999-12-31'");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerRoleCategory_PartnerRoleId",
                schema: "mdm",
                table: "BusinessPartnerRoleCategory",
                column: "PartnerRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerRoleCategory_TenantId_PartnerRoleId_Category",
                schema: "mdm",
                table: "BusinessPartnerRoleCategory",
                columns: new[] { "TenantId", "PartnerRoleId", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerSalesArea_CurrencyId",
                schema: "mdm",
                table: "BusinessPartnerSalesArea",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerSalesArea_PartnerId",
                schema: "mdm",
                table: "BusinessPartnerSalesArea",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerSalesArea_SalesOrganizationId",
                schema: "mdm",
                table: "BusinessPartnerSalesArea",
                column: "SalesOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerSalesArea_TenantId_PartnerId_SalesOrganizationId_DistributionChannel_Division",
                schema: "mdm",
                table: "BusinessPartnerSalesArea",
                columns: new[] { "TenantId", "PartnerId", "SalesOrganizationId", "DistributionChannel", "Division" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerTaxNumber_PartnerId",
                schema: "mdm",
                table: "BusinessPartnerTaxNumber",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "UX_BpTaxNumber_Unique",
                schema: "mdm",
                table: "BusinessPartnerTaxNumber",
                columns: new[] { "TenantId", "CountryCode", "TaxNumberType", "TaxNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerVendor_PartnerCompanyCodeId",
                schema: "mdm",
                table: "BusinessPartnerVendor",
                column: "PartnerCompanyCodeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerVendor_TenantId_VendorAccountNumber",
                schema: "mdm",
                table: "BusinessPartnerVendor",
                columns: new[] { "TenantId", "VendorAccountNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccounts_TenantId_Code",
                schema: "fin",
                table: "ChartOfAccounts",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClearingHeader_CompanyCodeId",
                schema: "fin",
                table: "ClearingHeader",
                column: "CompanyCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_ClearingHeader_CurrencyId",
                schema: "fin",
                table: "ClearingHeader",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_ClearingHeader_TenantId_CompanyCodeId_FiscalYear_ClearingDocumentNumber",
                schema: "fin",
                table: "ClearingHeader",
                columns: new[] { "TenantId", "CompanyCodeId", "FiscalYear", "ClearingDocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClearingLine_ClearingHeaderId",
                schema: "fin",
                table: "ClearingLine",
                column: "ClearingHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_ClearingLine_OpenItemId",
                schema: "fin",
                table: "ClearingLine",
                column: "OpenItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ClearingLine_TenantId_OpenItemId",
                schema: "fin",
                table: "ClearingLine",
                columns: new[] { "TenantId", "OpenItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Company_GroupCurrencyId",
                schema: "org",
                table: "Company",
                column: "GroupCurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Company_TenantId_Code",
                schema: "org",
                table: "Company",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCode_ChartOfAccountsId",
                schema: "org",
                table: "CompanyCode",
                column: "ChartOfAccountsId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCode_CompanyId",
                schema: "org",
                table: "CompanyCode",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCode_CreditControlAreaId",
                schema: "org",
                table: "CompanyCode",
                column: "CreditControlAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCode_FiscalYearVariantId",
                schema: "org",
                table: "CompanyCode",
                column: "FiscalYearVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCode_LocalCurrencyId",
                schema: "org",
                table: "CompanyCode",
                column: "LocalCurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCode_PostingPeriodVariantId",
                schema: "org",
                table: "CompanyCode",
                column: "PostingPeriodVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCode_TenantId_Code",
                schema: "org",
                table: "CompanyCode",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ControllingArea_CurrencyId",
                schema: "co",
                table: "ControllingArea",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_ControllingArea_FiscalYearVariantId",
                schema: "co",
                table: "ControllingArea",
                column: "FiscalYearVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_ControllingArea_TenantId_Code",
                schema: "co",
                table: "ControllingArea",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ControllingAreaCompanyCode_CompanyCodeId",
                schema: "co",
                table: "ControllingAreaCompanyCode",
                column: "CompanyCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_ControllingAreaCompanyCode_ControllingAreaId",
                schema: "co",
                table: "ControllingAreaCompanyCode",
                column: "ControllingAreaId");

            migrationBuilder.CreateIndex(
                name: "UX_ControllingAreaCompanyCode_Open",
                schema: "co",
                table: "ControllingAreaCompanyCode",
                columns: new[] { "TenantId", "ControllingAreaId", "CompanyCodeId" },
                unique: true,
                filter: "[ValidTo] = '9999-12-31'");

            migrationBuilder.CreateIndex(
                name: "IX_CostCenter_CompanyCodeId",
                schema: "co",
                table: "CostCenter",
                column: "CompanyCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_CostCenter_ControllingAreaId",
                schema: "co",
                table: "CostCenter",
                column: "ControllingAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_CostCenter_CurrencyId",
                schema: "co",
                table: "CostCenter",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_CostCenter_FunctionalAreaId",
                schema: "co",
                table: "CostCenter",
                column: "FunctionalAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_CostCenter_ParentCostCenterId",
                schema: "co",
                table: "CostCenter",
                column: "ParentCostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_CostCenter_ProfitCenterId",
                schema: "co",
                table: "CostCenter",
                column: "ProfitCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_CostCenter_TenantId_CompanyCodeId",
                schema: "co",
                table: "CostCenter",
                columns: new[] { "TenantId", "CompanyCodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_CostCenter_TenantId_ControllingAreaId_Code_ValidFrom",
                schema: "co",
                table: "CostCenter",
                columns: new[] { "TenantId", "ControllingAreaId", "Code", "ValidFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditControlArea_CurrencyId",
                schema: "org",
                table: "CreditControlArea",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditControlArea_TenantId_Code",
                schema: "org",
                table: "CreditControlArea",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Currency_TenantId_Code",
                schema: "cfg",
                table: "Currency",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentType_TenantId_Code",
                schema: "cfg",
                table: "DocumentType",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRate_ExchangeRateTypeId",
                schema: "cfg",
                table: "ExchangeRate",
                column: "ExchangeRateTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRate_FromCurrencyId",
                schema: "cfg",
                table: "ExchangeRate",
                column: "FromCurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRate_TenantId_ExchangeRateTypeId_FromCurrencyId_ToCurrencyId_ValidFrom",
                schema: "cfg",
                table: "ExchangeRate",
                columns: new[] { "TenantId", "ExchangeRateTypeId", "FromCurrencyId", "ToCurrencyId", "ValidFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRate_ToCurrencyId",
                schema: "cfg",
                table: "ExchangeRate",
                column: "ToCurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRateType_TenantId_Code",
                schema: "cfg",
                table: "ExchangeRateType",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementItem_FinancialStatementVersionId",
                schema: "fin",
                table: "FinancialStatementItem",
                column: "FinancialStatementVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementItem_ParentItemId",
                schema: "fin",
                table: "FinancialStatementItem",
                column: "ParentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementItem_TenantId_FinancialStatementVersionId_Code",
                schema: "fin",
                table: "FinancialStatementItem",
                columns: new[] { "TenantId", "FinancialStatementVersionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementVersion_ChartOfAccountsId",
                schema: "fin",
                table: "FinancialStatementVersion",
                column: "ChartOfAccountsId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementVersion_TenantId_Code",
                schema: "fin",
                table: "FinancialStatementVersion",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYearPeriod_FiscalYearVariantId",
                schema: "cfg",
                table: "FiscalYearPeriod",
                column: "FiscalYearVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYearPeriod_TenantId_FiscalYearVariantId_FiscalYear_Period",
                schema: "cfg",
                table: "FiscalYearPeriod",
                columns: new[] { "TenantId", "FiscalYearVariantId", "FiscalYear", "Period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYearPeriod_TenantId_FiscalYearVariantId_StartDate_EndDate",
                schema: "cfg",
                table: "FiscalYearPeriod",
                columns: new[] { "TenantId", "FiscalYearVariantId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYearVariant_TenantId_Code",
                schema: "cfg",
                table: "FiscalYearVariant",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FunctionalArea_TenantId_Code",
                schema: "org",
                table: "FunctionalArea",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GLAccount_ChartOfAccountsId",
                schema: "fin",
                table: "GLAccount",
                column: "ChartOfAccountsId");

            migrationBuilder.CreateIndex(
                name: "IX_GLAccount_GLAccountGroupId",
                schema: "fin",
                table: "GLAccount",
                column: "GLAccountGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GLAccount_TenantId_ChartOfAccountsId_AccountNumber",
                schema: "fin",
                table: "GLAccount",
                columns: new[] { "TenantId", "ChartOfAccountsId", "AccountNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GLAccountCompanyCode_AccountCurrencyId",
                schema: "fin",
                table: "GLAccountCompanyCode",
                column: "AccountCurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_GLAccountCompanyCode_CompanyCodeId",
                schema: "fin",
                table: "GLAccountCompanyCode",
                column: "CompanyCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_GLAccountCompanyCode_GLAccountId",
                schema: "fin",
                table: "GLAccountCompanyCode",
                column: "GLAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_GLAccountCompanyCode_TenantId_GLAccountId_CompanyCodeId",
                schema: "fin",
                table: "GLAccountCompanyCode",
                columns: new[] { "TenantId", "GLAccountId", "CompanyCodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GLAccountGroup_ChartOfAccountsId",
                schema: "fin",
                table: "GLAccountGroup",
                column: "ChartOfAccountsId");

            migrationBuilder.CreateIndex(
                name: "IX_GLAccountGroup_TenantId_ChartOfAccountsId_Code",
                schema: "fin",
                table: "GLAccountGroup",
                columns: new[] { "TenantId", "ChartOfAccountsId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InternalOrder_CompanyCodeId",
                schema: "co",
                table: "InternalOrder",
                column: "CompanyCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalOrder_ControllingAreaId",
                schema: "co",
                table: "InternalOrder",
                column: "ControllingAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalOrder_CurrencyId",
                schema: "co",
                table: "InternalOrder",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalOrder_InternalOrderTypeId",
                schema: "co",
                table: "InternalOrder",
                column: "InternalOrderTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalOrder_ProfitCenterId",
                schema: "co",
                table: "InternalOrder",
                column: "ProfitCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalOrder_ResponsibleCostCenterId",
                schema: "co",
                table: "InternalOrder",
                column: "ResponsibleCostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalOrder_TenantId_Code",
                schema: "co",
                table: "InternalOrder",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InternalOrder_TenantId_ControllingAreaId_IsClosed",
                schema: "co",
                table: "InternalOrder",
                columns: new[] { "TenantId", "ControllingAreaId", "IsClosed" });

            migrationBuilder.CreateIndex(
                name: "IX_InternalOrderSettlementRule_InternalOrderId",
                schema: "co",
                table: "InternalOrderSettlementRule",
                column: "InternalOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalOrderSettlementRule_TenantId_InternalOrderId",
                schema: "co",
                table: "InternalOrderSettlementRule",
                columns: new[] { "TenantId", "InternalOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_InternalOrderType_TenantId_Code",
                schema: "co",
                table: "InternalOrderType",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryHeader_CompanyCodeId",
                schema: "fin",
                table: "JournalEntryHeader",
                column: "CompanyCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryHeader_DocumentCurrencyId",
                schema: "fin",
                table: "JournalEntryHeader",
                column: "DocumentCurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryHeader_DocumentTypeId",
                schema: "fin",
                table: "JournalEntryHeader",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryHeader_ExchangeRateTypeId",
                schema: "fin",
                table: "JournalEntryHeader",
                column: "ExchangeRateTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryHeader_IntercompanyTransactionId",
                schema: "fin",
                table: "JournalEntryHeader",
                column: "IntercompanyTransactionId",
                filter: "[IntercompanyTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryHeader_TenantId_CompanyCodeId_DocumentNumberFormatted",
                schema: "fin",
                table: "JournalEntryHeader",
                columns: new[] { "TenantId", "CompanyCodeId", "DocumentNumberFormatted" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryHeader_TenantId_CompanyCodeId_PostingDate",
                schema: "fin",
                table: "JournalEntryHeader",
                columns: new[] { "TenantId", "CompanyCodeId", "PostingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryHeader_TenantId_Status",
                schema: "fin",
                table: "JournalEntryHeader",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLine_Account",
                schema: "fin",
                table: "JournalEntryLine",
                columns: new[] { "TenantId", "CompanyCodeId", "LedgerId", "GLAccountId" })
                .Annotation("SqlServer:Include", new[] { "LocalAmount", "GroupAmount", "DocumentAmount" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLine_CostCenter",
                schema: "fin",
                table: "JournalEntryLine",
                columns: new[] { "TenantId", "CostCenterId" },
                filter: "[CostCenterId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLine_Partner",
                schema: "fin",
                table: "JournalEntryLine",
                columns: new[] { "TenantId", "CompanyCodeId", "BusinessPartnerId" },
                filter: "[BusinessPartnerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLine_ProfitCenter",
                schema: "fin",
                table: "JournalEntryLine",
                columns: new[] { "TenantId", "ProfitCenterId" },
                filter: "[ProfitCenterId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Ledger_TenantId_Code",
                schema: "fin",
                table: "Ledger",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Ledger_OneLeadingPerTenant",
                schema: "fin",
                table: "Ledger",
                column: "TenantId",
                unique: true,
                filter: "[LedgerType] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_LoginHistory_TenantId_AttemptedAtUtc",
                schema: "sec",
                table: "LoginHistory",
                columns: new[] { "TenantId", "AttemptedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginHistory_TenantId_AttemptedUserName_AttemptedAtUtc",
                schema: "sec",
                table: "LoginHistory",
                columns: new[] { "TenantId", "AttemptedUserName", "AttemptedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NumberRange_CompanyCodeId",
                schema: "cfg",
                table: "NumberRange",
                column: "CompanyCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_NumberRange_TenantId_ObjectType_Code_CompanyCodeId_FiscalYear",
                schema: "cfg",
                table: "NumberRange",
                columns: new[] { "TenantId", "ObjectType", "Code", "CompanyCodeId", "FiscalYear" },
                unique: true,
                filter: "[CompanyCodeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NumberRangeGap_NumberRangeId",
                schema: "cfg",
                table: "NumberRangeGap",
                column: "NumberRangeId");

            migrationBuilder.CreateIndex(
                name: "IX_NumberRangeGap_TenantId_NumberRangeId_GapNumber",
                schema: "cfg",
                table: "NumberRangeGap",
                columns: new[] { "TenantId", "NumberRangeId", "GapNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenItem_Account",
                schema: "fin",
                table: "OpenItem",
                columns: new[] { "TenantId", "CompanyCodeId", "GLAccountId", "ClearingStatus" },
                filter: "[GLAccountId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OpenItem_PartnerDue",
                schema: "fin",
                table: "OpenItem",
                columns: new[] { "TenantId", "CompanyCodeId", "BusinessPartnerId", "ClearingStatus", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "UX_OpenItem_JournalLine",
                schema: "fin",
                table: "OpenItem",
                columns: new[] { "TenantId", "CompanyCodeId", "FiscalYear", "DocumentNumber", "LedgerId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordHistory_TenantId_UserId_ChangedAtUtc",
                schema: "sec",
                table: "PasswordHistory",
                columns: new[] { "TenantId", "UserId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordHistory_UserId",
                schema: "sec",
                table: "PasswordHistory",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Permission_TenantId_Code",
                schema: "sec",
                table: "Permission",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plant_CompanyCodeId",
                schema: "org",
                table: "Plant",
                column: "CompanyCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Plant_TenantId_Code",
                schema: "org",
                table: "Plant",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostingIdempotency_TenantId_IdempotencyKey",
                schema: "fin",
                table: "PostingIdempotency",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostingKey_TenantId_Code",
                schema: "cfg",
                table: "PostingKey",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostingPeriod_PostingPeriodVariantId",
                schema: "cfg",
                table: "PostingPeriod",
                column: "PostingPeriodVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_PostingPeriod_TenantId_PostingPeriodVariantId_AccountType_FiscalYear_Period",
                schema: "cfg",
                table: "PostingPeriod",
                columns: new[] { "TenantId", "PostingPeriodVariantId", "AccountType", "FiscalYear", "Period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostingPeriodVariant_TenantId_Code",
                schema: "cfg",
                table: "PostingPeriodVariant",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfitCenter_ControllingAreaId",
                schema: "co",
                table: "ProfitCenter",
                column: "ControllingAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfitCenter_ParentProfitCenterId",
                schema: "co",
                table: "ProfitCenter",
                column: "ParentProfitCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfitCenter_SegmentId",
                schema: "co",
                table: "ProfitCenter",
                column: "SegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfitCenter_TenantId_ControllingAreaId_Code_ValidFrom",
                schema: "co",
                table: "ProfitCenter",
                columns: new[] { "TenantId", "ControllingAreaId", "Code", "ValidFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchasingOrganization_CompanyCodeId",
                schema: "org",
                table: "PurchasingOrganization",
                column: "CompanyCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchasingOrganization_TenantId_Code",
                schema: "org",
                table: "PurchasingOrganization",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Role_TenantId_Code",
                schema: "sec",
                table: "Role",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleAuthorization_AuthorizationObjectId",
                schema: "sec",
                table: "RoleAuthorization",
                column: "AuthorizationObjectId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleAuthorization_RoleId",
                schema: "sec",
                table: "RoleAuthorization",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleAuthorization_TenantId_RoleId_AuthorizationObjectId",
                schema: "sec",
                table: "RoleAuthorization",
                columns: new[] { "TenantId", "RoleId", "AuthorizationObjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_RoleAuthorizationValue_AuthorizationFieldId",
                schema: "sec",
                table: "RoleAuthorizationValue",
                column: "AuthorizationFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleAuthorizationValue_RoleAuthorizationId",
                schema: "sec",
                table: "RoleAuthorizationValue",
                column: "RoleAuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleAuthorizationValue_TenantId_RoleAuthorizationId_AuthorizationFieldId",
                schema: "sec",
                table: "RoleAuthorizationValue",
                columns: new[] { "TenantId", "RoleAuthorizationId", "AuthorizationFieldId" });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermission_PermissionId",
                schema: "sec",
                table: "RolePermission",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermission_RoleId",
                schema: "sec",
                table: "RolePermission",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermission_TenantId_RoleId_PermissionId",
                schema: "sec",
                table: "RolePermission",
                columns: new[] { "TenantId", "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleTransactionCode_RoleId",
                schema: "sec",
                table: "RoleTransactionCode",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleTransactionCode_TenantId_RoleId_TransactionCodeId",
                schema: "sec",
                table: "RoleTransactionCode",
                columns: new[] { "TenantId", "RoleId", "TransactionCodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrganization_CompanyCodeId",
                schema: "org",
                table: "SalesOrganization",
                column: "CompanyCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrganization_CurrencyId",
                schema: "org",
                table: "SalesOrganization",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrganization_TenantId_Code",
                schema: "org",
                table: "SalesOrganization",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Segment_TenantId_Code",
                schema: "org",
                table: "Segment",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SegregationOfDutiesRule_TenantId_Code",
                schema: "sec",
                table: "SegregationOfDutiesRule",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatisticalKeyFigure_ControllingAreaId",
                schema: "co",
                table: "StatisticalKeyFigure",
                column: "ControllingAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_StatisticalKeyFigure_TenantId_ControllingAreaId_Code",
                schema: "co",
                table: "StatisticalKeyFigure",
                columns: new[] { "TenantId", "ControllingAreaId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TableBrowserLog_TenantId_ExecutedAtUtc",
                schema: "audit",
                table: "TableBrowserLog",
                columns: new[] { "TenantId", "ExecutedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TableBrowserLog_TenantId_TableName_ExecutedAtUtc",
                schema: "audit",
                table: "TableBrowserLog",
                columns: new[] { "TenantId", "TableName", "ExecutedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TableBrowserLog_TenantId_UserName_ExecutedAtUtc",
                schema: "audit",
                table: "TableBrowserLog",
                columns: new[] { "TenantId", "UserName", "ExecutedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxCode_TenantId_CountryCode_Code_ValidFrom",
                schema: "cfg",
                table: "TaxCode",
                columns: new[] { "TenantId", "CountryCode", "Code", "ValidFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenant_Code",
                schema: "org",
                table: "Tenant",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionCode_TenantId_Category",
                schema: "cfg",
                table: "TransactionCode",
                columns: new[] { "TenantId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionCode_TenantId_Code",
                schema: "cfg",
                table: "TransactionCode",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_User_TenantId_Email",
                schema: "sec",
                table: "User",
                columns: new[] { "TenantId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_User_TenantId_UserName",
                schema: "sec",
                table: "User",
                columns: new[] { "TenantId", "UserName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanyCode_TenantId_UserId_CompanyCodeId",
                schema: "sec",
                table: "UserCompanyCode",
                columns: new[] { "TenantId", "UserId", "CompanyCodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanyCode_UserId",
                schema: "sec",
                table: "UserCompanyCode",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_RoleId",
                schema: "sec",
                table: "UserRole",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_UserId",
                schema: "sec",
                table: "UserRole",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_UserRole_Open",
                schema: "sec",
                table: "UserRole",
                columns: new[] { "TenantId", "UserId", "RoleId" },
                unique: true,
                filter: "[ValidTo] = '9999-12-31'");

            migrationBuilder.CreateIndex(
                name: "IX_UserSession_SessionId",
                schema: "sec",
                table: "UserSession",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSession_TenantId_UserId_EndedAtUtc",
                schema: "sec",
                table: "UserSession",
                columns: new[] { "TenantId", "UserId", "EndedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSession_UserId",
                schema: "sec",
                table: "UserSession",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubstitution_SubstituteUserId",
                schema: "sec",
                table: "UserSubstitution",
                column: "SubstituteUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubstitution_TenantId_UserId_ValidFrom",
                schema: "sec",
                table: "UserSubstitution",
                columns: new[] { "TenantId", "UserId", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSubstitution_UserId",
                schema: "sec",
                table: "UserSubstitution",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AllocationCycleSegment",
                schema: "co");

            migrationBuilder.DropTable(
                name: "AuditLogField",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "Branch",
                schema: "org");

            migrationBuilder.DropTable(
                name: "BusinessArea",
                schema: "org");

            migrationBuilder.DropTable(
                name: "BusinessPartnerBank",
                schema: "mdm");

            migrationBuilder.DropTable(
                name: "BusinessPartnerCommunication",
                schema: "mdm");

            migrationBuilder.DropTable(
                name: "BusinessPartnerCreditProfile",
                schema: "mdm");

            migrationBuilder.DropTable(
                name: "BusinessPartnerCustomer",
                schema: "mdm");

            migrationBuilder.DropTable(
                name: "BusinessPartnerIdentification",
                schema: "mdm");

            migrationBuilder.DropTable(
                name: "BusinessPartnerPurchasingOrganization",
                schema: "mdm");

            migrationBuilder.DropTable(
                name: "BusinessPartnerRelationship",
                schema: "mdm");

            migrationBuilder.DropTable(
                name: "BusinessPartnerRoleAssignment",
                schema: "mdm");

            migrationBuilder.DropTable(
                name: "BusinessPartnerRoleCategory",
                schema: "mdm");

            migrationBuilder.DropTable(
                name: "BusinessPartnerSalesArea",
                schema: "mdm");

            migrationBuilder.DropTable(
                name: "BusinessPartnerTaxNumber",
                schema: "mdm");

            migrationBuilder.DropTable(
                name: "BusinessPartnerVendor",
                schema: "mdm");

            migrationBuilder.DropTable(
                name: "ClearingLine",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "ControllingAreaCompanyCode",
                schema: "co");

            migrationBuilder.DropTable(
                name: "ExchangeRate",
                schema: "cfg");

            migrationBuilder.DropTable(
                name: "FinancialStatementItem",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "FiscalYearPeriod",
                schema: "cfg");

            migrationBuilder.DropTable(
                name: "GLAccountCompanyCode",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "InternalOrderSettlementRule",
                schema: "co");

            migrationBuilder.DropTable(
                name: "Ledger",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "LoginHistory",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "NumberRangeGap",
                schema: "cfg");

            migrationBuilder.DropTable(
                name: "PasswordHistory",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "Plant",
                schema: "org");

            migrationBuilder.DropTable(
                name: "PostingIdempotency",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "PostingKey",
                schema: "cfg");

            migrationBuilder.DropTable(
                name: "PostingPeriod",
                schema: "cfg");

            migrationBuilder.DropTable(
                name: "RoleAuthorizationValue",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "RolePermission",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "RoleTransactionCode",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "SegregationOfDutiesRule",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "TableBrowserLog",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "TaxCode",
                schema: "cfg");

            migrationBuilder.DropTable(
                name: "Tenant",
                schema: "org");

            migrationBuilder.DropTable(
                name: "TransactionCode",
                schema: "cfg");

            migrationBuilder.DropTable(
                name: "UserCompanyCode",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "UserRole",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "UserSession",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "UserSubstitution",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "AllocationCycle",
                schema: "co");

            migrationBuilder.DropTable(
                name: "StatisticalKeyFigure",
                schema: "co");

            migrationBuilder.DropTable(
                name: "AuditLog",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "BusinessPartnerAddress",
                schema: "mdm");

            migrationBuilder.DropTable(
                name: "PurchasingOrganization",
                schema: "org");

            migrationBuilder.DropTable(
                name: "BusinessPartnerRole",
                schema: "mdm");

            migrationBuilder.DropTable(
                name: "SalesOrganization",
                schema: "org");

            migrationBuilder.DropTable(
                name: "BusinessPartnerCompanyCode",
                schema: "mdm");

            migrationBuilder.DropTable(
                name: "ClearingHeader",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "OpenItem",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "FinancialStatementVersion",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "InternalOrder",
                schema: "co");

            migrationBuilder.DropTable(
                name: "NumberRange",
                schema: "cfg");

            migrationBuilder.DropTable(
                name: "AuthorizationField",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "RoleAuthorization",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "Permission",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "User",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "BusinessPartner",
                schema: "mdm");

            migrationBuilder.DropTable(
                name: "GLAccount",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "JournalEntryLine",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "CostCenter",
                schema: "co");

            migrationBuilder.DropTable(
                name: "InternalOrderType",
                schema: "co");

            migrationBuilder.DropTable(
                name: "AuthorizationObject",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "Role",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "BusinessPartnerGroup",
                schema: "mdm");

            migrationBuilder.DropTable(
                name: "GLAccountGroup",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "JournalEntryHeader",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "FunctionalArea",
                schema: "org");

            migrationBuilder.DropTable(
                name: "ProfitCenter",
                schema: "co");

            migrationBuilder.DropTable(
                name: "CompanyCode",
                schema: "org");

            migrationBuilder.DropTable(
                name: "DocumentType",
                schema: "cfg");

            migrationBuilder.DropTable(
                name: "ExchangeRateType",
                schema: "cfg");

            migrationBuilder.DropTable(
                name: "ControllingArea",
                schema: "co");

            migrationBuilder.DropTable(
                name: "Segment",
                schema: "org");

            migrationBuilder.DropTable(
                name: "ChartOfAccounts",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "Company",
                schema: "org");

            migrationBuilder.DropTable(
                name: "CreditControlArea",
                schema: "org");

            migrationBuilder.DropTable(
                name: "PostingPeriodVariant",
                schema: "cfg");

            migrationBuilder.DropTable(
                name: "FiscalYearVariant",
                schema: "cfg");

            migrationBuilder.DropTable(
                name: "Currency",
                schema: "cfg");
        }
    }
}
