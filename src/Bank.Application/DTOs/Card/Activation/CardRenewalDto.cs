namespace Bank.Application.DTOs.Card.Activation;

/// <summary>
/// Request for card renewal
/// </summary>
public class CardRenewalResult
{
    public bool Success { get; set; }
    public int ProcessedCount { get; set; }
    public int SuccessfulRenewals { get; set; }
    public int FailedRenewals { get; set; }
    public List<CardRenewalInfo> RenewalDetails { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Card renewal information
/// </summary>
public class CardRenewalInfo
{
    public Guid CardId { get; set; }
    public string CardNumber { get; set; } = string.Empty;
    public Guid? NewCardId { get; set; }
    public string? NewCardNumber { get; set; }
    public DateTime OldExpiryDate { get; set; }
    public DateTime NewExpiryDate { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}


