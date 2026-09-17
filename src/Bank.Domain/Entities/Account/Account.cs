using Bank.Domain.Common;
using Bank.Domain.Enums;

namespace Bank.Domain.Entities;

public class Account : BaseEntity
{
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    
    // Customer relationship
    public Guid CustomerId { get; set; } // Add CustomerId for compatibility
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // Account lifecycle properties
    public AccountStatus Status { get; set; } = AccountStatus.Active;
    public AccountType Type { get; set; } = AccountType.Checking;
    public DateTime OpenedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedDate { get; set; }
    public string? ClosureReason { get; set; }
    
    // Dormancy tracking
    public DateTime LastActivityDate { get; set; } = DateTime.UtcNow;
    public DateTime? DormancyDate { get; set; }
    public int DormancyPeriodDays { get; set; } = 365; // Default 1 year
    
    // Fee management
    public decimal MinimumBalance { get; set; } = 0;
    public decimal MonthlyMaintenanceFee { get; set; } = 0;
    public bool FeeWaiverEligible { get; set; } = false;
    public DateTime? LastFeeCalculationDate { get; set; }
    
    // Interest calculation
    public decimal InterestRate { get; set; } = 0;
    public DateTime? LastInterestCalculationDate { get; set; }
    public InterestCompoundingFrequency CompoundingFrequency { get; set; } = InterestCompoundingFrequency.Monthly;
    
    // Joint account properties
    public bool IsJointAccount { get; set; } = false;
    public bool RequiresMultipleSignatures { get; set; } = false;
    public decimal? MultipleSignatureThreshold { get; set; } // Amount above which multiple signatures are required
    public int MinimumSignaturesRequired { get; set; } = 1;
    
    // Account restrictions and holds
    public bool HasHolds { get; set; } = false;
    public bool HasRestrictions { get; set; } = false;

    public ICollection<Transaction> SentTransactions { get; set; } = new List<Transaction>();
    public ICollection<Transaction> ReceivedTransactions { get; set; } = new List<Transaction>();
    public ICollection<AccountFee> Fees { get; set; } = new List<AccountFee>();
    public ICollection<AccountHold> Holds { get; set; } = new List<AccountHold>();
    public ICollection<AccountRestriction> Restrictions { get; set; } = new List<AccountRestriction>();
    public ICollection<AccountStatusHistory> StatusHistory { get; set; } = new List<AccountStatusHistory>();
    public ICollection<JointAccountHolder> JointHolders { get; set; } = new List<JointAccountHolder>();
    
    // Business rules have been extracted to AccountTransactionPolicy, AccountDormancyPolicy, and JointAccountPolicy
}
