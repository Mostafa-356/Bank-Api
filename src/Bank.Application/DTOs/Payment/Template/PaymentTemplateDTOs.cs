using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Payment.Template;

public class CreatePaymentTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid FromAccountId { get; set; }
    public Guid? ToAccountId { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? BeneficiaryAccountNumber { get; set; }
    public string? BeneficiaryBankCode { get; set; }
    public decimal? Amount { get; set; }
    public TransactionType Type { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public PaymentTemplateCategory Category { get; set; }
    public string[]? Tags { get; set; }
    public Guid CreatedByUserId { get; set; }
}

public class UpdatePaymentTemplateRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public Guid? ToAccountId { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? BeneficiaryAccountNumber { get; set; }
    public string? BeneficiaryBankCode { get; set; }
    public decimal? Amount { get; set; }
    public TransactionType? Type { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public PaymentTemplateCategory? Category { get; set; }
    public string[]? Tags { get; set; }
}

public class ExecuteTemplateRequest
{
    public decimal? Amount { get; set; } // Override template amount if provided
    public string? Reference { get; set; } // Override template reference if provided
    public string? Description { get; set; } // Override template description if provided
    public Guid? ToAccountId { get; set; } // Override beneficiary if provided
}

