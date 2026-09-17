using Bank.Application.DTOs.Loan.Core;
using Bank.Application.DTOs.Loan.Application;
using Bank.Application.DTOs.Loan.Approval;
using Bank.Application.DTOs.Loan.Disbursement;
using Bank.Application.DTOs.Loan.Repayment;
using Bank.Application.DTOs.Loan.Analytics;
using Bank.Application.DTOs.Loan.Configuration;
using Bank.Domain.Enums;
using FluentValidation;

namespace Bank.Application.Validators.Loan;

/// <summary>
/// Validator for loan application requests
/// </summary>
public class LoanApplicationRequestValidator : AbstractValidator<LoanApplicationRequest>
{
    public LoanApplicationRequestValidator()
    {
        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Invalid loan type");

        RuleFor(x => x.RequestedAmount)
            .GreaterThan(0)
            .WithMessage("Requested amount must be greater than zero")
            .LessThanOrEqualTo(10_000_000)
            .WithMessage("Requested amount cannot exceed $10,000,000");

        RuleFor(x => x.TermInMonths)
            .GreaterThan(0)
            .WithMessage("Term must be greater than zero")
            .LessThanOrEqualTo(360)
            .WithMessage("Term cannot exceed 360 months (30 years)");

        RuleFor(x => x.Purpose)
            .NotEmpty()
            .WithMessage("Purpose is required")
            .MaximumLength(500)
            .WithMessage("Purpose cannot exceed 500 characters");

        RuleFor(x => x.MonthlyIncome)
            .GreaterThan(0)
            .WithMessage("Monthly income must be greater than zero");

        RuleFor(x => x.ExistingDebtAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Existing debt amount cannot be negative");

        RuleFor(x => x.EmploymentStatus)
            .NotEmpty()
            .WithMessage("Employment status is required")
            .MaximumLength(100)
            .WithMessage("Employment status cannot exceed 100 characters");

        RuleFor(x => x.EmploymentYears)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Employment years cannot be negative")
            .LessThanOrEqualTo(50)
            .WithMessage("Employment years cannot exceed 50");

        // Conditional validation for collateral
        When(x => x.Type == LoanType.Auto || x.Type == LoanType.Mortgage, () =>
        {
            RuleFor(x => x.CollateralDescription)
                .NotEmpty()
                .WithMessage("Collateral description is required for secured loans");

            RuleFor(x => x.CollateralValue)
                .GreaterThan(0)
                .WithMessage("Collateral value must be greater than zero for secured loans");
        });

        // Business logic validations
        RuleFor(x => x)
            .Must(HaveReasonableDebtToIncomeRatio)
            .WithMessage("Debt-to-income ratio is too high (maximum 43%)")
            .WithName("DebtToIncomeRatio");

        RuleFor(x => x)
            .Must(HaveReasonableLoanToValueRatio)
            .WithMessage("Loan-to-value ratio is too high (maximum 80%)")
            .WithName("LoanToValueRatio")
            .When(x => x.CollateralValue.HasValue && x.CollateralValue > 0);
    }

    private static bool HaveReasonableDebtToIncomeRatio(LoanApplicationRequest request)
    {
        if (request.MonthlyIncome <= 0) return false;

        // Estimate monthly payment (simplified calculation)
        var estimatedMonthlyPayment = request.RequestedAmount * 0.01m; // Rough estimate
        var totalMonthlyDebt = (request.ExistingDebtAmount / 12) + estimatedMonthlyPayment;
        var debtToIncomeRatio = totalMonthlyDebt / request.MonthlyIncome;

        return debtToIncomeRatio <= 0.43m; // 43% maximum DTI ratio
    }

    private static bool HaveReasonableLoanToValueRatio(LoanApplicationRequest request)
    {
        if (!request.CollateralValue.HasValue || request.CollateralValue <= 0)
            return true; // Skip validation if no collateral

        var loanToValueRatio = request.RequestedAmount / request.CollateralValue.Value;
        return loanToValueRatio <= 0.80m; // 80% maximum LTV ratio
    }
}

