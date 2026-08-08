using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using S4HERP.BuildingBlocks.Application;
using S4HERP.BusinessPartner.Application;

namespace S4HERP.BusinessPartner.Api;

public static class BusinessPartnerEndpoints
{
    public static IEndpointRouteBuilder MapBusinessPartnerEndpoints(this IEndpointRouteBuilder routes)
    {
        var partners = routes
            .MapGroup("/api/v1/business-partners")
            .WithTags("Business partner bank details");

        partners.MapGet("/{partnerNumber}/bank-details", async (
                string partnerNumber, IDispatcher dispatcher, CancellationToken ct) =>
                Results.Ok(await dispatcher.QueryAsync(
                    new GetPartnerBanksQuery { PartnerNumber = partnerNumber }, ct)))
            .WithName("GetPartnerBankDetails")
            .WithSummary("A partner's bank details. Every one of them is approved.");

        // The change is raised here and applied nowhere near here: this route
        // stages values and opens an approval, and returns 202 rather than 201
        // because nothing about the partner has changed yet.
        partners.MapPost("/{partnerNumber}/bank-details/changes", async (
                string partnerNumber,
                RequestBankChangeCommand command,
                IDispatcher dispatcher,
                CancellationToken ct) =>
            {
                var result = await dispatcher.SendAsync(
                    command with { PartnerNumber = partnerNumber }, ct);

                return Results.Accepted(
                    $"/api/v1/business-partners/bank-details/changes/{result.RequestId}", result);
            })
            .WithName("RequestBankChange")
            .WithSummary("Raise a bank detail change for approval (FK02). Applies nothing.");

        var changes = routes
            .MapGroup("/api/v1/business-partners/bank-details/changes")
            .WithTags("Business partner bank details");

        changes.MapGet("/{requestId}", async (
                string requestId, IDispatcher dispatcher, CancellationToken ct) =>
                Results.Ok(await dispatcher.QueryAsync(
                    new GetBankChangeQuery { RequestId = requestId }, ct)))
            .WithName("GetBankChange")
            .WithSummary("A change request: what it proposes, what it replaces, who must sign it.");

        changes.MapPost("/{requestId}/approve", async (
                string requestId,
                DecisionBody? body,
                IDispatcher dispatcher,
                CancellationToken ct) =>
                Results.Ok(await dispatcher.SendAsync(
                    new DecideBankChangeCommand
                    {
                        RequestId = requestId,
                        Approve = true,
                        Comment = body?.Comment,
                    }, ct)))
            .WithName("ApproveBankChange")
            .WithSummary("Approve a bank detail change. The last approval applies it.");

        changes.MapPost("/{requestId}/reject", async (
                string requestId,
                DecisionBody body,
                IDispatcher dispatcher,
                CancellationToken ct) =>
                Results.Ok(await dispatcher.SendAsync(
                    new DecideBankChangeCommand
                    {
                        RequestId = requestId,
                        Approve = false,
                        Comment = body.Comment,
                    }, ct)))
            .WithName("RejectBankChange")
            .WithSummary("Reject a bank detail change. The reason is mandatory.");

        changes.MapPost("/{requestId}/withdraw", async (
                string requestId,
                DecisionBody? body,
                IDispatcher dispatcher,
                CancellationToken ct) =>
                Results.Ok(await dispatcher.SendAsync(
                    new WithdrawBankChangeCommand
                    {
                        RequestId = requestId,
                        Comment = body?.Comment,
                    }, ct)))
            .WithName("WithdrawBankChange")
            .WithSummary("Pull a change request back before anyone has decided it.");

        return routes;
    }
}
