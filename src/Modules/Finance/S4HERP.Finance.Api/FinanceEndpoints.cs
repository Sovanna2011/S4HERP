using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using S4HERP.BuildingBlocks.Application;
using S4HERP.Finance.Application;

namespace S4HERP.Finance.Api;

public static class FinanceEndpoints
{
    public static IEndpointRouteBuilder MapFinanceEndpoints(this IEndpointRouteBuilder routes)
    {
        var journal = routes.MapGroup("/api/v1/finance/journal-entries").WithTags("Journal");

        journal.MapPost("/", async (
                PostJournalEntryCommand command,
                IDispatcher dispatcher,
                HttpContext http,
                CancellationToken ct) =>
            {
                // The header wins over the body so a proxy or client library can
                // supply the key without rewriting the payload.
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault()
                          ?? command.IdempotencyKey;

                var result = await dispatcher.SendAsync(
                    command with { IdempotencyKey = key, Simulate = false }, ct);

                return Results.Created(
                    $"/api/v1/finance/journal-entries/{result.CompanyCode}/{result.FiscalYear}/{result.DocumentNumber}",
                    result);
            })
            .WithName("PostJournalEntry")
            .WithSummary("Post an accounting document (FB50).");

        journal.MapPost("/simulate", async (
                PostJournalEntryCommand command, IDispatcher dispatcher, CancellationToken ct) =>
            {
                // Simulation runs the identical pipeline up to the balance check;
                // it allocates no number and writes nothing.
                var result = await dispatcher.SendAsync(command with { Simulate = true }, ct);
                return Results.Ok(result);
            })
            .WithName("SimulateJournalEntry")
            .WithSummary("Show the full accounting impact without posting.");

        journal.MapPost("/park", async (
                PostJournalEntryCommand command,
                IDispatcher dispatcher,
                HttpContext http,
                CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault()
                          ?? command.IdempotencyKey;

                var result = await dispatcher.SendAsync(
                    command with { IdempotencyKey = key, Simulate = false, Park = true }, ct);

                return Results.Created(
                    $"/api/v1/finance/journal-entries/{result.CompanyCode}/{result.FiscalYear}/{result.DocumentNumber}",
                    result);
            })
            .WithName("ParkJournalEntry")
            .WithSummary("Park a document without posting it (FV50).");

        journal.MapPost("/{companyCode}/{fiscalYear:int}/{documentNumber:long}/submit", async (
                string companyCode, int fiscalYear, long documentNumber,
                IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.SendAsync(new SubmitJournalEntryCommand
                {
                    CompanyCode = companyCode,
                    FiscalYear = (short)fiscalYear,
                    DocumentNumber = documentNumber,
                }, ct);

                return Results.Ok(result);
            })
            .WithName("SubmitJournalEntry")
            .WithSummary("Submit a parked document for approval.");

        journal.MapPost("/{companyCode}/{fiscalYear:int}/{documentNumber:long}/approve", async (
                string companyCode, int fiscalYear, long documentNumber,
                ApprovalDecisionBody? body, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.SendAsync(new ApproveJournalEntryCommand
                {
                    CompanyCode = companyCode,
                    FiscalYear = (short)fiscalYear,
                    DocumentNumber = documentNumber,
                    Comment = body?.Comment,
                }, ct);

                return Results.Ok(result);
            })
            .WithName("ApproveJournalEntry")
            .WithSummary("Approve the caller's step; posts the document on the last one.");

        journal.MapPost("/{companyCode}/{fiscalYear:int}/{documentNumber:long}/reject", async (
                string companyCode, int fiscalYear, long documentNumber,
                ApprovalDecisionBody body, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.SendAsync(new RejectJournalEntryCommand
                {
                    CompanyCode = companyCode,
                    FiscalYear = (short)fiscalYear,
                    DocumentNumber = documentNumber,
                    // Passed through empty rather than rejected here, so the reason
                    // the caller sees is the workflow's own REJECTION_COMMENT_REQUIRED
                    // wherever the request came from.
                    Comment = body?.Comment ?? string.Empty,
                }, ct);

                return Results.Ok(result);
            })
            .WithName("RejectJournalEntry")
            .WithSummary("Reject the document, with a mandatory reason.");

        journal.MapPost("/{companyCode}/{fiscalYear:int}/{documentNumber:long}/withdraw", async (
                string companyCode, int fiscalYear, long documentNumber,
                ApprovalDecisionBody? body, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.SendAsync(new WithdrawJournalEntryCommand
                {
                    CompanyCode = companyCode,
                    FiscalYear = (short)fiscalYear,
                    DocumentNumber = documentNumber,
                    Comment = body?.Comment,
                }, ct);

                return Results.Ok(result);
            })
            .WithName("WithdrawJournalEntry")
            .WithSummary("Pull a submitted document back out of approval.");

        journal.MapDelete("/{companyCode}/{fiscalYear:int}/{documentNumber:long}", async (
                string companyCode, int fiscalYear, long documentNumber,
                IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.SendAsync(new DeleteJournalEntryCommand
                {
                    CompanyCode = companyCode,
                    FiscalYear = (short)fiscalYear,
                    DocumentNumber = documentNumber,
                }, ct);

                return Results.Ok(result);
            })
            .WithName("DeleteJournalEntry")
            .WithSummary("Discard a document that never reached the ledger.");

        journal.MapGet("/{companyCode}/{fiscalYear:int}/{documentNumber:long}/workflow", async (
                string companyCode, int fiscalYear, long documentNumber,
                IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.QueryAsync(new GetJournalWorkflowQuery
                {
                    CompanyCode = companyCode,
                    FiscalYear = (short)fiscalYear,
                    DocumentNumber = documentNumber,
                }, ct);

                return Results.Ok(result);
            })
            .WithName("GetJournalWorkflow")
            .WithSummary("Approval state and step history for a document.");

        journal.MapGet("/{companyCode}/{fiscalYear:int}/{documentNumber:long}", async (
                string companyCode, int fiscalYear, long documentNumber,
                IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.QueryAsync(new GetJournalEntryQuery
                {
                    CompanyCode = companyCode,
                    FiscalYear = (short)fiscalYear,
                    DocumentNumber = documentNumber,
                }, ct);

                return Results.Ok(result);
            })
            .WithName("GetJournalEntry")
            .WithSummary("Display an accounting document (FB03).");

        journal.MapPost("/{companyCode}/{fiscalYear:int}/{documentNumber:long}/reverse", async (
                string companyCode, int fiscalYear, long documentNumber,
                ReverseJournalEntryRequest request, IDispatcher dispatcher,
                HttpContext http, CancellationToken ct) =>
            {
                var result = await dispatcher.SendAsync(new ReverseJournalEntryCommand
                {
                    CompanyCode = companyCode,
                    FiscalYear = (short)fiscalYear,
                    DocumentNumber = documentNumber,
                    ReversalReasonCode = request.ReversalReasonCode,
                    PostingDate = request.PostingDate,
                    IdempotencyKey = http.Request.Headers["Idempotency-Key"].FirstOrDefault(),
                }, ct);

                return Results.Ok(result);
            })
            .WithName("ReverseJournalEntry")
            .WithSummary("Reverse an accounting document (FB08).");

        routes.MapGet("/api/v1/finance/approvals", async (
                IDispatcher dispatcher, CancellationToken ct) =>
                Results.Ok(await dispatcher.QueryAsync(new MyJournalApprovalsQuery(), ct)))
            .WithTags("Journal")
            .WithName("GetMyJournalApprovals")
            .WithSummary("Documents waiting on the calling user.");

        var payments = routes.MapGroup("/api/v1/finance/payments").WithTags("Payments");

        payments.MapPost("/", async (
                PostPaymentCommand command, IDispatcher dispatcher,
                HttpContext http, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault()
                          ?? command.IdempotencyKey;

                var result = await dispatcher.SendAsync(command with { IdempotencyKey = key }, ct);

                return Results.Created(
                    $"/api/v1/finance/journal-entries/{result.CompanyCode}/{result.FiscalYear}/{result.DocumentNumber}",
                    result);
            })
            .WithName("PostPayment")
            .WithSummary("Post a payment clearing open items (F-28 / F-53).");

        payments.MapPost("/{companyCode}/{fiscalYear:int}/{documentNumber:long}/reset-clearing", async (
                string companyCode, int fiscalYear, long documentNumber,
                IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.SendAsync(new ResetClearingCommand
                {
                    CompanyCode = companyCode,
                    FiscalYear = (short)fiscalYear,
                    ClearingDocumentNumber = documentNumber,
                }, ct);

                return Results.Ok(result);
            })
            .WithName("ResetClearing")
            .WithSummary("Reopen the items a payment cleared (FBRA).");

        routes.MapGet("/api/v1/finance/open-items", async (
                string companyCode, string? accountType, string? businessPartner,
                DateOnly? asOf, bool? includeCleared,
                IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.QueryAsync(new OpenItemsQuery
                {
                    CompanyCode = companyCode,
                    AccountType = accountType,
                    BusinessPartner = businessPartner,
                    AsOf = asOf,
                    IncludeCleared = includeCleared ?? false,
                }, ct);

                return Results.Ok(result);
            })
            .WithTags("Reports")
            .WithName("GetOpenItems")
            .WithSummary("Open items with aging (FBL5N / FBL1N).");

        var reports = routes.MapGroup("/api/v1/finance/reports").WithTags("Reports");

        reports.MapGet("/trial-balance", async (
                string companyCode, int fiscalYear, int? fromPeriod, int? toPeriod,
                IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.QueryAsync(new TrialBalanceQuery
                {
                    CompanyCode = companyCode,
                    FiscalYear = (short)fiscalYear,
                    FromPeriod = (byte?)fromPeriod,
                    ToPeriod = (byte?)toPeriod,
                }, ct);

                return Results.Ok(result);
            })
            .WithName("GetTrialBalance")
            .WithSummary("Trial balance, derived from the universal journal.");

        return routes;
    }
}

public sealed record ReverseJournalEntryRequest(string ReversalReasonCode, DateOnly? PostingDate);

public sealed record ApprovalDecisionBody(string? Comment);
