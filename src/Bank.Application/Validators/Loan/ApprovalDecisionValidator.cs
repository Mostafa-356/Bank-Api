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
/// Validator for approval decisions
/// </summary>
public class ApprovalDecisionValidator : AbstractValidator<ApprovalDecision>
{
    public ApprovalDecisionValidator()
    {
        When(x => x.IsApproved, () =>
        {
            RuleFor(x => x.ApprovedAmount)
                .GreaterThan(0)
                .WithMessage("Approved amount must be greater than zero when loan is approved");

            RuleFor(x => x.InterestRate)
                .GreaterThan(0)
                .WithMessage("Interest rate must be greater than zero when loan is approved")
                .LessThanOrEqualTo(50)
                .WithMessage("Interest rate cannot exceed 50%");

            RuleFor(x => x.ApprovedTermInMonths)
                .GreaterThan(0)
                .WithMessage("Approved term must be greater than zero when loan is approved")
                .LessThanOrEqualTo(360)
                .WithMessage("Approved term cannot exceed 360 months");
        });

        When(x => !x.IsApproved, () =>
        {
            RuleFor(x => x.RejectionReason)
                .NotEmpty()
                .WithMessage("Rejection reason is required when loan is rejected")
                .MaximumLength(1000)
                .WithMessage("Rejection reason cannot exceed 1000 characters");
        });

        RuleFor(x => x.Conditions)
            .MaximumLength(2000)
            .WithMessage("Conditions cannot exceed 2000 characters");
    }
}

