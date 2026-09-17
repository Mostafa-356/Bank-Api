using Bank.Application.DTOs;
using Bank.Application.DTOs.Transaction.Core;
using Bank.Application.DTOs.Account.Transfer;
using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

/// <summary>
/// Service for checking transfer eligibility and limits for beneficiaries
/// </summary>
public class TransferEligibilityService : ITransferEligibilityService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBeneficiaryService _beneficiaryService;
    private readonly IFraudDetectionService _fraudDetectionService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<TransferEligibilityService> _logger;

    public TransferEligibilityService(
        IUnitOfWork unitOfWork,
        IBeneficiaryService beneficiaryService,
        IFraudDetectionService fraudDetectionService,
        IAuditLogService auditLogService,
        ILogger<TransferEligibilityService> logger)
    {
        _unitOfWork = unitOfWork;
        _beneficiaryService = beneficiaryService;
        _fraudDetectionService = fraudDetectionService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<TransferEligibilityResult> CheckTransferEligibilityAsync(TransferEligibilityRequest request)
    {
        try
        {
            var result = new TransferEligibilityResult();
            var issues = new List<string>();

            // Get beneficiary details
            var beneficiary = await _unitOfWork.Repository<Beneficiary>().GetByIdAsync(request.BeneficiaryId);
            if (beneficiary == null)
            {
                result.IsEligible = false;
                result.EligibilityIssues.Add("Beneficiary not found");
                return result;
            }

            // Check beneficiary status
            result.BeneficiaryActive = beneficiary.IsActive;
            result.BeneficiaryVerified = beneficiary.IsVerified;

            if (!beneficiary.IsActive)
                issues.Add("Beneficiary is inactive");

            if (!beneficiary.IsVerified)
                issues.Add("Beneficiary is not verified");

            if (beneficiary.Status != BeneficiaryStatus.Active)
                issues.Add($"Beneficiary status is {beneficiary.Status}");

            // Check transfer limits
            var dailyCheck = await CheckDailyLimitsAsync(request.BeneficiaryId, request.Amount, request.ProposedTransferDate);
            var monthlyCheck = await CheckMonthlyLimitsAsync(request.BeneficiaryId, request.Amount, request.ProposedTransferDate);
            var singleCheck = CheckSingleTransferLimit(beneficiary, request.Amount);

            result.WithinTransferLimits = dailyCheck.WithinLimit && monthlyCheck.WithinLimit && singleCheck.WithinLimit;
            result.WithinDailyLimits = dailyCheck.WithinLimit;
            result.WithinMonthlyLimits = monthlyCheck.WithinLimit;
            result.RemainingDailyLimit = dailyCheck.RemainingLimit;
            result.RemainingMonthlyLimit = monthlyCheck.RemainingLimit;

            if (!dailyCheck.WithinLimit)
                issues.AddRange(dailyCheck.LimitIssues);

            if (!monthlyCheck.WithinLimit)
                issues.AddRange(monthlyCheck.LimitIssues);

            if (!singleCheck.WithinLimit)
                issues.AddRange(singleCheck.LimitIssues);

            // Overall eligibility
            result.IsEligible = result.BeneficiaryActive && 
                               result.BeneficiaryVerified && 
                               result.WithinTransferLimits;

            result.EligibilityIssues = issues;

            // Provide recommendation
            if (!result.IsEligible)
            {
                if (!result.BeneficiaryVerified)
                    result.RecommendedAction = "Verify beneficiary before attempting transfer";
                else if (!result.WithinTransferLimits)
                    result.RecommendedAction = "Reduce transfer amount or wait for limit reset";
                else if (!result.BeneficiaryActive)
                    result.RecommendedAction = "Reactivate beneficiary before attempting transfer";
            }

            await _auditLogService.LogAsync("Transfer Eligibility Check", 
                $"Checked eligibility for beneficiary {request.BeneficiaryId}, amount {request.Amount:C}. Eligible: {result.IsEligible}");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking transfer eligibility for beneficiary {BeneficiaryId}", request.BeneficiaryId);
            return new TransferEligibilityResult
            {
                IsEligible = false,
                EligibilityIssues = new List<string> { "Transfer eligibility check temporarily unavailable" }
            };
        }
    }

    public async Task<BeneficiaryLimitsResult> GetRemainingLimitsAsync(Guid beneficiaryId, DateTime? forDate = null)
    {
        try
        {
            var checkDate = forDate ?? DateTime.UtcNow;
            var beneficiary = await _unitOfWork.Repository<Beneficiary>().GetByIdAsync(beneficiaryId);
            
            if (beneficiary == null)
            {
                return new BeneficiaryLimitsResult { BeneficiaryId = beneficiaryId };
            }

            var result = new BeneficiaryLimitsResult
            {
                BeneficiaryId = beneficiaryId,
                DailyLimit = beneficiary.DailyTransferLimit,
                MonthlyLimit = beneficiary.MonthlyTransferLimit,
                SingleTransferLimit = beneficiary.SingleTransferLimit,
                CalculationDate = checkDate
            };

            // Calculate daily usage
            var dailyStart = checkDate.Date;
            var dailyEnd = dailyStart.AddDays(1);
            var dailyTransactions = await GetTransactionsInPeriod(beneficiaryId, dailyStart, dailyEnd);
            result.DailyUsed = dailyTransactions.Sum(t => t.Amount);

            // Calculate monthly usage
            var monthlyStart = new DateTime(checkDate.Year, checkDate.Month, 1);
            var monthlyEnd = monthlyStart.AddMonths(1);
            var monthlyTransactions = await GetTransactionsInPeriod(beneficiaryId, monthlyStart, monthlyEnd);
            result.MonthlyUsed = monthlyTransactions.Sum(t => t.Amount);

            // Calculate remaining limits
            result.RemainingDaily = result.DailyLimit.HasValue ? 
                Math.Max(0, result.DailyLimit.Value - result.DailyUsed) : null;
            
            result.RemainingMonthly = result.MonthlyLimit.HasValue ? 
                Math.Max(0, result.MonthlyLimit.Value - result.MonthlyUsed) : null;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting remaining limits for beneficiary {BeneficiaryId}", beneficiaryId);
            return new BeneficiaryLimitsResult { BeneficiaryId = beneficiaryId };
        }
    }

    public async Task<LimitCheckResult> CheckDailyLimitsAsync(Guid beneficiaryId, decimal amount, DateTime? transferDate = null)
    {
        try
        {
            var checkDate = transferDate ?? DateTime.UtcNow;
            var beneficiary = await _unitOfWork.Repository<Beneficiary>().GetByIdAsync(beneficiaryId);
            
            var result = new LimitCheckResult
            {
                LimitType = "Daily",
                RequestedAmount = amount
            };

            if (beneficiary?.DailyTransferLimit == null)
            {
                result.WithinLimit = true; // No limit set
                return result;
            }

            result.Limit = beneficiary.DailyTransferLimit.Value;

            // Get daily usage
            var dailyStart = checkDate.Date;
            var dailyEnd = dailyStart.AddDays(1);
            var dailyTransactions = await GetTransactionsInPeriod(beneficiaryId, dailyStart, dailyEnd);
            result.CurrentUsage = dailyTransactions.Sum(t => t.Amount);

            var totalWithNewTransfer = result.CurrentUsage + amount;
            result.WithinLimit = totalWithNewTransfer <= result.Limit;
            result.RemainingLimit = Math.Max(0, result.Limit.Value - result.CurrentUsage);

            if (!result.WithinLimit)
            {
                result.LimitIssues.Add($"Daily limit exceeded. Limit: {result.Limit:C}, Current usage: {result.CurrentUsage:C}, Requested: {amount:C}");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking daily limits for beneficiary {BeneficiaryId}", beneficiaryId);
            return new LimitCheckResult
            {
                WithinLimit = false,
                LimitType = "Daily",
                RequestedAmount = amount,
                LimitIssues = new List<string> { "Daily limit check temporarily unavailable" }
            };
        }
    }

    public async Task<LimitCheckResult> CheckMonthlyLimitsAsync(Guid beneficiaryId, decimal amount, DateTime? transferDate = null)
    {
        try
        {
            var checkDate = transferDate ?? DateTime.UtcNow;
            var beneficiary = await _unitOfWork.Repository<Beneficiary>().GetByIdAsync(beneficiaryId);
            
            var result = new LimitCheckResult
            {
                LimitType = "Monthly",
                RequestedAmount = amount
            };

            if (beneficiary?.MonthlyTransferLimit == null)
            {
                result.WithinLimit = true; // No limit set
                return result;
            }

            result.Limit = beneficiary.MonthlyTransferLimit.Value;

            // Get monthly usage
            var monthlyStart = new DateTime(checkDate.Year, checkDate.Month, 1);
            var monthlyEnd = monthlyStart.AddMonths(1);
            var monthlyTransactions = await GetTransactionsInPeriod(beneficiaryId, monthlyStart, monthlyEnd);
            result.CurrentUsage = monthlyTransactions.Sum(t => t.Amount);

            var totalWithNewTransfer = result.CurrentUsage + amount;
            result.WithinLimit = totalWithNewTransfer <= result.Limit;
            result.RemainingLimit = Math.Max(0, result.Limit.Value - result.CurrentUsage);

            if (!result.WithinLimit)
            {
                result.LimitIssues.Add($"Monthly limit exceeded. Limit: {result.Limit:C}, Current usage: {result.CurrentUsage:C}, Requested: {amount:C}");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking monthly limits for beneficiary {BeneficiaryId}", beneficiaryId);
            return new LimitCheckResult
            {
                WithinLimit = false,
                LimitType = "Monthly",
                RequestedAmount = amount,
                LimitIssues = new List<string> { "Monthly limit check temporarily unavailable" }
            };
        }
    }

    public async Task<TransferHistorySummary> GetTransferHistorySummaryAsync(Guid beneficiaryId, DateTime fromDate, DateTime toDate)
    {
        try
        {
            var transactions = await GetTransactionsInPeriod(beneficiaryId, fromDate, toDate);
            
            var summary = new TransferHistorySummary
            {
                BeneficiaryId = beneficiaryId,
                FromDate = fromDate,
                ToDate = toDate,
                TransactionCount = transactions.Count,
                TotalAmount = transactions.Sum(t => t.Amount),
                LargestTransfer = transactions.Any() ? transactions.Max(t => t.Amount) : 0,
                LastTransferDate = transactions.Any() ? transactions.Max(t => t.CreatedAt) : null
            };

            // Group by day for daily summaries
            summary.DailySummaries = transactions
                .GroupBy(t => t.CreatedAt.Date)
                .Select(g => new DailyTransferSummary
                {
                    Date = g.Key,
                    TransactionCount = g.Count(),
                    TotalAmount = g.Sum(t => t.Amount)
                })
                .OrderBy(d => d.Date)
                .ToList();

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transfer history summary for beneficiary {BeneficiaryId}", beneficiaryId);
            return new TransferHistorySummary
            {
                BeneficiaryId = beneficiaryId,
                FromDate = fromDate,
                ToDate = toDate
            };
        }
    }

    public async Task<TransferPreValidationResult> PreValidateTransferAsync(TransferPreValidationRequest request)
    {
        try
        {
            var result = new TransferPreValidationResult
            {
                ValidationDate = DateTime.UtcNow
            };

            var errors = new List<string>();
            var warnings = new List<string>();

            // Check account balance if requested
            if (request.CheckAccountBalance)
            {
                var account = await _unitOfWork.Repository<Account>().GetByIdAsync(request.FromAccountId);
                if (account == null)
                {
                    errors.Add("Source account not found");
                }
                else
                {
                    result.AvailableBalance = account.Balance;
                    result.SufficientBalance = account.Balance >= request.Amount;
                    
                    if (!result.SufficientBalance)
                        errors.Add($"Insufficient balance. Available: {account.Balance:C}, Required: {request.Amount:C}");
                }
            }

            // Check beneficiary eligibility if requested
            if (request.CheckBeneficiaryStatus)
            {
                var eligibilityRequest = new TransferEligibilityRequest
                {
                    BeneficiaryId = request.BeneficiaryId,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    ProposedTransferDate = request.ProposedTransferDate
                };

                result.EligibilityResult = await CheckTransferEligibilityAsync(eligibilityRequest);
                result.BeneficiaryEligible = result.EligibilityResult.IsEligible;

                if (!result.BeneficiaryEligible)
                    errors.AddRange(result.EligibilityResult.EligibilityIssues);
            }

            // Check transfer limits if requested
            if (request.CheckTransferLimits)
            {
                result.LimitsResult = await GetRemainingLimitsAsync(request.BeneficiaryId, request.ProposedTransferDate);
                
                var dailyCheck = await CheckDailyLimitsAsync(request.BeneficiaryId, request.Amount, request.ProposedTransferDate);
                var monthlyCheck = await CheckMonthlyLimitsAsync(request.BeneficiaryId, request.Amount, request.ProposedTransferDate);

                result.WithinLimits = dailyCheck.WithinLimit && monthlyCheck.WithinLimit;

                if (!result.WithinLimits)
                {
                    errors.AddRange(dailyCheck.LimitIssues);
                    errors.AddRange(monthlyCheck.LimitIssues);
                }
            }

            // Check fraud rules if requested
            if (request.CheckFraudRules)
            {
                // This would integrate with the fraud detection service
                // For now, simulate basic fraud checks
                result.PassesFraudCheck = await SimulateFraudCheck(request);
                
                if (!result.PassesFraudCheck)
                    errors.Add("Transfer flagged by fraud detection system");
            }

            // Set overall validation result
            result.IsValid = errors.Count == 0;
            result.ValidationErrors = errors;
            result.Warnings = warnings;

            // Provide recommendations
            if (!result.IsValid)
            {
                if (!result.SufficientBalance)
                    result.RecommendedAction = "Add funds to account before attempting transfer";
                else if (!result.BeneficiaryEligible)
                    result.RecommendedAction = result.EligibilityResult?.RecommendedAction ?? "Resolve beneficiary issues";
                else if (!result.WithinLimits)
                    result.RecommendedAction = "Reduce transfer amount or wait for limit reset";
                else if (!result.PassesFraudCheck)
                    result.RecommendedAction = "Contact customer service for manual review";
            }

            await _auditLogService.LogAsync("Transfer Pre-Validation", 
                $"Pre-validated transfer from {request.FromAccountId} to beneficiary {request.BeneficiaryId}, amount {request.Amount:C}. Valid: {result.IsValid}");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pre-validating transfer");
            return new TransferPreValidationResult
            {
                IsValid = false,
                ValidationErrors = new List<string> { "Transfer pre-validation service temporarily unavailable" }
            };
        }
    }

    #region Private Helper Methods

    private LimitCheckResult CheckSingleTransferLimit(Beneficiary beneficiary, decimal amount)
    {
        var result = new LimitCheckResult
        {
            LimitType = "Single",
            RequestedAmount = amount
        };

        if (beneficiary.SingleTransferLimit == null)
        {
            result.WithinLimit = true; // No limit set
            return result;
        }

        result.Limit = beneficiary.SingleTransferLimit.Value;
        result.WithinLimit = amount <= result.Limit;

        if (!result.WithinLimit)
        {
            result.LimitIssues.Add($"Single transfer limit exceeded. Limit: {result.Limit:C}, Requested: {amount:C}");
        }

        return result;
    }

    private async Task<List<Transaction>> GetTransactionsInPeriod(Guid beneficiaryId, DateTime fromDate, DateTime toDate)
    {
        // This is a simplified approach - in a real system, you'd need to properly link transactions to beneficiaries
        // For now, we'll simulate this by checking transaction descriptions or references
        var transactions = await _unitOfWork.Repository<Transaction>()
            .FindAsync(t => t.CreatedAt >= fromDate && 
                           t.CreatedAt < toDate && 
                           t.Status == TransactionStatus.Completed);

        // Filter transactions that are related to this beneficiary
        // In a real system, you'd have a proper foreign key relationship
        return transactions.Where(t => t.Description?.Contains(beneficiaryId.ToString()) == true).ToList();
    }

    private async Task<bool> SimulateFraudCheck(TransferPreValidationRequest request)
    {
        await Task.Delay(50); // Simulate fraud check delay

        // Simple fraud rules simulation
        if (request.Amount > 10000) // Large amount flag
            return false;

        if (request.ProposedTransferDate.Hour < 6 || request.ProposedTransferDate.Hour > 22) // Off-hours flag
            return false;

        return true; // Pass fraud check
    }

    #endregion
}