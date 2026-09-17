using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

public class FeeCalculationService : IFeeCalculationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FeeCalculationService> _logger;

    public FeeCalculationService(IUnitOfWork unitOfWork, ILogger<FeeCalculationService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<decimal> CalculateMaintenanceFeeAsync(Account account, DateTime fromDate, DateTime toDate)
    {
        try
        {
            var feeSchedule = await GetFeeScheduleAsync(account.Type);
            if (feeSchedule == null || !feeSchedule.IsApplicable(account))
                return 0;

            if (feeSchedule.IsWaiverEligible(account))
                return 0;

            // Calculate monthly maintenance fee
            var monthsDiff = ((toDate.Year - fromDate.Year) * 12) + toDate.Month - fromDate.Month;
            if (monthsDiff <= 0) monthsDiff = 1;

            return feeSchedule.Amount * monthsDiff;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating maintenance fee for account {MaskedAccountId}", SecureLoggingService.MaskGuid(account.Id));
            return 0;
        }
    }

    public async Task<decimal> CalculateOverdraftFeeAsync(Account account, decimal overdraftAmount)
    {
        try
        {
            var feeSchedules = await _unitOfWork.Repository<FeeSchedule>().GetAllAsync();
            var overdraftFeeSchedule = feeSchedules.FirstOrDefault(fs => 
                fs.Type == FeeType.OverdraftFee && 
                fs.IsActive && 
                fs.IsApplicable(account));

            if (overdraftFeeSchedule == null)
                return 0;

            if (overdraftFeeSchedule.IsWaiverEligible(account))
                return 0;

            return overdraftFeeSchedule.Amount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating overdraft fee for account {MaskedAccountId}", SecureLoggingService.MaskGuid(account.Id));
            return 0;
        }
    }

    public async Task<decimal> CalculateInactivityFeeAsync(Account account, int daysSinceLastActivity)
    {
        try
        {
            var feeSchedules = await _unitOfWork.Repository<FeeSchedule>().GetAllAsync();
            var inactivityFeeSchedule = feeSchedules.FirstOrDefault(fs => 
                fs.Type == FeeType.DormancyFee && 
                fs.IsActive && 
                fs.IsApplicable(account) &&
                fs.DormancyDaysThreshold.HasValue &&
                daysSinceLastActivity >= fs.DormancyDaysThreshold.Value);

            if (inactivityFeeSchedule == null)
                return 0;

            if (inactivityFeeSchedule.IsWaiverEligible(account))
                return 0;

            return inactivityFeeSchedule.Amount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating inactivity fee for account {MaskedAccountId}", SecureLoggingService.MaskGuid(account.Id));
            return 0;
        }
    }

    public async Task<decimal> CalculateEarlyClosureFeeAsync(Account account)
    {
        try
        {
            var feeSchedules = await _unitOfWork.Repository<FeeSchedule>().GetAllAsync();
            var closureFeeSchedule = feeSchedules.FirstOrDefault(fs => 
                fs.Type == FeeType.AccountClosureFee && 
                fs.IsActive && 
                fs.IsApplicable(account));

            if (closureFeeSchedule == null)
                return 0;

            if (closureFeeSchedule.IsWaiverEligible(account))
                return 0;

            // Check if account is being closed within a certain period (e.g., 6 months)
            var accountAge = DateTime.UtcNow - account.OpenedDate;
            if (accountAge.TotalDays < 180) // 6 months
            {
                return closureFeeSchedule.Amount;
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating early closure fee for account {MaskedAccountId}", SecureLoggingService.MaskGuid(account.Id));
            return 0;
        }
    }

    public async Task<decimal> CalculateMinimumBalanceFeeAsync(Account account, decimal minimumBalance)
    {
        try
        {
            if (account.Balance >= minimumBalance)
                return 0;

            var feeSchedules = await _unitOfWork.Repository<FeeSchedule>().GetAllAsync();
            var minBalanceFeeSchedule = feeSchedules.FirstOrDefault(fs => 
                fs.Type == FeeType.MinimumBalanceFee && 
                fs.IsActive && 
                fs.IsApplicable(account));

            if (minBalanceFeeSchedule == null)
                return 0;

            if (minBalanceFeeSchedule.IsWaiverEligible(account))
                return 0;

            return minBalanceFeeSchedule.Amount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating minimum balance fee for account {MaskedAccountId}", SecureLoggingService.MaskGuid(account.Id));
            return 0;
        }
    }

    public async Task<FeeSchedule> GetFeeScheduleAsync(AccountType accountType)
    {
        try
        {
            var feeSchedules = await _unitOfWork.Repository<FeeSchedule>().GetAllAsync();
            return feeSchedules.FirstOrDefault(fs => 
                fs.Type == FeeType.MonthlyMaintenance && 
                fs.IsActive && 
                (fs.AccountType == accountType || fs.AccountType == null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fee schedule for account type {AccountType}", accountType);
            return null;
        }
    }

    public async Task<List<AccountFee>> GetPendingFeesAsync(Guid accountId)
    {
        try
        {
            var fees = await _unitOfWork.Repository<AccountFee>().GetAllAsync();
            return fees.Where(f => 
                f.AccountId == accountId && 
                f.AppliedDate == null && 
                !f.IsWaived)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending fees for account {MaskedAccountId}", SecureLoggingService.MaskGuid(accountId));
            return new List<AccountFee>();
        }
    }

    public async Task<bool> WaiveFeeAsync(Guid feeId, string reason, Guid userId)
    {
        try
        {
            var fee = await _unitOfWork.Repository<AccountFee>().GetByIdAsync(feeId);
            if (fee == null)
            {
                _logger.LogWarning("Fee {FeeId} not found for waiver", feeId);
                return false;
            }

            if (fee.IsWaived || fee.AppliedDate != null)
            {
                _logger.LogWarning("Fee {FeeId} cannot be waived - already waived or applied", feeId);
                return false;
            }

            fee.WaiveFee(reason, userId);
            _unitOfWork.Repository<AccountFee>().Update(fee);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Fee {FeeId} waived successfully", feeId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error waiving fee {FeeId}", feeId);
            return false;
        }
    }

    public async Task<decimal> CalculateTotalFeesAsync(Guid accountId, DateTime fromDate, DateTime toDate)
    {
        try
        {
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            if (account == null)
                return 0;

            decimal totalFees = 0;

            // Calculate maintenance fee
            totalFees += await CalculateMaintenanceFeeAsync(account, fromDate, toDate);

            // Calculate inactivity fee if applicable
            var daysSinceLastActivity = (DateTime.UtcNow - account.LastActivityDate).TotalDays;
            if (daysSinceLastActivity > account.DormancyPeriodDays)
            {
                totalFees += await CalculateInactivityFeeAsync(account, (int)daysSinceLastActivity);
            }

            // Calculate minimum balance fee if applicable
            totalFees += await CalculateMinimumBalanceFeeAsync(account, account.MinimumBalance);

            return totalFees;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating total fees for account {MaskedAccountId}", SecureLoggingService.MaskGuid(accountId));
            return 0;
        }
    }
}

