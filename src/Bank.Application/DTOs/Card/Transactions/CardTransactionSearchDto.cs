using Bank.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Bank.Application.DTOs.Card.Transactions;

/// <summary>
/// Request to search card transactions
/// </summary>
public class CardTransactionSearchRequest : PagedRequest
{
    [Required]
    public Guid CardId { get; set; }
    
    [Required]
    public Guid CustomerId { get; set; }
    
    public DateTime? FromDate { get; set; }
    
    public DateTime? ToDate { get; set; }
    
    public CardTransactionType? TransactionType { get; set; }
    
    public CardTransactionStatus? Status { get; set; }
    
    public decimal? MinAmount { get; set; }
    
    public decimal? MaxAmount { get; set; }
    
    public string? MerchantName { get; set; }
    
    public MerchantCategory? MerchantCategory { get; set; }
    
    public bool? IsInternational { get; set; }
}


