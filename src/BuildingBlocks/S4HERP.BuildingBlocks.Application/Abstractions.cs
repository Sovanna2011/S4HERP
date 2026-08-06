namespace S4HERP.BuildingBlocks.Application;

/// <summary>A request that changes state. Runs inside a transaction.</summary>
public interface ICommand<TResult>;

/// <summary>A request that reads state. Never opens a write transaction.</summary>
public interface IQuery<TResult>;

public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// Cross-cutting step wrapped around a handler. Order is fixed by registration
/// and matters — see <c>PipelineBehaviors.cs</c>.
/// </summary>
public interface IPipelineBehavior<in TRequest, TResult>
{
    Task<TResult> HandleAsync(
        TRequest request, Func<Task<TResult>> next, CancellationToken cancellationToken);
}

public interface IDispatcher
{
    Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);
    Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Declares which authorisation object a request needs. Checked by
/// <c>AuthorizationBehavior</c> before validation, so a caller cannot probe
/// field-level rules on data they may not see.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RequiresAuthorizationAttribute(string authorizationObject, string activity)
    : Attribute
{
    public string AuthorizationObject { get; } = authorizationObject;
    public string Activity { get; } = activity;
}

/// <summary>
/// Marks a request as validated inside its handler rather than by a separate
/// validator. Used where validation and derivation are the same pass — the
/// posting engine cannot check a period without first deriving it.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SelfValidatingAttribute : Attribute;
