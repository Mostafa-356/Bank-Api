using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Account.JointAccount;

/// <summary>
/// Detailed information about a joint account holder
/// </summary>
public class JointAccountHolderDetailsDto
{
    /// <summary>
    /// Holder record ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Associated account ID
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// User ID of the account holder
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Username of the account holder
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Email of the account holder
    /// </summary>
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// Role of the holder in the joint account
    /// </summary>
    public JointAccountRole Role { get; set; }

    /// <summary>
    /// Access level for the holder
    /// </summary>
    public JointAccountAccessLevel AccessLevel { get; set; }

    /// <summary>
    /// Transaction limit for this holder (optional)
    /// </summary>
    public decimal? TransactionLimit { get; set; }

    /// <summary>
    /// Whether this holder is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// When this holder joined the account
    /// </summary>
    public DateTime JoinedDate { get; set; }
}
