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
