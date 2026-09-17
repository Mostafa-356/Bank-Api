using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Card.Advanced;

/// <summary>
/// Card network status
/// </summary>
public class CardNetworkStatus
{
    public CardNetwork Network { get; set; }
    public bool IsOnline { get; set; }
    public DateTime LastStatusCheck { get; set; }
    public string? StatusMessage { get; set; }
    public TimeSpan? ResponseTime { get; set; }
}


