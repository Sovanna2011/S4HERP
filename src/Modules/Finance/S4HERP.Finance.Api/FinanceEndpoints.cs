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

        // What the bank said back. A separate group from payment-runs because the
        // return leg is not part of making a payment — it is what happens to one
        // afterwards, and it can arrive days later.
        var status = routes
            .MapGroup("/api/v1/finance/payment-status-reports")
            .WithTags("Bank status");

        status.MapPost("/", async (
                HttpRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            {
                // Read as text rather than bound as JSON: the payload is the
                // bank's XML, byte for byte, and it is stored as evidence.
                using var reader = new StreamReader(request.Body);
                var content = await reader.ReadToEndAsync(ct);

                var result = await dispatcher.SendAsync(
                    new ImportPaymentStatusCommand { Content = content }, ct);

                // 200 on a re-import, 201 on a new one: importing the same file
                // twice is a normal thing to do and not a creation.
                return result.AlreadyImported
                    ? Results.Ok(result)
                    : Results.Created(
                        $"/api/v1/finance/payment-status-reports/{result.MessageId}", result);
            })
            .Accepts<string>("application/xml", "text/xml")
            .WithName("ImportPaymentStatusReport")
            .WithSummary("Import an ISO 20022 pain.002 payment status report.");

        status.MapGet("/{messageId}", async (
                string messageId, IDispatcher dispatcher, CancellationToken ct) =>
                Results.Ok(await dispatcher.QueryAsync(
                    new GetPaymentStatusReportQuery { MessageId = messageId }, ct)))
            .WithName("GetPaymentStatusReport")
            .WithSummary("An imported status report and the bank's verdict per payment.");

        status.MapGet("/rejections", async (
                string? companyCode, bool? includeResolved,
                IDispatcher dispatcher, CancellationToken ct) =>
                Results.Ok(await dispatcher.QueryAsync(
                    new GetOutstandingRejectionsQuery
                    {
                        CompanyCode = companyCode,
                        IncludeResolved = includeResolved ?? false,
                    }, ct)))
            .WithName("GetOutstandingRejections")
            .WithSummary("Payments the bank refused that the ledger still shows as paid.");

        status.MapPost("/rejections/{endToEndId}/resolve", async (
                string endToEndId,
                ResolveRejectionBody? body,
                IDispatcher dispatcher,
                CancellationToken ct) =>
                Results.Ok(await dispatcher.SendAsync(
                    new ResolveRejectedPaymentCommand
                    {
                        EndToEndId = endToEndId,
                        Comment = body?.Comment,
                    }, ct)))
            .WithName("ResolveRejectedPayment")
            .WithSummary("Reverse a refused payment and reopen the invoices it cleared.");

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

        var runs = routes.MapGroup("/api/v1/finance/payment-runs").WithTags("Payments");

        runs.MapGet("/", async (
                string? companyCode, string? status, int? take,
                IDispatcher dispatcher, CancellationToken ct) =>
                Results.Ok(await dispatcher.QueryAsync(
                    new ListPaymentRunsQuery
                    {
                        CompanyCode = companyCode,
                        Status = status,
                        Take = take ?? 50,
                    }, ct)))
            .WithName("ListPaymentRuns")
            .WithSummary("Payment runs the caller may see, newest first.");

        runs.MapPost("/", async (
                CreatePaymentProposalCommand command, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.SendAsync(command, ct);
                return Results.Created($"/api/v1/finance/payment-runs/{result.RunId}", result);
            })
            .WithName("CreatePaymentProposal")
            .WithSummary("Propose a payment run (F110). Posts nothing.");

        runs.MapGet("/{runId}", async (
                string runId, IDispatcher dispatcher, CancellationToken ct) =>
                Results.Ok(await dispatcher.QueryAsync(new GetPaymentRunQuery { RunId = runId }, ct)))
            .WithName("GetPaymentRun")
            .WithSummary("A payment run, its proposed payments and its exclusions.");

        runs.MapPost("/{runId}/submit", async (
                string runId, IDispatcher dispatcher, CancellationToken ct) =>
                Results.Ok(await dispatcher.SendAsync(
                    new SubmitPaymentRunCommand { RunId = runId }, ct)))
            .WithName("SubmitPaymentRun")
            .WithSummary("Send a proposal for approval.");

        runs.MapPost("/{runId}/approve", async (
                string runId, ApprovalDecisionBody? body,
                IDispatcher dispatcher, CancellationToken ct) =>
                Results.Ok(await dispatcher.SendAsync(
                    new ApprovePaymentRunCommand { RunId = runId, Comment = body?.Comment }, ct)))
            .WithName("ApprovePaymentRun")
            .WithSummary("Approve the caller's step; the last one releases the run.");

        runs.MapPost("/{runId}/reject", async (
                string runId, ApprovalDecisionBody? body,
                IDispatcher dispatcher, CancellationToken ct) =>
                Results.Ok(await dispatcher.SendAsync(new RejectPaymentRunCommand
                {
                    RunId = runId,
                    Comment = body?.Comment ?? string.Empty,
                }, ct)))
            .WithName("RejectPaymentRun")
            .WithSummary("Reject the run, with a mandatory reason.");

        runs.MapPost("/{runId}/execute", async (
                string runId, IDispatcher dispatcher, CancellationToken ct) =>
                Results.Ok(await dispatcher.SendAsync(
                    new ExecutePaymentRunCommand { RunId = runId }, ct)))
            .WithName("ExecutePaymentRun")
            .WithSummary("Post the proposal: one payment document per partner.");

        runs.MapDelete("/{runId}", async (
                string runId, IDispatcher dispatcher, CancellationToken ct) =>
                Results.Ok(await dispatcher.SendAsync(
                    new DeletePaymentProposalCommand { RunId = runId }, ct)))
            .WithName("DeletePaymentProposal")
            .WithSummary("Discard a proposal that was never executed.");

        runs.MapPost("/{runId}/payment-file", async (
                string runId, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.SendAsync(
                    new GeneratePaymentFileCommand { RunId = runId }, ct);

                return Results.Created(
                    $"/api/v1/finance/payment-runs/{runId}/payment-file", result);
            })
            .WithName("GeneratePaymentFile")
            .WithSummary("Generate the ISO 20022 pain.001 instruction for an executed run.");

        runs.MapGet("/{runId}/bank-status", async (
                string runId, IDispatcher dispatcher, CancellationToken ct) =>
                Results.Ok(await dispatcher.QueryAsync(
                    new GetRunBankStatusQuery { RunId = runId }, ct)))
            .WithName("GetRunBankStatus")
            .WithSummary("What the bank has said about this run's payments, latest word first.");

        runs.MapGet("/{runId}/payment-file", async (
                string runId, IDispatcher dispatcher, CancellationToken ct) =>
                Results.Ok(await dispatcher.QueryAsync(
                    new GetPaymentFileQuery { RunId = runId }, ct)))
            .WithName("GetPaymentFile")
            .WithSummary("Payment file metadata: counts, control sum, hash, who took a copy.");

        // The content is a separate route from the metadata so the permission can
        // be separate too. Anyone who may see the run may see that a file exists
        // and what it totals; taking the account numbers away needs S_EXPORT.
        runs.MapGet("/{runId}/payment-file/content", async (
                string runId, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var file = await dispatcher.SendAsync(
                    new DownloadPaymentFileCommand { RunId = runId }, ct);

                // As a download rather than a rendered body: the name carries the
                // run and the message id, which is what a treasurer has to quote
                // when the bank asks which instruction they are looking at.
                return Results.File(
                    System.Text.Encoding.UTF8.GetBytes(file.Content),
                    "application/xml",
                    file.FileName);
            })
            .WithName("DownloadPaymentFile")
            .WithSummary("The pain.001 XML itself. Requires S_EXPORT and is audited.");

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
