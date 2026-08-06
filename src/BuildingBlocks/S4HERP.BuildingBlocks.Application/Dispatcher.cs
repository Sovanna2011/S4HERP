using Microsoft.Extensions.DependencyInjection;

namespace S4HERP.BuildingBlocks.Application;

/// <summary>
/// Resolves a handler for a request and runs it through the registered pipeline.
///
/// ADR-05: this exists instead of MediatR, whose licence (RPL-1.5, or paid) would
/// oblige releasing S4HERP's source. It is deliberately small — the value of a
/// mediator here is the behaviour pipeline, not the library.
/// </summary>
public sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    public Task<TResult> SendAsync<TResult>(
        ICommand<TResult> command, CancellationToken cancellationToken = default) =>
        InvokeAsync<TResult>(command, typeof(ICommandHandler<,>), cancellationToken);

    public Task<TResult> QueryAsync<TResult>(
        IQuery<TResult> query, CancellationToken cancellationToken = default) =>
        InvokeAsync<TResult>(query, typeof(IQueryHandler<,>), cancellationToken);

    private Task<TResult> InvokeAsync<TResult>(
        object request, Type handlerDefinition, CancellationToken cancellationToken)
    {
        var requestType = request.GetType();
        var handlerType = handlerDefinition.MakeGenericType(requestType, typeof(TResult));
        var handler = services.GetService(handlerType)
            ?? throw new InvalidOperationException(
                $"No handler registered for {requestType.Name}.");

        var invoke = handlerType.GetMethod("HandleAsync")!;

        Func<Task<TResult>> next = () =>
            (Task<TResult>)invoke.Invoke(handler, [request, cancellationToken])!;

        // Behaviours run outermost-first, so reverse the registration order when
        // wrapping: the first registered behaviour ends up on the outside.
        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResult));
        var behaviors = ((IEnumerable<object>)services.GetServices(behaviorType)).Reverse().ToList();

        foreach (var behavior in behaviors)
        {
            var inner = next;
            var behaviorInvoke = behaviorType.GetMethod("HandleAsync")!;
            next = () => (Task<TResult>)behaviorInvoke.Invoke(
                behavior, [request, inner, cancellationToken])!;
        }

        return next();
    }
}

public static class DispatcherRegistration
{
    public static IServiceCollection AddDispatcher(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();
        return services;
    }

    /// <summary>
    /// Registers a behaviour for every request type. Order of these calls is the
    /// order the behaviours wrap the handler.
    /// </summary>
    public static IServiceCollection AddPipelineBehavior(
        this IServiceCollection services, Type openBehaviorType)
    {
        services.AddScoped(typeof(IPipelineBehavior<,>), openBehaviorType);
        return services;
    }
}
