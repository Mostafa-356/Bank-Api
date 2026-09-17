using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Deposit.Maturity;

/// <summary>
/// Core maturity information for a deposit
/// </summary>
public class MaturityDto
{
    public Guid DepositId { get; set; }
    public DateTime MaturityDate { get; set; }
    public decimal MaturityAmount { get; set; }
    public MaturityAction MaturityAction { get; set; }
    public int? RenewalTerm { get; set; }
    public decimal? RenewalInterestRate { get; set; }
    public Guid? WithdrawalAccountId { get; set; }
    public InterestReinvestmentType? InterestReinvestmentType { get; set; }
    public NotificationPreference? NotificationPreference { get; set; }
}

/// <summary>
/// Maturity details data transfer object
/// </summary>
public class MaturityDetailsDto
{
    public Guid DepositId { get; set; }
    public DateTime MaturityDate { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal AccruedInterest { get; set; }
    public decimal MaturityAmount { get; set; }
    public MaturityAction DefaultAction { get; set; }
    public bool AutoRenewalEnabled { get; set; }
    public int? RenewalTermDays { get; set; }
    public bool CustomerConsentReceived { get; set; }
    public List<MaturityActionOption> AvailableActions { get; set; } = new();
}

/// <summary>
/// Maturity action option
/// </summary>
public class MaturityActionOption
{
    public MaturityAction Action { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool RequiresCustomerConsent { get; set; }
}

/// <summary>
/// Maturity reminder data transfer object
/// </summary>
public class MaturityReminderDto
{
    public Guid DepositId { get; set; }
    public string DepositNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public DateTime MaturityDate { get; set; }
    public int DaysToMaturity { get; set; }
    public decimal MaturityAmount { get; set; }
    public bool AutoRenewalEnabled { get; set; }
    public bool CustomerConsentReceived { get; set; }
    public MaturityAction DefaultAction { get; set; }
    public List<MaturityNoticeDto> SentNotices { get; set; } = new();
}

/// <summary>
/// Customer consent request
/// </summary>
public class CustomerConsentRequest
{
    public bool ConsentGiven { get; set; }
    public MaturityAction? PreferredAction { get; set; }
    public int? PreferredRenewalTerm { get; set; }
    public string? CustomerNotes { get; set; }
}

/// <summary>
/// Auto-renewal summary data transfer object
/// </summary>
public class AutoRenewalSummaryDto
{
    public int TotalEligible { get; set; }
    public int SuccessfulRenewals { get; set; }
    public int FailedRenewals { get; set; }
    public int PendingConsent { get; set; }
    public decimal TotalRenewedAmount { get; set; }
    public List<string> Errors { get; set; } = new();
}
