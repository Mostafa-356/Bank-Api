namespace Bank.Application.DTOs.Payment.Biller;

public class BillLineItemDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime? ServiceDate { get; set; }
}

