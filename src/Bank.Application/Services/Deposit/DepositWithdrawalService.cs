using Bank.Application.DTOs;
using Bank.Application.Helpers.Shared;
using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

/// <summary>
/// Specialized service for handling deposit withdrawals and penalty calculations
/// </summary>
public class DepositWithdrawalService : IDepositWithdrawalService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<DepositWithdrawalService> _logger;

    public DepositWithdrawalService(
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService,
        ILogger<DepositWithdrawalService> logger)
    {
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <summary>
    /// Calculate detailed penalty information for early withdrawal
    /// </summary>
    public async Task<DetailedWithdrawalCalculation> CalculateDetailedWithdrawalAsync(Guid depositId, decimal withdrawalAmount)
    {
        var deposit = await _unitOfWork.Repository<FixedDeposit>()
            .GetByIdAsync(depositId);
        
        if (deposit == null)
            throw new InvalidOperationException($"Fixed deposit {depositId} not found");

        var calculation = new DetailedWithdrawalCalculation
        {
            DepositId = depositId,
            DepositNumber = deposit.DepositNumber,
            RequestedAmount = withdrawalAmount,
            PrincipalAmount = deposit.PrincipalAmount,
            AccruedInterest = deposit.AccruedInterest,
            AvailableBalance = deposit.PrincipalAmount + deposit.AccruedInterest,
            MaturityDate = deposit.MaturityDate,
            DaysToMaturity = Math.Max(0, (deposit.MaturityDate - DateTime.UtcNow).Days),
            IsEarlyWithdrawal = DateTime.UtcNow < deposit.MaturityDate
        };

        // Calculate penalties
        if (calculation.IsEarlyWithdrawal)
        {
            calculation.PenaltyDetails = CalculatePenaltyDetails(deposit, withdrawalAmount);
            calculation.TotalPenalty = calculation.PenaltyDetails.Sum(p => p.Amount);
        }

        calculation.NetAmount = withdrawalAmount - calculation.TotalPenalty;
        calculation.RemainingBalance = calculation.AvailableBalance - withdrawalAmount;

        // Validate withdrawal
        calculation.ValidationResults = ValidateWithdrawal(deposit, calculation);

        return calculation;
    }

    /// <summary>
    /// Process early withdrawal with detailed penalty tracking
    /// </summary>
    public async Task<WithdrawalResult> ProcessEarlyWithdrawalWithDetailsAsync(
        Guid depositId, 
        EarlyWithdrawalRequest request, 
        Guid processedByUserId)
    {
        var result = new WithdrawalResult { DepositId = depositId };
        
        try
        {
            var calculation = await CalculateDetailedWithdrawalAsync(depositId, request.WithdrawalAmount);
            
            if (!calculation.ValidationResults.IsValid)
            {
                result.Success = false;
                result.Errors = calculation.ValidationResults.Errors;
                return result;
            }

            if (!request.AcknowledgePenalty && calculation.TotalPenalty > 0)
            {
                result.Success = false;
                result.Errors.Add("Customer must acknowledge penalty for early withdrawal");
                return result;
            }

            var deposit = await _unitOfWork.Repository<FixedDeposit>()
                .GetByIdAsync(depositId);

            // Process the withdrawal
            await ProcessWithdrawalTransactionsAsync(deposit!, calculation, request, processedByUserId);
            
            result.Success = true;
            result.NetAmountPaid = calculation.NetAmount;
            result.PenaltyApplied = calculation.TotalPenalty;
            result.TransactionReference = $"EW{DateTime.UtcNow:yyyyMMddHHmmss}";

            await _auditLogService.LogUserActionAsync(
                processedByUserId,
                "FixedDeposit",
                "EarlyWithdrawal",
                depositId.ToString(),
                $"Processed early withdrawal of {request.WithdrawalAmount:C} from deposit {deposit!.DepositNumber}, penalty {calculation.TotalPenalty:C}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing early withdrawal for deposit {DepositId}", depositId);
            result.Success = false;
            result.Errors.Add($"Processing error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Calculate penalty-free withdrawal periods
    /// </summary>
    public async Task<PenaltyFreePeriodsDto> GetPenaltyFreePeriodsAsync(Guid depositId)
    {
        var deposit = await _unitOfWork.Repository<FixedDeposit>()
            .GetByIdAsync(depositId);
        
        if (deposit == null)
            throw new InvalidOperationException($"Fixed deposit {depositId} not found");

        var periods = new PenaltyFreePeriodsDto
        {
            DepositId = depositId,
            HasPenaltyFreePeriods = deposit.DepositProduct.PenaltyFreeDays.HasValue && deposit.DepositProduct.PenaltyFreeDays > 0
        };

        if (periods.HasPenaltyFreePeriods)
        {
            var penaltyFreeDays = deposit.DepositProduct.PenaltyFreeDays!.Value;
            periods.PenaltyFreeDays = penaltyFreeDays;
            periods.PenaltyFreeUntil = deposit.StartDate.AddDays(penaltyFreeDays);
            periods.IsCurrentlyPenaltyFree = DateTime.UtcNow <= periods.PenaltyFreeUntil;
            
            if (!periods.IsCurrentlyPenaltyFree)
            {
                periods.DaysUntilPenaltyFree = null; // No future penalty-free period
            }
        }

        return periods;
    }

    /// <summary>
    /// Get withdrawal history for a deposit
    /// </summary>
    public async Task<IEnumerable<WithdrawalHistoryDto>> GetWithdrawalHistoryAsync(Guid depositId)
    {
        var transactions = await _unitOfWork.Repository<DepositTransaction>()
            .FindAsync(t => t.FixedDepositId == depositId && 
                           (t.TransactionType == DepositTransactionType.EarlyWithdrawal ||
                            t.TransactionType == DepositTransactionType.PartialWithdrawal ||
                            t.TransactionType == DepositTransactionType.MaturityPayout));

        return transactions.OrderByDescending(t => t.TransactionDate).Select(t => new WithdrawalHistoryDto
        {
            TransactionId = t.Id,
            TransactionReference = t.TransactionReference,
            WithdrawalType = t.TransactionType,
            Amount = t.Amount,
            PenaltyApplied = t.PenaltyType.HasValue ? GetPenaltyAmount(t) : 0,
            TransactionDate = t.TransactionDate,
            Reason = t.Description,
            ProcessedBy = t.ProcessedByUserId
        });
    }

    private List<PenaltyDetail> CalculatePenaltyDetails(FixedDeposit deposit, decimal withdrawalAmount)
    {
        var penalties = new List<PenaltyDetail>();
        
        switch (deposit.PenaltyType)
        {
            case WithdrawalPenaltyType.None:
                break;
                
            case WithdrawalPenaltyType.FixedAmount:
                if (deposit.PenaltyAmount.HasValue)
                {
                    penalties.Add(new PenaltyDetail
                    {
                        Type = "Fixed Amount Penalty",
                        Amount = deposit.PenaltyAmount.Value,
                        Description = $"Fixed penalty of {deposit.PenaltyAmount.Value:C} for early withdrawal"
                    });
                }
                break;
                
            case WithdrawalPenaltyType.Percentage:
                if (deposit.PenaltyPercentage.HasValue)
                {
                    var penaltyAmount = withdrawalAmount * deposit.PenaltyPercentage.Value / 100;
                    penalties.Add(new PenaltyDetail
                    {
                        Type = "Percentage Penalty",
                        Amount = penaltyAmount,
                        Description = $"{deposit.PenaltyPercentage.Value}% penalty on withdrawal amount ({penaltyAmount:C})"
                    });
                }
                break;
                
            case WithdrawalPenaltyType.InterestForfeiture:
                penalties.Add(new PenaltyDetail
                {
                    Type = "Interest Forfeiture",
                    Amount = deposit.AccruedInterest,
                    Description = $"Forfeiture of accrued interest ({deposit.AccruedInterest:C})"
                });
                break;
                
            case WithdrawalPenaltyType.Combined:
                if (deposit.PenaltyAmount.HasValue)
                {
                    penalties.Add(new PenaltyDetail
                    {
                        Type = "Fixed Amount Component",
                        Amount = deposit.PenaltyAmount.Value,
                        Description = $"Fixed penalty component: {deposit.PenaltyAmount.Value:C}"
                    });
                }
                if (deposit.PenaltyPercentage.HasValue)
                {
                    var percentagePenalty = withdrawalAmount * deposit.PenaltyPercentage.Value / 100;
                    penalties.Add(new PenaltyDetail
                    {
                        Type = "Percentage Component",
                        Amount = percentagePenalty,
                        Description = $"Percentage penalty component: {deposit.PenaltyPercentage.Value}% ({percentagePenalty:C})"
                    });
                }
                break;
        }

        return penalties;
    }

    private static WithdrawalValidationResult ValidateWithdrawal(FixedDeposit deposit, DetailedWithdrawalCalculation calculation)
    {
        var result = new WithdrawalValidationResult();
        
        if (deposit.Status != FixedDepositStatus.Active)
        {
            result.Errors.Add("Deposit is not active");
        }
        
        if (calculation.RequestedAmount <= 0)
        {
            result.Errors.Add("Withdrawal amount must be greater than zero");
        }
        
        if (calculation.RequestedAmount > calculation.AvailableBalance)
        {
            result.Errors.Add($"Withdrawal amount ({calculation.RequestedAmount:C}) exceeds available balance ({calculation.AvailableBalance:C})");
        }
        
        if (calculation.NetAmount < 0)
        {
            result.Errors.Add("Net amount after penalties would be negative");
        }

        result.IsValid = !result.Errors.Any();
        return result;
    }

    private async Task ProcessWithdrawalTransactionsAsync(
        FixedDeposit deposit, 
        DetailedWithdrawalCalculation calculation, 
        EarlyWithdrawalRequest request, 
        Guid processedByUserId)
    {
        // Update deposit status
        deposit.Status = FixedDepositStatus.Closed;
        deposit.ClosureDate = DateTime.UtcNow;
        deposit.ClosedByUserId = processedByUserId;
        deposit.ClosureReason = $"Early withdrawal: {request.Reason}";
        deposit.PenaltyApplied = calculation.TotalPenalty;
        deposit.NetAmountPaid = calculation.NetAmount;

        // Credit the linked account with net amount
        deposit.LinkedAccount.Balance += calculation.NetAmount;

        // Create withdrawal transaction
        var withdrawalTransaction = new DepositTransaction
        {
            FixedDepositId = deposit.Id,
            TransactionType = DepositTransactionType.EarlyWithdrawal,
            Amount = calculation.RequestedAmount,
            Description = $"Early withdrawal from deposit {deposit.DepositNumber}: {request.Reason}",
            TransactionDate = DateTime.UtcNow,
            Status = TransactionStatus.Completed,
            ProcessedByUserId = processedByUserId,
            ProcessedDate = DateTime.UtcNow,
            TransactionReference = TokenHelper.GenerateTransactionReference()
        };

        // Create penalty transactions for each penalty component
        var penaltyTransactions = new List<DepositTransaction>();
        foreach (var penalty in calculation.PenaltyDetails)
        {
            var penaltyTransaction = new DepositTransaction
            {
                FixedDepositId = deposit.Id,
                TransactionType = DepositTransactionType.PenaltyCharge,
                Amount = penalty.Amount,
                Description = $"Early withdrawal penalty: {penalty.Description}",
                TransactionDate = DateTime.UtcNow,
                Status = TransactionStatus.Completed,
                PenaltyType = deposit.PenaltyType,
                PenaltyReason = penalty.Type,
                ProcessedByUserId = processedByUserId,
                ProcessedDate = DateTime.UtcNow,
                TransactionReference = TokenHelper.GenerateTransactionReference()
            };
            penaltyTransactions.Add(penaltyTransaction);
        }

        // Save all changes
        _unitOfWork.Repository<FixedDeposit>().Update(deposit);
        _unitOfWork.Repository<Account>().Update(deposit.LinkedAccount);
        await _unitOfWork.Repository<DepositTransaction>().AddAsync(withdrawalTransaction);
        
        foreach (var penaltyTransaction in penaltyTransactions)
        {
            await _unitOfWork.Repository<DepositTransaction>().AddAsync(penaltyTransaction);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private static decimal GetPenaltyAmount(DepositTransaction transaction)
    {
        return transaction.TransactionType == DepositTransactionType.PenaltyCharge ? transaction.Amount : 0;
    }
}