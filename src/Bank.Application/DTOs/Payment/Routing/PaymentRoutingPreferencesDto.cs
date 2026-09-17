using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Payment.Routing;

public class PaymentRoutingPreferences
{
    public Guid BillerId { get; set; }
    public PaymentMethod PreferredMethod { get; set; }
    public List<PaymentMethod> SupportedMethods { get; set; } = new();
    public TimeSpan ProcessingWindow { get; set; }
    public decimal MaxDailyAmount { get; set; }
    public int MaxDailyTransactions { get; set; }
    public bool RequiresPreAuthorization { get; set; }
    public Dictionary<string, object> RoutingRules { get; set; } = new();
}

