namespace Bank.Application.DTOs.Deposit.Core;

/// <summary>
/// Deposit summary data transfer object
/// </summary>
public class DepositSummaryDto
{
    public Guid CustomerId { get; set; }
    public int TotalDeposits { get; set; }
    public decimal TotalPrincipal { get; set; }
    public decimal TotalAccruedInterest { get; set; }
    public decimal TotalMaturityValue { get; set; }
    public int ActiveDeposits { get; set; }
    public int MaturingThisMonth { get; set; }
    public decimal AverageInterestRate { get; set; }
}

/// <summary>
/// Deposit portfolio data transfer object
/// </summary>
public class DepositPortfolioDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DepositSummaryDto Summary { get; set; } = new();
    public List<FixedDepositDto> ActiveDeposits { get; set; } = new();
    public List<FixedDepositDto> MaturingDeposits { get; set; } = new();
    public List<DepositTransactionDto> RecentTransactions { get; set; } = new();
}


