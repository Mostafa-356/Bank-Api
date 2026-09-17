using Bank.Application.DTOs;
using Bank.Application.Interfaces;
using Bank.Application.Helpers;
using Bank.Application.Helpers.Loan;
using Bank.Application.Helpers.Shared;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

/// <summary>
/// Service for loan analytics and reporting
/// </summary>
public class LoanAnalyticsService : ILoanAnalyticsService
{
    private readonly ILoanRepository _loanRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<LoanAnalyticsService> _logger;

    public LoanAnalyticsService(
        ILoanRepository loanRepository,
        IUserRepository userRepository,
        ILogger<LoanAnalyticsService> logger)
    {
        _loanRepository = loanRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<LoanAnalyticsDto> GetLoanAnalyticsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            var loans = await GetLoansInDateRangeAsync(fromDate, toDate);
            
            return new LoanAnalyticsDto
            {
                TotalLoans = loans.Count,
                TotalLoanAmount = loans.Sum(l => l.PrincipalAmount),
                TotalOutstandingBalance = loans.Sum(l => l.OutstandingBalance),
                AverageInterestRate = loans.Any() ? loans.Average(l => l.InterestRate) : 0,
                AverageTermMonths = loans.Any() ? (int)loans.Average(l => l.TermInMonths) : 0,
                LoansByType = loans.GroupBy(l => l.Type).ToDictionary(g => g.Key, g => g.Count()),
                LoansByStatus = loans.GroupBy(l => l.Status).ToDictionary(g => g.Key, g => g.Count()),
                LoansByCreditScore = loans.Where(l => l.CreditScoreRange.HasValue)
                    .GroupBy(l => l.CreditScoreRange!.Value)
                    .ToDictionary(g => g.Key, g => g.Count()),
                DelinquencyRate = CalculateDelinquencyRate(loans),
                DefaultRate = CalculateDefaultRate(loans),
                ReportDate = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating loan analytics");
            throw;
        }
    }

