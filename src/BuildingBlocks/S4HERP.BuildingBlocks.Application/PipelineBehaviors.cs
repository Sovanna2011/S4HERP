using System.Reflection;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using S4HERP.BuildingBlocks.Infrastructure;

namespace S4HERP.BuildingBlocks.Application;

/// <summary>
/// Checks the coarse authorisation object a request declares, before anything
/// else runs. Fine-grained checks — this company code, this amount — happen in
/// the handler, which is the only place that knows the actual values.
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResult>(IAuthorizationEnforcer enforcer)
    : IPipelineBehavior<TRequest, TResult>
{
    public async Task<TResult> HandleAsync(
        TRequest request, Func<Task<TResult>> next, CancellationToken cancellationToken)
    {
        var attribute = typeof(TRequest).GetCustomAttribute<RequiresAuthorizationAttribute>();
        if (attribute is not null)
        {
            await enforcer.RequireAsync(
                attribute.AuthorizationObject,
                [("ACTVT", attribute.Activity)],
                cancellationToken);
        }

        return await next();
    }
}

/// <summary>
/// Runs FluentValidation validators. Placed after authorisation so an
/// unauthorised caller learns nothing from validation messages.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResult>(
    IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResult>
{
    public async Task<TResult> HandleAsync(
        TRequest request, Func<Task<TResult>> next, CancellationToken cancellationToken)
    {
        var applicable = validators.ToList();
        if (applicable.Count == 0)
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var failures = (await Task.WhenAll(
                applicable.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .Select(f => new RuleViolation(
                Camel(f.PropertyName), f.ErrorCode ?? "INVALID", f.ErrorMessage))
            .ToList();

        if (failures.Count > 0)
        {
            throw new RequestValidationException(failures);
        }

        return await next();
    }

    private static string Camel(string path) => string.Join('.', path.Split('.')
        .Select(p => p.Length > 0 ? char.ToLowerInvariant(p[0]) + p[1..] : p));
}

/// <summary>
/// Opens one database transaction per command and commits it only if the handler
/// and every inner behaviour succeed (§22.1). Queries pass straight through —
/// a read does not need a write transaction.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResult>(
    S4herpDbContext db, ILogger<TransactionBehavior<TRequest, TResult>> logger)
    : IPipelineBehavior<TRequest, TResult>
{
    public async Task<TResult> HandleAsync(
        TRequest request, Func<Task<TResult>> next, CancellationToken cancellationToken)
    {
        var isCommand = typeof(TRequest).GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));

        if (!isCommand || db.Database.CurrentTransaction is not null)
        {
            return await next();
        }

        // EnableRetryOnFailure means an explicit transaction must be wrapped in
        // the execution strategy, or a retry would resume mid-transaction.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using IDbContextTransaction transaction =
                await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await next();
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                logger.LogWarning(ex, "Concurrency conflict handling {Request}.", typeof(TRequest).Name);
                throw new ConcurrencyConflictException(
                    "The record was changed by someone else. Reload and try again.");
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}

/// <summary>Attaches the correlation id to the log scope for the whole request.</summary>
public sealed class LoggingBehavior<TRequest, TResult>(
    ILogger<LoggingBehavior<TRequest, TResult>> logger, ICorrelationContext correlation)
    : IPipelineBehavior<TRequest, TResult>
{
    public async Task<TResult> HandleAsync(
        TRequest request, Func<Task<TResult>> next, CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlation.CorrelationId,
            ["Request"] = typeof(TRequest).Name,
        });

        try
        {
            return await next();
        }
        catch (S4herpException ex)
        {
            // Expected refusals are information, not incidents.
            logger.LogInformation("{Request} refused: {Code} {Message}",
                typeof(TRequest).Name, ex.ErrorCode, ex.Message);
            throw;
        }
    }
}

public interface ICorrelationContext
{
    Guid CorrelationId { get; }
}

/// <summary>
/// Server-side authorisation. Every command, query, report and export goes
/// through it; hiding a button in the UI is not authorisation (§22.1).
/// </summary>
public interface IAuthorizationEnforcer
{
    /// <summary>Throws <see cref="AuthorizationException"/> when the values are not covered.</summary>
    Task RequireAsync(
        string authorizationObject,
        IReadOnlyList<(string Field, string Value)> values,
        CancellationToken cancellationToken = default);

    Task<bool> IsAuthorizedAsync(
        string authorizationObject,
        IReadOnlyList<(string Field, string Value)> values,
        CancellationToken cancellationToken = default);

    /// <summary>Company codes the caller may act in, for query predicates.</summary>
    Task<IReadOnlyCollection<long>> AuthorizedCompanyCodeIdsAsync(
        CancellationToken cancellationToken = default);
}
