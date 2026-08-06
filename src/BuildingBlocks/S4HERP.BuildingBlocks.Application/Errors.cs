namespace S4HERP.BuildingBlocks.Application;

/// <summary>
/// One field-level failure. Carried through to the RFC 7807 response so the UI
/// can put the message beside the field that caused it (§22.1).
/// </summary>
/// <param name="Field">Dotted path, e.g. <c>lines[2].glAccount</c>.</param>
/// <param name="Code">Stable machine-readable code, e.g. <c>PERIOD_CLOSED</c>.</param>
/// <param name="Message">Human-readable explanation naming the actual values.</param>
public readonly record struct RuleViolation(string Field, string Code, string Message);

/// <summary>Base for every error that maps to a specific HTTP status.</summary>
public abstract class S4herpException(string message) : Exception(message)
{
    public abstract string ErrorCode { get; }
    public abstract int StatusCode { get; }
    public IReadOnlyList<RuleViolation> Violations { get; protected init; } = [];
}

/// <summary>Input failed validation. 400.</summary>
public sealed class RequestValidationException : S4herpException
{
    public RequestValidationException(IReadOnlyList<RuleViolation> violations)
        : base("The request failed validation.") => Violations = violations;

    public override string ErrorCode => "VALIDATION_FAILED";
    public override int StatusCode => 400;
}

/// <summary>A business rule refused the operation. 422 — the request was well formed.</summary>
public sealed class BusinessRuleException : S4herpException
{
    public BusinessRuleException(string code, string message)
        : base(message) => ErrorCode = code;

    public BusinessRuleException(string code, string message, IReadOnlyList<RuleViolation> violations)
        : base(message)
    {
        ErrorCode = code;
        Violations = violations;
    }

    public override string ErrorCode { get; }
    public override int StatusCode => 422;
}

/// <summary>The caller lacks the required authorisation. 403.</summary>
public sealed class AuthorizationException(string message) : S4herpException(message)
{
    public override string ErrorCode => "NOT_AUTHORIZED";
    public override int StatusCode => 403;
}

/// <summary>The object does not exist, or the caller may not know that it does. 404.</summary>
public sealed class NotFoundException(string message) : S4herpException(message)
{
    public override string ErrorCode => "NOT_FOUND";
    public override int StatusCode => 404;
}

/// <summary>Another change won the race. 409, never a silent overwrite (§22.1).</summary>
public sealed class ConcurrencyConflictException(string message) : S4herpException(message)
{
    public override string ErrorCode => "CONCURRENCY_CONFLICT";
    public override int StatusCode => 409;
}