    public async Task<LoanPerformanceMetrics> GetLoanPerformanceAsync(Guid loanId)
    {
        try
        {
            var loan = await _loanRepository.GetByIdAsync(loanId);
            if (loan == null)
                throw new ArgumentException($"Loan {loanId} not found");

            var onTimePayments = loan.Payments.Count(p => p.Status == LoanPaymentStatus.Paid && p.PaymentDate <= p.DueDate);
            var latePayments = loan.Payments.Count(p => p.Status == LoanPaymentStatus.Paid && p.PaymentDate > p.DueDate);
            var totalPayments = onTimePayments + latePayments;
            
            return new LoanPerformanceMetrics
            {
                LoanId = loanId,
                LoanNumber = loan.LoanNumber,
                PaymentToIncomeRatio = 0, // Would need customer income data
                DebtToIncomeRatio = 0, // Would need customer debt data
                PaymentHistory = onTimePayments,
                MissedPayments = latePayments,
                TotalInterestPaid = loan.TotalInterestPaid,
                PercentagePaid = loan.PrincipalAmount > 0 ? (loan.TotalPrincipalPaid / loan.PrincipalAmount) * 100 : 0,
                LastPaymentDate = loan.LastPaymentDate ?? DateTime.MinValue,
                RiskLevel = CalculateRiskLevel(loan, onTimePayments, totalPayments)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting loan performance for loan {LoanId}", loanId);
            throw;
        }
    }

    public async Task<Dictionary<LoanType, LoanAnalyticsDto>> GetPortfolioByTypeAsync()
    {
        try
        {
            var allLoans = await _loanRepository.SearchAsync();
            var result = new Dictionary<LoanType, LoanAnalyticsDto>();

            foreach (var loanType in Enum.GetValues<LoanType>())
            {
                var loansOfType = allLoans.Where(l => l.Type == loanType).ToList();
                if (loansOfType.Any())
                {
                    result[loanType] = new LoanAnalyticsDto
                    {
                        TotalLoans = loansOfType.Count,
                        TotalLoanAmount = loansOfType.Sum(l => l.PrincipalAmount),
                        TotalOutstandingBalance = loansOfType.Sum(l => l.OutstandingBalance),
                        AverageInterestRate = loansOfType.Average(l => l.InterestRate),
                        AverageTermMonths = (int)loansOfType.Average(l => l.TermInMonths),
                        LoansByStatus = loansOfType.GroupBy(l => l.Status).ToDictionary(g => g.Key, g => g.Count()),
                        DelinquencyRate = CalculateDelinquencyRate(loansOfType),
                        DefaultRate = CalculateDefaultRate(loansOfType),
                        ReportDate = DateTime.UtcNow
                    };
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting portfolio by type");
            throw;
        }
    }

    public async Task<List<LoanDto>> GetDelinquencyReportAsync()
    {
        try
        {
            var delinquentLoans = await _loanRepository.GetByStatusAsync(LoanStatus.Delinquent);
            return delinquentLoans.Select(MapToLoanDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating delinquency report");
            throw;
        }
    }

    public async Task<List<LoanDto>> GetLoansApproachingMaturityAsync(int daysAhead = 30)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(daysAhead);
            var allLoans = await _loanRepository.SearchAsync();
            
            var approachingMaturity = allLoans
                .Where(l => l.MaturityDate.HasValue && 
                           l.MaturityDate.Value <= cutoffDate && 
                           l.Status == LoanStatus.Active)
                .ToList();

            return approachingMaturity.Select(MapToLoanDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting loans approaching maturity");
            throw;
        }
    }

    public async Task<PortfolioRiskMetrics> CalculatePortfolioRiskAsync()
    {
        try
        {
            var allLoans = await _loanRepository.SearchAsync();
            var activeLoans = allLoans.Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Disbursed).ToList();

            var totalExposure = activeLoans.Sum(l => l.OutstandingBalance);
            var riskByType = new Dictionary<LoanType, decimal>();
            var riskByScore = new Dictionary<CreditScoreRange, decimal>();

            foreach (var loanType in Enum.GetValues<LoanType>())
            {
                var loansOfType = activeLoans.Where(l => l.Type == loanType);
                riskByType[loanType] = loansOfType.Sum(l => l.OutstandingBalance * GetRiskWeight(l));
            }

            foreach (var scoreRange in Enum.GetValues<CreditScoreRange>())
            {
                var loansInRange = activeLoans.Where(l => l.CreditScoreRange == scoreRange);
                riskByScore[scoreRange] = loansInRange.Sum(l => l.OutstandingBalance * GetRiskWeight(l));
            }

            return new PortfolioRiskMetrics
            {
                TotalExposure = totalExposure,
                WeightedAverageRisk = activeLoans.Any() ? activeLoans.Average(l => GetRiskWeight(l)) : 0,
                ConcentrationRisk = CalculateConcentrationRisk(activeLoans),
                RiskByType = riskByType,
                RiskByScore = riskByScore,
                VaR95 = CalculateVaR95(activeLoans),
                ExpectedLoss = CalculateExpectedLoss(activeLoans),
                CalculationDate = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating portfolio risk");
            throw;
        }
    }

    public async Task<List<LoanOriginationTrend>> GetOriginationTrendsAsync(int months = 12)
    {
        try
        {
            var startDate = DateTime.UtcNow.AddMonths(-months);
            var loans = await GetLoansInDateRangeAsync(startDate, DateTime.UtcNow);

            var trends = loans
                .GroupBy(l => new { l.ApplicationDate.Year, l.ApplicationDate.Month })
                .Select(g => new LoanOriginationTrend
                {
                    Month = new DateTime(g.Key.Year, g.Key.Month, 1),
                    LoansOriginated = g.Count(),
                    TotalAmount = g.Sum(l => l.PrincipalAmount),
                    AverageAmount = g.Average(l => l.PrincipalAmount),
                    AverageInterestRate = g.Average(l => l.InterestRate),
                    OriginationsByType = g.GroupBy(l => l.Type).ToDictionary(t => t.Key, t => t.Count())
                })
                .OrderBy(t => t.Month)
                .ToList();

            return trends;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting origination trends");
            throw;
        }
    }

    public async Task<CustomerLoanSummary> GetCustomerLoanSummaryAsync(Guid customerId)
    {
        try
        {
            var customerLoans = await _loanRepository.GetByCustomerIdAsync(customerId);
            var activeLoans = customerLoans.Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Disbursed).ToList();

            var onTimePayments = customerLoans.SelectMany(l => l.Payments)
                .Count(p => p.Status == LoanPaymentStatus.Paid && p.PaymentDate <= p.DueDate);
            var latePayments = customerLoans.SelectMany(l => l.Payments)
                .Count(p => p.Status == LoanPaymentStatus.Paid && p.PaymentDate > p.DueDate);
            var totalPayments = onTimePayments + latePayments;

            return new CustomerLoanSummary
            {
                CustomerId = customerId,
                TotalLoans = customerLoans.Count,
                ActiveLoans = activeLoans.Count,
                TotalBorrowed = customerLoans.Sum(l => l.PrincipalAmount),
                TotalOutstanding = activeLoans.Sum(l => l.OutstandingBalance),
                TotalPaid = customerLoans.Sum(l => l.TotalPrincipalPaid + l.TotalInterestPaid),
                WeightedAverageRate = activeLoans.Any() ? 
                    activeLoans.Sum(l => l.InterestRate * l.OutstandingBalance) / activeLoans.Sum(l => l.OutstandingBalance) : 0,
                OnTimePayments = onTimePayments,
                LatePayments = latePayments,
                PaymentReliabilityScore = totalPayments > 0 ? (decimal)onTimePayments / totalPayments * 100 : 100,
                RiskLevel = CalculateCustomerRiskLevel(customerLoans, onTimePayments, totalPayments),
                Loans = customerLoans.Select(MapToLoanDto).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer loan summary for customer {CustomerId}", customerId);
            throw;
        }
    }

    #region Private Helper Methods

    private async Task<List<Loan>> GetLoansInDateRangeAsync(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? DateTime.UtcNow.AddYears(-1);
        var to = toDate ?? DateTime.UtcNow;
        
        return await _loanRepository.SearchAsync(
            applicationDateFrom: from,
            applicationDateTo: to,
            take: int.MaxValue);
    }

    private static decimal CalculateDelinquencyRate(List<Loan> loans)
    {
        var delinquentCount = loans.Count(l => l.Status == LoanStatus.Delinquent);
        return Bank.Application.Helpers.Loan.LoanCalculationHelper.CalculateDelinquencyRate(delinquentCount, loans.Count);
    }

    private static decimal CalculateDefaultRate(List<Loan> loans)
    {
        var defaultCount = loans.Count(l => l.Status == LoanStatus.DefaultStatus);
        return Bank.Application.Helpers.Loan.LoanCalculationHelper.CalculateDefaultRate(defaultCount, loans.Count);
    }

    private static LoanRiskLevel CalculateRiskLevel(Loan loan, int onTimePayments, int totalPayments)
    {
        var paymentRatio = totalPayments > 0 ? (decimal)onTimePayments / totalPayments : 1;
        var daysOverdue = loan.DaysOverdue;

        return (paymentRatio, daysOverdue) switch
        {
            ( >= 0.95m, <= 0) => LoanRiskLevel.Low,
            ( >= 0.85m, <= 30) => LoanRiskLevel.Medium,
            ( >= 0.70m, <= 60) => LoanRiskLevel.High,
            _ => LoanRiskLevel.Critical
        };
    }

    private LoanRiskLevel CalculateCustomerRiskLevel(List<Loan> loans, int onTimePayments, int totalPayments)
    {
        var paymentRatio = totalPayments > 0 ? (decimal)onTimePayments / totalPayments : 1;
        var hasDelinquentLoans = loans.Any(l => l.Status == LoanStatus.Delinquent);
        var hasDefaultLoans = loans.Any(l => l.Status == LoanStatus.DefaultStatus);

        if (hasDefaultLoans) return LoanRiskLevel.Critical;
        if (hasDelinquentLoans) return LoanRiskLevel.High;
        
        return paymentRatio switch
        {
            >= 0.95m => LoanRiskLevel.Low,
            >= 0.85m => LoanRiskLevel.Medium,
            >= 0.70m => LoanRiskLevel.High,
            _ => LoanRiskLevel.Critical
        };
    }

    private decimal GetRiskWeight(Loan loan)
    {
        return loan.CreditScoreRange switch
        {
            CreditScoreRange.Excellent => 0.02m,
            CreditScoreRange.VeryGood => 0.05m,
            CreditScoreRange.Good => 0.10m,
            CreditScoreRange.Fair => 0.20m,
            CreditScoreRange.Poor => 0.35m,
            _ => 0.15m
        };
    }

    private decimal CalculateConcentrationRisk(List<Loan> loans)
    {
        if (!loans.Any()) return 0;
        
        var totalExposure = loans.Sum(l => l.OutstandingBalance);
        var typeConcentrations = loans.GroupBy(l => l.Type)
            .Select(g => g.Sum(l => l.OutstandingBalance) / totalExposure)
            .ToList();
        
        // Herfindahl-Hirschman Index for concentration
        return typeConcentrations.Sum(c => c * c) * 100;
    }

    private decimal CalculateVaR95(List<Loan> loans)
    {
        // Simplified VaR calculation - 95th percentile of potential losses
        var totalExposure = loans.Sum(l => l.OutstandingBalance);
        var averageRisk = loans.Average(l => GetRiskWeight(l));
        return totalExposure * averageRisk * 1.645m; // 95% confidence interval
    }

    private decimal CalculateExpectedLoss(List<Loan> loans)
    {
        return loans.Sum(l => l.OutstandingBalance * GetRiskWeight(l));
    }

    private static LoanDto MapToLoanDto(Loan loan)
    {
        return new LoanDto
        {
            Id = loan.Id,
            LoanNumber = loan.LoanNumber,
            Type = loan.Type,
            TypeName = loan.Type.ToString(),
            PrincipalAmount = loan.PrincipalAmount,
            InterestRate = loan.InterestRate,
            TermInMonths = loan.TermInMonths,
            Status = loan.Status,
            StatusName = loan.Status.ToString(),
            ApplicationDate = loan.ApplicationDate,
            ApprovalDate = loan.ApprovalDate,
            DisbursementDate = loan.DisbursementDate,
            MaturityDate = loan.MaturityDate,
            OutstandingBalance = loan.OutstandingBalance,
            MonthlyPaymentAmount = loan.MonthlyPaymentAmount,
            NextPaymentDueDate = loan.NextPaymentDueDate,
            DaysOverdue = loan.DaysOverdue,
            TotalInterestPaid = loan.TotalInterestPaid,
            TotalPrincipalPaid = loan.TotalPrincipalPaid,
            Purpose = loan.Purpose,
            CreditScore = loan.CreditScore,
            CreditScoreRange = loan.CreditScoreRange
        };
    }

    #endregion
}