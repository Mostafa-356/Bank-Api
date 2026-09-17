using Bank.Application.DTOs;
using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

/// <summary>
/// Service for loan-specific interest calculations and amortization
/// </summary>
public class LoanInterestCalculationService : ILoanInterestCalculationService
{
    private readonly ILoanRepository _loanRepository;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<LoanInterestCalculationService> _logger;

    public LoanInterestCalculationService(
        ILoanRepository loanRepository,
        IAuditLogService auditLogService,
        ILogger<LoanInterestCalculationService> logger)
    {
        _loanRepository = loanRepository;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<LoanInterestCalculationResult> CalculateLoanInterestAsync(Loan loan, decimal paymentAmount)
    {
        try
        {
            var result = new LoanInterestCalculationResult
            {
                CalculationMethod = loan.CalculationMethod,
                CalculationDate = DateTime.UtcNow,
                TotalPayment = paymentAmount
            };

            switch (loan.CalculationMethod)
            {
                case InterestCalculationMethod.Simple:
                    result = await CalculateSimpleInterestPaymentAsync(loan, paymentAmount);
                    break;
                case InterestCalculationMethod.CompoundMonthly:
                    result = await CalculateCompoundInterestPaymentAsync(loan, paymentAmount, 12);
                    break;
                case InterestCalculationMethod.CompoundDaily:
                    result = await CalculateCompoundInterestPaymentAsync(loan, paymentAmount, 365);
                    break;
                case InterestCalculationMethod.ReducingBalance:
                    result = await CalculateReducingBalancePaymentAsync(loan, paymentAmount);
                    break;
                case InterestCalculationMethod.FlatRate:
                    result = await CalculateFlatRatePaymentAsync(loan, paymentAmount);
                    break;
                default:
                    result = await CalculateReducingBalancePaymentAsync(loan, paymentAmount);
                    break;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating loan interest for loan {LoanId}", loan.Id);
            throw;
        }
    }

    public async Task<decimal> CalculateMonthlyPaymentAsync(decimal principal, decimal annualRate, int termInMonths, InterestCalculationMethod method)
    {
        try
        {
            if (principal <= 0 || annualRate < 0 || termInMonths <= 0)
                return 0;

            return method switch
            {
                InterestCalculationMethod.Simple => CalculateSimpleInterestMonthlyPayment(principal, annualRate, termInMonths),
                InterestCalculationMethod.CompoundMonthly => CalculateCompoundMonthlyPayment(principal, annualRate, termInMonths),
                InterestCalculationMethod.CompoundDaily => CalculateCompoundDailyPayment(principal, annualRate, termInMonths),
                InterestCalculationMethod.ReducingBalance => CalculateReducingBalanceMonthlyPayment(principal, annualRate, termInMonths),
                InterestCalculationMethod.FlatRate => CalculateFlatRateMonthlyPayment(principal, annualRate, termInMonths),
                _ => CalculateReducingBalanceMonthlyPayment(principal, annualRate, termInMonths)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating monthly payment for principal {Principal}, rate {Rate}, term {Term}", 
                principal, annualRate, termInMonths);
            return 0;
        }
    }

    public async Task<AmortizationSchedule> GenerateAmortizationScheduleAsync(Loan loan)
    {
        try
        {
            var schedule = new AmortizationSchedule
            {
                LoanId = loan.Id,
                LoanNumber = loan.LoanNumber,
                Schedule = new List<AmortizationEntry>()
            };

            var balance = loan.PrincipalAmount;
            var monthlyPayment = loan.MonthlyPaymentAmount;
            var monthlyRate = loan.InterestRate / 100 / 12;
            var cumulativeInterest = 0m;
            var cumulativePrincipal = 0m;
            var startDate = loan.DisbursementDate ?? loan.ApprovalDate ?? DateTime.UtcNow;

            for (int i = 1; i <= loan.TermInMonths && balance > 0; i++)
            {
                var interestAmount = CalculateInterestForPeriod(balance, loan.InterestRate, loan.CalculationMethod);
                var principalAmount = Math.Min(monthlyPayment - interestAmount, balance);
                
                // Ensure we don't have negative principal
                if (principalAmount < 0)
                {
                    principalAmount = 0;
                    interestAmount = monthlyPayment;
                }

                balance -= principalAmount;
                cumulativeInterest += interestAmount;
                cumulativePrincipal += principalAmount;

                var entry = new AmortizationEntry
                {
                    PaymentNumber = i,
                    PaymentDate = startDate.AddMonths(i),
                    PaymentAmount = i == loan.TermInMonths && balance <= 0.01m ? principalAmount + interestAmount : monthlyPayment,
                    PrincipalAmount = Math.Round(principalAmount, 2),
                    InterestAmount = Math.Round(interestAmount, 2),
                    RemainingBalance = Math.Max(0, Math.Round(balance, 2)),
                    CumulativeInterest = Math.Round(cumulativeInterest, 2),
                    CumulativePrincipal = Math.Round(cumulativePrincipal, 2)
                };

                schedule.Schedule.Add(entry);

                if (balance <= 0.01m) break; // Account for rounding
            }

            schedule.TotalInterest = cumulativeInterest;
            schedule.TotalPayments = schedule.Schedule.Sum(s => s.PaymentAmount);

            return schedule;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating amortization schedule for loan {LoanId}", loan.Id);
            throw;
        }
    }

    public async Task<decimal> CalculateRemainingInterestAsync(Loan loan)
    {
        try
        {
            var schedule = await GenerateAmortizationScheduleAsync(loan);
            var paidPayments = loan.Payments.Count(p => p.Status == LoanPaymentStatus.Paid);
            
            return schedule.Schedule
                .Skip(paidPayments)
                .Sum(s => s.InterestAmount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating remaining interest for loan {LoanId}", loan.Id);
            return 0;
        }
    }

    public async Task<EarlyPayoffCalculation> CalculateEarlyPayoffAmountAsync(Guid loanId, DateTime payoffDate)
    {
        try
        {
            var loan = await _loanRepository.GetByIdAsync(loanId);
            if (loan == null)
                throw new ArgumentException($"Loan {loanId} not found");

            var accruedInterest = await CalculateAccruedInterestAsync(loan, loan.LastPaymentDate ?? loan.DisbursementDate ?? DateTime.UtcNow, payoffDate);
            var config = await GetLoanTypeConfigurationAsync(loan.Type);
            var prepaymentPenalty = loan.OutstandingBalance * (config.PrepaymentPenaltyPercentage / 100);
            
            // Calculate interest savings
            var remainingInterest = await CalculateRemainingInterestAsync(loan);
            var interestSavings = Math.Max(0, remainingInterest - accruedInterest - prepaymentPenalty);
            
            // Calculate payments saved
            var remainingPayments = loan.TermInMonths - loan.Payments.Count(p => p.Status == LoanPaymentStatus.Paid);

            return new EarlyPayoffCalculation
            {
                LoanId = loanId,
                PayoffDate = payoffDate,
                CurrentBalance = loan.OutstandingBalance,
                AccruedInterest = accruedInterest,
                PrepaymentPenalty = config.AllowsEarlyPayoff ? prepaymentPenalty : 0,
                TotalPayoffAmount = loan.OutstandingBalance + accruedInterest + (config.AllowsEarlyPayoff ? prepaymentPenalty : 0),
                InterestSavings = interestSavings,
                PaymentsSaved = remainingPayments
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating early payoff amount for loan {LoanId}", loanId);
            throw;
        }
    }

    public async Task<decimal> GetInterestRateForLoanTypeAsync(LoanType loanType, int creditScore, decimal loanAmount)
    {
        try
        {
            var config = await GetLoanTypeConfigurationAsync(loanType);
            var baseRate = config.BaseInterestRate;
            
            // Adjust rate based on credit score
            var creditAdjustment = creditScore switch
            {
                >= 800 => -1.5m,
                >= 740 => -1.0m,
                >= 670 => -0.5m,
                >= 580 => 0.5m,
                _ => 2.0m
            };
            
            // Adjust rate based on loan amount (larger loans get better rates)
            var amountAdjustment = loanAmount switch
            {
                >= 500000 => -0.5m,
                >= 100000 => -0.25m,
                >= 50000 => 0m,
                _ => 0.25m
            };
            
            var finalRate = baseRate + creditAdjustment + amountAdjustment;
            return Math.Max(1.0m, Math.Min(25.0m, finalRate)); // Cap between 1% and 25%
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting interest rate for loan type {LoanType}", loanType);
            return 10.0m; // Default fallback rate
        }
    }

    public async Task<decimal> CalculateLateFeeAsync(Loan loan, int daysOverdue)
    {
        try
        {
            if (daysOverdue <= 0) return 0;
            
            var config = await GetLoanTypeConfigurationAsync(loan.Type);
            var lateFeePercentage = 0.05m; // 5% late fee
            var maxLateFee = loan.MonthlyPaymentAmount * 0.1m; // Max 10% of monthly payment
            
            var lateFee = loan.MonthlyPaymentAmount * lateFeePercentage;
            return Math.Min(lateFee, maxLateFee);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating late fee for loan {LoanId}", loan.Id);
            return 0;
        }
    }

    public async Task<bool> UpdateLoanInterestRateAsync(Guid loanId, decimal newRate, Guid updatedBy)
    {
        try
        {
            var loan = await _loanRepository.GetByIdAsync(loanId);
            if (loan == null) return false;
            
            var oldRate = loan.InterestRate;
            loan.InterestRate = newRate;
            
            // Recalculate monthly payment
            loan.MonthlyPaymentAmount = await CalculateMonthlyPaymentAsync(
                loan.OutstandingBalance, newRate, 
                loan.TermInMonths - loan.Payments.Count(p => p.Status == LoanPaymentStatus.Paid),
                loan.CalculationMethod);
            
            await _loanRepository.UpdateAsync(loan);
            
            await _auditLogService.LogAsync("Loan Interest Rate Updated", 
                $"Loan {loanId} interest rate updated from {oldRate}% to {newRate}%", updatedBy);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating loan interest rate for loan {LoanId}", loanId);
            return false;
        }
    }

    public async Task<decimal> CalculateAccruedInterestAsync(Loan loan, DateTime fromDate, DateTime toDate)
    {
        try
        {
            var days = (toDate - fromDate).TotalDays;
            if (days <= 0) return 0;
            
            var dailyRate = loan.InterestRate / 100 / 365;
            return loan.OutstandingBalance * dailyRate * (decimal)days;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating accrued interest for loan {LoanId}", loan.Id);
            return 0;
        }
    }

    public async Task<LoanTypeConfiguration> GetLoanTypeConfigurationAsync(LoanType loanType)
    {
        // In a real implementation, this would come from a database configuration table
        return loanType switch
        {
            LoanType.Personal => new LoanTypeConfiguration
            {
                LoanType = LoanType.Personal,
                TypeName = "Personal Loan",
                MinimumAmount = 1000,
                MaximumAmount = 100000,
                MinimumTermMonths = 12,
                MaximumTermMonths = 84,
                BaseInterestRate = 12.0m,
                DefaultCalculationMethod = InterestCalculationMethod.ReducingBalance,
                RequiresCollateral = false,
                MaxLoanToValueRatio = 0,
                RequiredDocuments = new List<string> { "Identity Proof", "Income Proof", "Address Proof" },
                ProcessingFeePercentage = 2.0m,
                PrepaymentPenaltyPercentage = 2.0m,
                AllowsEarlyPayoff = true
            },
            LoanType.Auto => new LoanTypeConfiguration
            {
                LoanType = LoanType.Auto,
                TypeName = "Auto Loan",
                MinimumAmount = 5000,
                MaximumAmount = 500000,
                MinimumTermMonths = 12,
                MaximumTermMonths = 84,
                BaseInterestRate = 8.0m,
                DefaultCalculationMethod = InterestCalculationMethod.ReducingBalance,
                RequiresCollateral = true,
                MaxLoanToValueRatio = 0.9m,
                RequiredDocuments = new List<string> { "Identity Proof", "Income Proof", "Vehicle Registration", "Insurance" },
                ProcessingFeePercentage = 1.0m,
                PrepaymentPenaltyPercentage = 1.0m,
                AllowsEarlyPayoff = true
            },
            LoanType.Mortgage => new LoanTypeConfiguration
            {
                LoanType = LoanType.Mortgage,
                TypeName = "Mortgage Loan",
                MinimumAmount = 50000,
                MaximumAmount = 2000000,
                MinimumTermMonths = 120,
                MaximumTermMonths = 360,
                BaseInterestRate = 6.0m,
                DefaultCalculationMethod = InterestCalculationMethod.CompoundMonthly,
                RequiresCollateral = true,
                MaxLoanToValueRatio = 0.8m,
                RequiredDocuments = new List<string> { "Identity Proof", "Income Proof", "Property Documents", "Property Valuation" },
                ProcessingFeePercentage = 0.5m,
                PrepaymentPenaltyPercentage = 0.5m,
                AllowsEarlyPayoff = true
            },
            LoanType.Business => new LoanTypeConfiguration
            {
                LoanType = LoanType.Business,
                TypeName = "Business Loan",
                MinimumAmount = 10000,
                MaximumAmount = 1000000,
                MinimumTermMonths = 12,
                MaximumTermMonths = 120,
                BaseInterestRate = 10.0m,
                DefaultCalculationMethod = InterestCalculationMethod.ReducingBalance,
                RequiresCollateral = true,
                MaxLoanToValueRatio = 0.7m,
                RequiredDocuments = new List<string> { "Business Registration", "Financial Statements", "Tax Returns", "Business Plan" },
                ProcessingFeePercentage = 1.5m,
                PrepaymentPenaltyPercentage = 1.5m,
                AllowsEarlyPayoff = true
            },
            LoanType.Education => new LoanTypeConfiguration
            {
                LoanType = LoanType.Education,
                TypeName = "Education Loan",
                MinimumAmount = 5000,
                MaximumAmount = 200000,
                MinimumTermMonths = 24,
                MaximumTermMonths = 180,
                BaseInterestRate = 7.0m,
                DefaultCalculationMethod = InterestCalculationMethod.Simple,
                RequiresCollateral = false,
                MaxLoanToValueRatio = 0,
                RequiredDocuments = new List<string> { "Identity Proof", "Admission Letter", "Fee Structure", "Income Proof" },
                ProcessingFeePercentage = 1.0m,
                PrepaymentPenaltyPercentage = 0m,
                AllowsEarlyPayoff = true
            },
            LoanType.HomeEquity => new LoanTypeConfiguration
            {
                LoanType = LoanType.HomeEquity,
                TypeName = "Home Equity Loan",
                MinimumAmount = 25000,
                MaximumAmount = 500000,
                MinimumTermMonths = 60,
                MaximumTermMonths = 240,
                BaseInterestRate = 7.5m,
                DefaultCalculationMethod = InterestCalculationMethod.CompoundMonthly,
                RequiresCollateral = true,
                MaxLoanToValueRatio = 0.8m,
                RequiredDocuments = new List<string> { "Identity Proof", "Property Documents", "Property Valuation", "Income Proof" },
                ProcessingFeePercentage = 1.0m,
                PrepaymentPenaltyPercentage = 1.0m,
                AllowsEarlyPayoff = true
            },
            _ => throw new ArgumentException($"Unsupported loan type: {loanType}")
        };
    }

    #region Private Helper Methods

    private async Task<LoanInterestCalculationResult> CalculateSimpleInterestPaymentAsync(Loan loan, decimal paymentAmount)
    {
        var daysInYear = 365;
        var daysSinceLastPayment = (DateTime.UtcNow - (loan.LastPaymentDate ?? loan.DisbursementDate ?? DateTime.UtcNow)).Days;
        var interestAmount = loan.OutstandingBalance * (loan.InterestRate / 100) * daysSinceLastPayment / daysInYear;
        var principalAmount = Math.Max(0, paymentAmount - interestAmount);

        return new LoanInterestCalculationResult
        {
            InterestAmount = Math.Round(interestAmount, 2),
            PrincipalAmount = Math.Round(principalAmount, 2),
            TotalPayment = paymentAmount,
            RemainingBalance = Math.Max(0, loan.OutstandingBalance - principalAmount),
            CalculationMethod = InterestCalculationMethod.Simple,
            CalculationDate = DateTime.UtcNow,
            DaysCalculated = daysSinceLastPayment
        };
    }

    private async Task<LoanInterestCalculationResult> CalculateCompoundInterestPaymentAsync(Loan loan, decimal paymentAmount, int compoundingFrequency)
    {
        var periodsPerYear = compoundingFrequency;
        var periodsSinceLastPayment = 1; // Assuming monthly payments
        var periodRate = loan.InterestRate / 100 / periodsPerYear;
        var interestAmount = loan.OutstandingBalance * periodRate;
        var principalAmount = Math.Max(0, paymentAmount - interestAmount);

        return new LoanInterestCalculationResult
        {
            InterestAmount = Math.Round(interestAmount, 2),
            PrincipalAmount = Math.Round(principalAmount, 2),
            TotalPayment = paymentAmount,
            RemainingBalance = Math.Max(0, loan.OutstandingBalance - principalAmount),
            CalculationMethod = compoundingFrequency == 365 ? InterestCalculationMethod.CompoundDaily : InterestCalculationMethod.CompoundMonthly,
            CalculationDate = DateTime.UtcNow,
            DaysCalculated = 30 // Approximate monthly period
        };
    }

    private async Task<LoanInterestCalculationResult> CalculateReducingBalancePaymentAsync(Loan loan, decimal paymentAmount)
    {
        var monthlyRate = loan.InterestRate / 100 / 12;
        var interestAmount = loan.OutstandingBalance * monthlyRate;
        var principalAmount = Math.Max(0, paymentAmount - interestAmount);

        return new LoanInterestCalculationResult
        {
            InterestAmount = Math.Round(interestAmount, 2),
            PrincipalAmount = Math.Round(principalAmount, 2),
            TotalPayment = paymentAmount,
            RemainingBalance = Math.Max(0, loan.OutstandingBalance - principalAmount),
            CalculationMethod = InterestCalculationMethod.ReducingBalance,
            CalculationDate = DateTime.UtcNow,
            DaysCalculated = 30
        };
    }

    private async Task<LoanInterestCalculationResult> CalculateFlatRatePaymentAsync(Loan loan, decimal paymentAmount)
    {
        // Flat rate: Interest is calculated on original principal for entire term
        var totalInterest = loan.PrincipalAmount * (loan.InterestRate / 100) * (loan.TermInMonths / 12m);
        var monthlyInterest = totalInterest / loan.TermInMonths;
        var principalAmount = Math.Max(0, paymentAmount - monthlyInterest);

        return new LoanInterestCalculationResult
        {
            InterestAmount = Math.Round(monthlyInterest, 2),
            PrincipalAmount = Math.Round(principalAmount, 2),
            TotalPayment = paymentAmount,
            RemainingBalance = Math.Max(0, loan.OutstandingBalance - principalAmount),
            CalculationMethod = InterestCalculationMethod.FlatRate,
            CalculationDate = DateTime.UtcNow,
            DaysCalculated = 30
        };
    }

    private decimal CalculateSimpleInterestMonthlyPayment(decimal principal, decimal annualRate, int termInMonths)
    {
        var totalInterest = principal * (annualRate / 100) * (termInMonths / 12m);
        return (principal + totalInterest) / termInMonths;
    }

    private decimal CalculateCompoundMonthlyPayment(decimal principal, decimal annualRate, int termInMonths)
    {
        var monthlyRate = (double)(annualRate / 100) / 12;
        var emi = principal * (decimal)(monthlyRate * Math.Pow(1 + monthlyRate, termInMonths) / 
                  (Math.Pow(1 + monthlyRate, termInMonths) - 1));
        return Math.Round(emi, 2);
    }

    private decimal CalculateCompoundDailyPayment(decimal principal, decimal annualRate, int termInMonths)
    {
        var dailyRate = (double)(annualRate / 100) / 365;
        var totalDays = termInMonths * 30; // Approximate
        var compoundAmount = principal * (decimal)Math.Pow(1 + dailyRate, totalDays);
        return compoundAmount / termInMonths;
    }

    private decimal CalculateReducingBalanceMonthlyPayment(decimal principal, decimal annualRate, int termInMonths)
    {
        return CalculateCompoundMonthlyPayment(principal, annualRate, termInMonths);
    }

    private decimal CalculateFlatRateMonthlyPayment(decimal principal, decimal annualRate, int termInMonths)
    {
        return CalculateSimpleInterestMonthlyPayment(principal, annualRate, termInMonths);
    }

    private decimal CalculateInterestForPeriod(decimal balance, decimal annualRate, InterestCalculationMethod method)
    {
        var monthlyRate = annualRate / 100 / 12;
        return balance * monthlyRate;
    }

    #endregion
}