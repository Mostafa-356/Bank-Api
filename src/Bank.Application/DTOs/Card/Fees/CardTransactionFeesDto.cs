using System.ComponentModel.DataAnnotations;
using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Card.Fees;

/// <summary>
/// Transaction fees calculation
/// </summary>
public class CardTransactionFees
{
    public decimal InterchangeFee { get; set; }
    public decimal ProcessingFee { get; set; }
    public decimal NetworkFee { get; set; }
    public decimal TotalFees { get; set; }
    public string Currency { get; set; } = "USD";
}

/// <summary>
/// Request for transaction fee calculation
/// </summary>
public class CardTransactionFeeRequest
{
    [Required]
    public decimal Amount { get; set; }
    
    [Required]
    public CardNetwork Network { get; set; }
    
    [Required]
    public CardTransactionType TransactionType { get; set; }
    
    public MerchantCategory MerchantCategory { get; set; }
    
    public bool IsInternational { get; set; }
    
    public bool IsOnline { get; set; }
    
    public string Currency { get; set; } = "USD";
}


