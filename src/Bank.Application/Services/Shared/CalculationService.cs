using Bank.Application.Interfaces;
using Bank.Application.Helpers.Loan;
using Bank.Application.Helpers.Payment;
using Bank.Application.Helpers.Deposit;
using Bank.Domain.Enums;

namespace Bank.Application.Services;

/// <summary>
/// Service implementation for financial calculations
/// Delegates to consolidated calculation helpers
/// </summary>
public class CalculationService : ICalculationService
{
    public decimal CalculateCompoundInterest(decimal principal, decimal rate, int compoundingFrequency, decimal time)
        => LoanCalculationHelper.CalculateCompoundInterest(principal, rate, compoundingFrequency, time);

    public decimal CalculateSimpleInterest(decimal principal, decimal rate, decimal time)
        => LoanCalculationHelper.CalculateSimpleInterest(principal, rate, time);

    public decimal CalculateInterestRateFromScore(int creditScore, decimal baseRate = 0.03m, decimal maxRate = 0.25m)
        => LoanCalculationHelper.CalculateInterestRateFromScore(creditScore, baseRate, maxRate);

    public decimal CalculateMaxLoanAmount(decimal monthlyIncome, decimal existingDebt, 
        decimal maxDebtToIncomeRatio, int loanTermMonths, decimal interestRate)
        => LoanCalculationHelper.CalculateMaxLoanAmount(monthlyIncome, existingDebt, maxDebtToIncomeRatio, loanTermMonths, interestRate);

    public (decimal PrincipalPayment, decimal InterestPayment) CalculatePaymentAllocation(
        decimal remainingBalance, decimal monthlyPayment, decimal monthlyInterestRate)
        => PaymentCalculationHelper.CalculatePaymentAllocation(remainingBalance, monthlyPayment, monthlyInterestRate);

    public decimal CalculateProcessingFee(decimal amount, PaymentMethod paymentMethod)
        => PaymentCalculationHelper.CalculateProcessingFee(amount, paymentMethod);

    public decimal CalculatePenaltyAmount(decimal amount, WithdrawalPenaltyType penaltyType, 
        decimal? penaltyAmount = null, decimal? penaltyPercentage = null)
        => PenaltyCalculationHelper.CalculatePenaltyAmount(amount, penaltyType, penaltyAmount, penaltyPercentage);

    public decimal CalculateDelinquencyRate(int delinquentLoans, int totalLoans)
        => LoanCalculationHelper.CalculateDelinquencyRate(delinquentLoans, totalLoans);

    public decimal CalculateDefaultRate(int defaultedLoans, int totalLoans)
        => LoanCalculationHelper.CalculateDefaultRate(defaultedLoans, totalLoans);

    public string CalculateRiskLevel(int creditScore, decimal debtToIncomeRatio, decimal loanToValueRatio = 0)
        => LoanCalculationHelper.CalculateRiskLevel(creditScore, debtToIncomeRatio, loanToValueRatio);

    public decimal CalculateMonthlyPayment(decimal principal, decimal annualRate, int termMonths)
        => LoanCalculationHelper.CalculateMonthlyPayment(principal, annualRate, termMonths);
}