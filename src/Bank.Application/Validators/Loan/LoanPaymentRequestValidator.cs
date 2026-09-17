using Bank.Application.DTOs.Loan.Core;
using Bank.Application.DTOs.Loan.Application;
using Bank.Application.DTOs.Loan.Approval;
using Bank.Application.DTOs.Loan.Disbursement;
using Bank.Application.DTOs.Loan.Repayment;
using Bank.Application.DTOs.Loan.Analytics;
using Bank.Application.DTOs.Loan.Configuration;
using FluentValidation;

namespace Bank.Application.Validators.Loan;

/// <summary>
/// Validator for loan payment requests
/// </summary>
public class LoanPaymentRequestValidator : AbstractValidator<LoanPaymentRequest>
{
    public LoanPaymentRequestValidator()
    {
        RuleFor(x => x.LoanId)
            .NotEmpty()
            .WithMessage("Loan ID is required");

        RuleFor(x => x.PaymentAmount)
            .GreaterThan(0)
            .WithMessage("Payment amount must be greater than zero")
            .LessThanOrEqualTo(1_000_000)
            .WithMessage("Payment amount cannot exceed $1,000,000");

        RuleFor(x => x.PaymentMethod)
            .NotEmpty()
            .WithMessage("Payment method is required")
            .MaximumLength(50)
            .WithMessage("Payment method cannot exceed 50 characters");

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .WithMessage("Notes cannot exceed 1000 characters");
    }
}

