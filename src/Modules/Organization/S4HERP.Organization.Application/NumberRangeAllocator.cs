using Microsoft.EntityFrameworkCore;
using S4HERP.BuildingBlocks.Application;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Organization.Domain;

// Lives in Organization, not Finance. cfg.NumberRange is Organization's
// configuration table, and the allocator is a service over it — it was only in
// Finance because accounting documents were the first thing to need a number.
// Business partner bank change requests are the second, and a module may not
// depend on Finance to get one.
namespace S4HERP.Organization.Application;

/// <summary>
/// Error codes the allocator raises. Same string values Finance has always
/// returned — they are part of the API contract, and moving a class between
/// assemblies is not a reason to change what a client branches on.
/// </summary>
public static class NumberRangeErrors
{
    public const string UnknownObject = "UNKNOWN_OBJECT";
    public const string NumberRangeExhausted = "NUMBER_RANGE_EXHAUSTED";
}

public interface INumberRangeAllocator
{
    Task<long> AllocateAsync(
        NumberRangeObject objectType, string rangeCode, long? companyCodeId,
        short fiscalYear, CancellationToken cancellationToken);
}

/// <summary>
/// ADR-11. Gapless allocation inside the posting transaction, using a row lock
/// held for the shortest possible span.
///
/// The lock serialises concurrent postings to the same (company code, year,
/// document type). That is the cost of gaplessness, and it is why the allocation
/// happens last — every validation, derivation and translation is already done
/// before the lock is taken, and nothing that can fail runs between allocation
/// and commit.
///
/// A range configured with IsGapless = false skips the lock entirely and may
/// leave gaps on rollback.
/// </summary>
public sealed class NumberRangeAllocator(S4herpDbContext db) : INumberRangeAllocator
{
    public async Task<long> AllocateAsync(
        NumberRangeObject objectType, string rangeCode, long? companyCodeId,
        short fiscalYear, CancellationToken cancellationToken)
    {
        // UPDLOCK + ROWLOCK takes the update lock at read time, so two concurrent
        // allocations cannot both read the same CurrentNumber. OUTPUT returns the
        // new value in the same round trip.
        var rows = await db.Database
            .SqlQuery<long>($"""
                UPDATE cfg.NumberRange WITH (UPDLOCK, ROWLOCK)
                SET CurrentNumber = CurrentNumber + 1
                OUTPUT inserted.CurrentNumber AS Value
                WHERE ObjectType = {(int)objectType}
                  AND Code = {rangeCode}
                  AND FiscalYear = {fiscalYear}
                  AND (CompanyCodeId = {companyCodeId} OR ({companyCodeId} IS NULL AND CompanyCodeId IS NULL))
                  AND CurrentNumber < ToNumber
                """)
            .ToListAsync(cancellationToken);

        if (rows.Count == 1)
        {
            return rows[0];
        }

        // Zero rows means either no such range, or it is exhausted. Distinguish
        // them, because the fixes are entirely different.
        var range = await db.Set<NumberRange>()
            .Where(r => r.ObjectType == objectType
                        && r.Code == rangeCode
                        && r.FiscalYear == fiscalYear
                        && r.CompanyCodeId == companyCodeId)
            .Select(r => new { r.CurrentNumber, r.ToNumber })
            .SingleOrDefaultAsync(cancellationToken);

        if (range is null)
        {
            throw new BusinessRuleException(
                NumberRangeErrors.UnknownObject,
                $"No number range {rangeCode} is configured for object {objectType} " +
                $"in fiscal year {fiscalYear}.");
        }

        throw new BusinessRuleException(
            NumberRangeErrors.NumberRangeExhausted,
            $"Number range {rangeCode} for fiscal year {fiscalYear} is exhausted " +
            $"at {range.CurrentNumber} of {range.ToNumber}.");
    }
}
