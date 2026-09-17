using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Card.Advanced;

/// <summary>
/// Card usage statistics
/// </summary>
public class CardUsageStatsDto
{
    public Guid CardId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalTransactions { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalFees { get; set; }
    public int PurchaseCount { get; set; }
    public decimal PurchaseAmount { get; set; }
    public int WithdrawalCount { get; set; }
    public decimal WithdrawalAmount { get; set; }
    public int OnlineTransactionCount { get; set; }
    public decimal OnlineTransactionAmount { get; set; }
    public int InternationalTransactionCount { get; set; }
    public decimal InternationalTransactionAmount { get; set; }
    public decimal DailyLimitUtilization { get; set; }
    public decimal MonthlyLimitUtilization { get; set; }
    public List<MerchantCategoryUsageDto> MerchantCategoryBreakdown { get; set; } = new();
}

/// <summary>
/// Merchant category usage breakdown
/// </summary>
public class MerchantCategoryUsageDto
{
    public MerchantCategory Category { get; set; }
    public int TransactionCount { get; set; }
    public decimal Amount { get; set; }
    public decimal Percentage { get; set; }
}


