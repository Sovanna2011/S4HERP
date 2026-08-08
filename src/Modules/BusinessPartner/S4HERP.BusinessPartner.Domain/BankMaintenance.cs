using System.ComponentModel.DataAnnotations;
using S4HERP.BuildingBlocks.Domain;

namespace S4HERP.BusinessPartner.Domain;

/// <summary>
/// What a bank detail change is asking for. Deactivation rather than deletion:
/// a bank account a payment has already been made to is evidence of where the
/// money went, and the aging report, the payment file and any future statement
/// reconciliation all still need to resolve it.
/// </summary>
public enum BankChangeOperation
{
    Create = 1,
    Change = 2,
    Deactivate = 3,
}

public enum BankChangeStatus
{
    /// <summary>Raised, waiting on an approver. Nothing has changed yet.</summary>
    Pending = 1,

    /// <summary>Approved and applied to <see cref="PartnerBank"/>.</summary>
    Applied = 2,

    Rejected = 3,

    /// <summary>Pulled back by the requester before anyone decided.</summary>
    Withdrawn = 4,
}

/// <summary>
/// A proposed change to a business partner's bank details, held apart from the
/// details themselves until somebody else approves it.
///
/// The staging is the whole design. The obvious alternative — write the change
/// and mark the row "unapproved" — means the live record now holds unapproved
/// data, and every reader downstream has to remember to filter it out. The
/// payment run is one of those readers, and the consequence of it forgetting is
/// money paid into an account nobody signed off. Here, <see cref="PartnerBank"/>
/// only ever contains approved values, so a reader cannot get that wrong: there
/// is nothing to filter.
///
/// SOD001 rates "maintain vendor bank details" plus "run payments" as Critical,
/// because together they are the mechanism of invoice fraud — redirect the
/// account, run the payment, restore the account. Maker-checker here is the
/// runtime control that makes the first step visible to a second person.
/// </summary>
public class PartnerBankChangeRequest : AuditableEntity, IConcurrencyControlled
{
    /// <summary>
    /// Business key, and the workflow's object id. Sequence-allocated, because
    /// a request identifier built from a timestamp is unique until two people
    /// raise one in the same second.
    /// </summary>
    [MaxLength(40)] public required string RequestId { get; set; }

    public long PartnerId { get; set; }
    public Partner Partner { get; set; } = null!;

    public BankChangeOperation Operation { get; set; }
    public BankChangeStatus Status { get; set; } = BankChangeStatus.Pending;

    /// <summary>
    /// The record being changed or deactivated. Null for a create — there is
    /// nothing to point at yet.
    /// </summary>
    public long? PartnerBankId { get; set; }
    public PartnerBank? PartnerBank { get; set; }

    // The proposed values. Nullable across the board because a deactivation
    // proposes none of them, and required-ness is a per-operation rule the
    // handler enforces rather than something the column can express.
    [MaxLength(2)] public string? CountryCode { get; set; }
    [MaxLength(15)] public string? BankKey { get; set; }
    [MaxLength(120)] public string? BankName { get; set; }
    [MaxLength(35)] public string? AccountNumber { get; set; }
    [MaxLength(60)] public string? AccountHolder { get; set; }
    [MaxLength(34)] public string? Iban { get; set; }
    [MaxLength(11)] public string? Swift { get; set; }
    public bool IsDefault { get; set; }
    public DateOnly? ValidFrom { get; set; }

    /// <summary>
    /// What the live record held when the request was raised, as JSON. Kept so an
    /// approver can see what is actually changing rather than only what it is
    /// changing to — "account number 1234" tells them nothing; "1234, was 5678"
    /// is the whole question. Also the record of what was overwritten, since the
    /// live row is updated in place.
    /// </summary>
    public string? PreviousValues { get; set; }

    /// <summary>Mandatory on the request, because "why" is the part an approver judges.</summary>
    [MaxLength(400)] public required string Reason { get; set; }

    [MaxLength(64)] public required string RequestedBy { get; set; }
    public DateTime RequestedAtUtc { get; set; }

    [MaxLength(64)] public string? DecidedBy { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    [MaxLength(400)] public string? DecisionComment { get; set; }
}
