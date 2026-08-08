using Microsoft.EntityFrameworkCore;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Finance.Domain;
using S4HERP.Workflow.Contracts;

namespace S4HERP.Finance.Application;

/// <summary>
/// What Finance's approvable objects look like in the inbox. Finance owns these
/// objects, so Finance says what they are called — Workflow asking would mean
/// Workflow knowing, and the dependency only runs one way (ADR-02).
/// </summary>
public sealed class JournalEntryDescriber(S4herpDbContext db) : IApprovalObjectDescriber
{
    public string ObjectType => JournalWorkflowLookup.ObjectType;

    public async Task<IReadOnlyDictionary<string, ApprovalObjectDescription>> DescribeAsync(
        IReadOnlyList<string> objectIds, CancellationToken cancellationToken = default)
    {
        var headers = await db.Set<JournalEntryHeader>()
            .AsNoTracking()
            .Include(h => h.DocumentType)
            .Where(h => objectIds.Contains(h.DocumentNumberFormatted))
            .Select(h => new
            {
                h.DocumentNumberFormatted,
                DocumentType = h.DocumentType.Code,
                h.HeaderText,
                h.PostingDate,
                h.Reference,
            })
            .ToListAsync(cancellationToken);

        return headers.ToDictionary(
            h => h.DocumentNumberFormatted,
            h => new ApprovalObjectDescription(
                $"{h.DocumentType} {h.DocumentNumberFormatted}",
                // The header text is what the preparer wrote to explain the
                // posting, so it is the most useful single line an approver can
                // be given before opening it. The date matters because an old
                // document waiting in an inbox is itself a signal.
                string.IsNullOrWhiteSpace(h.HeaderText)
                    ? $"Posting date {h.PostingDate:yyyy-MM-dd}"
                    : $"{h.HeaderText} · {h.PostingDate:yyyy-MM-dd}"));
    }
}

public sealed class PaymentRunDescriber(S4herpDbContext db) : IApprovalObjectDescriber
{
    public string ObjectType => PaymentRunApproval.ObjectType;

    public async Task<IReadOnlyDictionary<string, ApprovalObjectDescription>> DescribeAsync(
        IReadOnlyList<string> objectIds, CancellationToken cancellationToken = default)
    {
        var runs = await db.Set<PaymentRun>()
            .AsNoTracking()
            .Where(r => objectIds.Contains(r.RunId))
            .Select(r => new
            {
                r.RunId,
                r.RunDate,
                r.PaymentMethodCode,
                Payees = r.Items.Select(i => i.BusinessPartnerId).Distinct().Count(),
            })
            .ToListAsync(cancellationToken);

        return runs.ToDictionary(
            r => r.RunId,
            r => new ApprovalObjectDescription(
                $"Payment run {r.RunId}",
                // How many creditors, not just how much: a run of one large
                // payment and a run of two hundred small ones need different
                // scrutiny, and the total alone does not distinguish them.
                $"{r.Payees} payee{(r.Payees == 1 ? "" : "s")} · method {r.PaymentMethodCode} · " +
                $"run date {r.RunDate:yyyy-MM-dd}"));
    }
}
