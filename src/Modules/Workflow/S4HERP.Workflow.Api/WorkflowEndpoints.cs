using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using S4HERP.BuildingBlocks.Application;
using S4HERP.Workflow.Application;

namespace S4HERP.Workflow.Api;

public static class WorkflowEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder routes)
    {
        // Not under /finance. The inbox spans modules — journal entries, payment
        // runs and bank changes — and routing it through Finance would say the
        // opposite of what the architecture does.
        routes.MapGet("/api/v1/approvals", async (
                string? objectType, IDispatcher dispatcher, CancellationToken ct) =>
                Results.Ok(await dispatcher.QueryAsync(
                    new ApprovalInboxQuery { ObjectType = objectType }, ct)))
            .WithTags("Approvals")
            .WithName("GetMyApprovals")
            .WithSummary("Everything waiting on the calling user, of every kind.");

        return routes;
    }
}
