using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using S4HERP.BuildingBlocks.Application;

namespace S4HERP.Host.Infrastructure;

/// <summary>
/// Maps every failure to an RFC 7807 response with a stable application error
/// code, a correlation id and field-level details (§22.1).
///
/// Nothing internal escapes: no stack trace, no SQL, no connection string. The
/// full detail goes to the log under the same correlation id, so support can
/// join the two.
/// </summary>
/// <remarks>
/// Registered as a singleton, so it must not capture the scoped
/// <see cref="RequestContext"/>. The correlation id is read from the response
/// header that <see cref="RequestContextMiddleware"/> has already set.
/// </remarks>
public sealed class S4herpExceptionHandler(ILogger<S4herpExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext http, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, title, detail, violations) = exception switch
        {
            S4herpException known =>
                (known.StatusCode, known.ErrorCode, TitleFor(known.StatusCode),
                 known.Message, known.Violations),
            OperationCanceledException =>
                (499, "REQUEST_CANCELLED", "Request cancelled",
                 "The request was cancelled.", (IReadOnlyList<RuleViolation>)[]),
            _ => (500, "INTERNAL_ERROR", "Internal server error",
                  "An unexpected error occurred. Quote the trace id when reporting it.",
                  (IReadOnlyList<RuleViolation>)[]),
        };

        if (status >= 500)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}.",
                http.Request.Method, http.Request.Path);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = $"https://s4herp.invalid/errors/{code.ToLowerInvariant()}",
            Instance = http.Request.Path,
        };

        problem.Extensions["errorCode"] = code;
        problem.Extensions["correlationId"] =
            http.Response.Headers["X-Correlation-Id"].FirstOrDefault() ?? http.TraceIdentifier;
        problem.Extensions["traceId"] = http.TraceIdentifier;

        if (violations.Count > 0)
        {
            // Grouped by field so the UI can place each message beside its input.
            problem.Extensions["errors"] = violations
                .GroupBy(v => v.Field)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(v => new { code = v.Code, message = v.Message }).ToArray());
        }

        http.Response.StatusCode = status;
        await http.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static string TitleFor(int status) => status switch
    {
        400 => "Validation failed",
        403 => "Not authorised",
        404 => "Not found",
        409 => "Conflict",
        422 => "Business rule violated",
        _ => "Request failed",
    };
}
