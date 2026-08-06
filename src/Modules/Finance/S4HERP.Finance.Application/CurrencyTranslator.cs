using Microsoft.EntityFrameworkCore;
using S4HERP.BuildingBlocks.Application;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Organization.Domain;

namespace S4HERP.Finance.Application;

public interface ICurrencyTranslator
{
    /// <summary>
    /// Rate to convert one unit of <paramref name="fromCurrencyId"/> into
    /// <paramref name="toCurrencyId"/> on <paramref name="date"/>. Returns 1 when
    /// the currencies are the same.
    /// </summary>
    Task<decimal> RateAsync(
        long fromCurrencyId, long toCurrencyId, DateOnly date,
        string rateTypeCode, CancellationToken cancellationToken);

    /// <summary>Translates and rounds to the four decimal places amounts are stored at.</summary>
    decimal Translate(decimal amount, decimal rate);
}

public sealed class CurrencyTranslator(S4herpDbContext db) : ICurrencyTranslator
{
    public async Task<decimal> RateAsync(
        long fromCurrencyId, long toCurrencyId, DateOnly date,
        string rateTypeCode, CancellationToken cancellationToken)
    {
        if (fromCurrencyId == toCurrencyId)
        {
            return 1m;
        }

        // Most recent rate valid on or before the date — a rate stays in force
        // until superseded.
        var rate = await (
            from r in db.Set<ExchangeRate>()
            join t in db.Set<ExchangeRateType>() on r.ExchangeRateTypeId equals t.Id
            where t.Code == rateTypeCode
                  && r.FromCurrencyId == fromCurrencyId
                  && r.ToCurrencyId == toCurrencyId
                  && r.ValidFrom <= date
            orderby r.ValidFrom descending
            select new { r.Rate, r.FromRatio, r.ToRatio }).FirstOrDefaultAsync(cancellationToken);

        if (rate is not null)
        {
            return rate.Rate * rate.ToRatio / rate.FromRatio;
        }

        // Fall back to the inverse before giving up: maintaining USD→KHR should
        // not oblige maintaining KHR→USD as well.
        var inverse = await (
            from r in db.Set<ExchangeRate>()
            join t in db.Set<ExchangeRateType>() on r.ExchangeRateTypeId equals t.Id
            where t.Code == rateTypeCode
                  && r.FromCurrencyId == toCurrencyId
                  && r.ToCurrencyId == fromCurrencyId
                  && r.ValidFrom <= date
            orderby r.ValidFrom descending
            select new { r.Rate, r.FromRatio, r.ToRatio }).FirstOrDefaultAsync(cancellationToken);

        if (inverse is not null && inverse.Rate != 0)
        {
            return inverse.FromRatio / (inverse.Rate * inverse.ToRatio);
        }

        var codes = await db.Set<Currency>()
            .Where(c => c.Id == fromCurrencyId || c.Id == toCurrencyId)
            .ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);

        throw new BusinessRuleException(
            PostingErrors.NoExchangeRate,
            $"No {rateTypeCode} exchange rate from {codes.GetValueOrDefault(fromCurrencyId, "?")} " +
            $"to {codes.GetValueOrDefault(toCurrencyId, "?")} is valid on {date:yyyy-MM-dd}.");
    }

    /// <summary>
    /// Translate per line, then verify the document balances — translating the
    /// total and distributing it produces lines that do not foot.
    /// </summary>
    public decimal Translate(decimal amount, decimal rate) =>
        decimal.Round(amount * rate, 4, MidpointRounding.AwayFromZero);
}
