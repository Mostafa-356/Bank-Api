using Bank.Application.DTOs.Loan.Core;
using Bank.Application.DTOs.Loan.Application;
using Bank.Application.DTOs.Loan.Approval;
using Bank.Application.DTOs.Loan.Disbursement;
using Bank.Application.DTOs.Loan.Repayment;
using Bank.Application.DTOs.Loan.Analytics;
using Bank.Application.DTOs.Loan.Configuration;
using Bank.Application.Validators.Base;
using FluentValidation;

namespace Bank.Application.Validators.Loan;

/// <summary>
/// Validator for loan search requests
/// </summary>
public class LoanSearchRequestValidator : AbstractValidator<LoanSearchRequest>
{
    public LoanSearchRequestValidator()
    {
        // Add amount range validation
        AmountRangeValidator.AddAmountRangeRules(this, x => x.MinAmount, x => x.MaxAmount);

        RuleFor(x => x)
            .Must(x => !x.ApplicationDateFrom.HasValue || !x.ApplicationDateTo.HasValue || x.ApplicationDateFrom <= x.ApplicationDateTo)
            .WithMessage("Application date from cannot be greater than application date to")
            .WithName("DateRange");

        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("Page number must be greater than zero");

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .WithMessage("Page size must be greater than zero")
            .LessThanOrEqualTo(100)
            .WithMessage("Page size cannot exceed 100");

        RuleFor(x => x.SortBy)
            .NotEmpty()
            .WithMessage("Sort by field is required")
            .Must(BeValidSortField)
            .WithMessage("Invalid sort field");
    }

    private static bool BeValidSortField(string sortBy)
    {
        var validFields = new[] { "ApplicationDate", "Amount", "Status", "Type", "LoanNumber" };
        return validFields.Contains(sortBy, StringComparer.OrdinalIgnoreCase);
    }
}

