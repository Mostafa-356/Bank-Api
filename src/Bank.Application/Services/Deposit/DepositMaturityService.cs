using Bank.Application.DTOs;
using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

/// <summary>
/// Specialized service for handling deposit maturity processing and customer consent management
/// </summary>
public class DepositMaturityService : IDepositMaturityService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDepositService _depositService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<DepositMaturityService> _logger;

    public DepositMaturityService(
        IUnitOfWork unitOfWork,
        IDepositService depositService,
        IAuditLogService auditLogService,
        ILogger<DepositMaturityService> logger)
    {
        _unitOfWork = unitOfWork;
        _depositService = depositService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <summary>
    /// Process deposits approaching maturity and send appropriate notices
    /// </summary>
    public async Task<MaturityProcessingResult> ProcessApproachingMaturityAsync()
    {
        var result = new MaturityProcessingResult();
        
        try
        {
            // Get deposits maturing in the next 30 days
            var approachingMaturity = await GetDepositsApproachingMaturityAsync();
            
            foreach (var deposit in approachingMaturity)
            {
                try
                {
                    await ProcessSingleDepositMaturityNoticesAsync(deposit, result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing maturity notices for deposit {DepositId}", deposit.Id);
                    result.Errors.Add($"Deposit {deposit.DepositNumber}: {ex.Message}");
                }
            }

            _logger.LogInformation("Processed maturity notices for {Count} deposits", result.ProcessedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ProcessApproachingMaturityAsync");
            result.Errors.Add($"General error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Process deposits that have matured and execute customer instructions
    /// </summary>
    public async Task<MaturityProcessingResult> ProcessMaturedDepositsAsync()
    {
        var result = new MaturityProcessingResult();
        
        try
        {
            var maturedDeposits = await GetMaturedDepositsAsync();
            
            foreach (var deposit in maturedDeposits)
            {
                try
                {
                    await ProcessSingleMaturedDepositAsync(deposit, result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing matured deposit {DepositId}", deposit.Id);
                    result.Errors.Add($"Deposit {deposit.DepositNumber}: {ex.Message}");
                }
            }

            _logger.LogInformation("Processed {Count} matured deposits", result.ProcessedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ProcessMaturedDepositsAsync");
            result.Errors.Add($"General error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Handle customer consent for auto-renewal
    /// </summary>
    public async Task<bool> ProcessCustomerConsentAsync(Guid depositId, bool consentGiven, MaturityAction? preferredAction = null)
    {
        try
        {
            var deposit = await _unitOfWork.Repository<FixedDeposit>().GetByIdAsync(depositId);
            if (deposit == null)
                return false;

            deposit.CustomerConsentReceived = consentGiven;
            
            if (preferredAction.HasValue)
                deposit.MaturityAction = preferredAction.Value;

            _unitOfWork.Repository<FixedDeposit>().Update(deposit);
            await _unitOfWork.SaveChangesAsync();

            await _auditLogService.LogUserActionAsync(
                deposit.CustomerId,
                "FixedDeposit",
                "ConsentUpdate",
                depositId.ToString(),
                $"Customer consent {(consentGiven ? "given" : "withdrawn")} for deposit {deposit.DepositNumber}");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing customer consent for deposit {DepositId}", depositId);
            return false;
        }
    }

    /// <summary>
    /// Generate and send renewal reminder notices
    /// </summary>
    public async Task<int> SendRenewalRemindersAsync()
    {
        var sentCount = 0;
        
        try
        {
            var depositsNeedingReminders = await GetDepositsNeedingRenewalRemindersAsync();
            
            foreach (var deposit in depositsNeedingReminders)
            {
                try
                {
                    await _depositService.GenerateMaturityNoticeAsync(deposit.Id, MaturityNoticeType.Reminder, Guid.Empty);
                    sentCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending renewal reminder for deposit {DepositId}", deposit.Id);
                }
            }

            _logger.LogInformation("Sent {Count} renewal reminders", sentCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SendRenewalRemindersAsync");
        }

        return sentCount;
    }

    /// <summary>
    /// Process automatic renewals for deposits with customer consent
    /// </summary>
    public async Task<MaturityProcessingResult> ProcessAutomaticRenewalsAsync()
    {
        var result = new MaturityProcessingResult();
        
        try
        {
            var depositsForAutoRenewal = await GetDepositsForAutoRenewalAsync();
            
            foreach (var deposit in depositsForAutoRenewal)
            {
                try
                {
                    var renewalRequest = new RenewDepositRequest
                    {
                        TermDays = deposit.RenewalTermDays ?? deposit.TermDays,
                        AutoRenewalEnabled = deposit.AutoRenewalEnabled
                    };

                    await _depositService.RenewFixedDepositAsync(deposit.Id, renewalRequest, Guid.Empty);
                    result.ProcessedCount++;
                    result.SuccessfulRenewals++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error auto-renewing deposit {DepositId}", deposit.Id);
                    result.Errors.Add($"Deposit {deposit.DepositNumber}: {ex.Message}");
                }
            }

            _logger.LogInformation("Processed {Count} automatic renewals", result.SuccessfulRenewals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ProcessAutomaticRenewalsAsync");
            result.Errors.Add($"General error: {ex.Message}");
        }

        return result;
    }

    private async Task<IEnumerable<FixedDeposit>> GetDepositsApproachingMaturityAsync()
    {
        var thirtyDaysFromNow = DateTime.UtcNow.AddDays(30);
        var sevenDaysFromNow = DateTime.UtcNow.AddDays(7);
        
        return await _unitOfWork.Repository<FixedDeposit>()
            .FindAsync(d => d.Status == FixedDepositStatus.Active &&
                           d.MaturityDate >= DateTime.UtcNow &&
                           d.MaturityDate <= thirtyDaysFromNow);
    }

    private async Task<IEnumerable<FixedDeposit>> GetMaturedDepositsAsync()
    {
        return await _unitOfWork.Repository<FixedDeposit>()
            .FindAsync(d => d.Status == FixedDepositStatus.Active &&
                           d.MaturityDate <= DateTime.UtcNow);
    }

    private async Task<IEnumerable<FixedDeposit>> GetDepositsNeedingRenewalRemindersAsync()
    {
        var sevenDaysFromNow = DateTime.UtcNow.AddDays(7);
        
        return await _unitOfWork.Repository<FixedDeposit>()
            .FindAsync(d => d.Status == FixedDepositStatus.Active &&
                           d.AutoRenewalEnabled &&
                           !d.CustomerConsentReceived &&
                           d.MaturityDate <= sevenDaysFromNow &&
                           d.MaturityDate > DateTime.UtcNow);
    }

    private async Task<IEnumerable<FixedDeposit>> GetDepositsForAutoRenewalAsync()
    {
        return await _unitOfWork.Repository<FixedDeposit>()
            .FindAsync(d => d.Status == FixedDepositStatus.Active &&
                           d.MaturityDate <= DateTime.UtcNow &&
                           d.AutoRenewalEnabled &&
                           d.CustomerConsentReceived);
    }

    private async Task ProcessSingleDepositMaturityNoticesAsync(FixedDeposit deposit, MaturityProcessingResult result)
    {
        var daysToMaturity = (deposit.MaturityDate - DateTime.UtcNow).Days;
        
        // Check if notices have already been sent
        var existingNotices = await _unitOfWork.Repository<MaturityNotice>()
            .FindAsync(n => n.FixedDepositId == deposit.Id);

        var hasInitialNotice = existingNotices.Any(n => n.NoticeType == MaturityNoticeType.Initial);
        var hasReminderNotice = existingNotices.Any(n => n.NoticeType == MaturityNoticeType.Reminder);

        // Send initial notice (30 days before maturity)
        if (daysToMaturity <= 30 && daysToMaturity > 7 && !hasInitialNotice)
        {
            await _depositService.GenerateMaturityNoticeAsync(deposit.Id, MaturityNoticeType.Initial, Guid.Empty);
            result.ProcessedCount++;
            result.NoticesSent++;
        }
        
        // Send reminder notice (7 days before maturity)
        else if (daysToMaturity <= 7 && daysToMaturity > 1 && !hasReminderNotice)
        {
            await _depositService.GenerateMaturityNoticeAsync(deposit.Id, MaturityNoticeType.Reminder, Guid.Empty);
            result.ProcessedCount++;
            result.NoticesSent++;
        }
        
        // Send final notice (1 day before maturity)
        else if (daysToMaturity <= 1 && daysToMaturity >= 0)
        {
            var hasFinalNotice = existingNotices.Any(n => n.NoticeType == MaturityNoticeType.Final);
            if (!hasFinalNotice)
            {
                await _depositService.GenerateMaturityNoticeAsync(deposit.Id, MaturityNoticeType.Final, Guid.Empty);
                result.ProcessedCount++;
                result.NoticesSent++;
            }
        }
    }

    private async Task ProcessSingleMaturedDepositAsync(FixedDeposit deposit, MaturityProcessingResult result)
    {
        // If auto-renewal is enabled and customer has given consent, process renewal
        if (deposit.AutoRenewalEnabled && deposit.CustomerConsentReceived)
        {
            var renewalRequest = new RenewDepositRequest
            {
                TermDays = deposit.RenewalTermDays ?? deposit.TermDays,
                AutoRenewalEnabled = deposit.AutoRenewalEnabled
            };

            await _depositService.RenewFixedDepositAsync(deposit.Id, renewalRequest, Guid.Empty);
            result.SuccessfulRenewals++;
        }
        else
        {
            // Process according to default maturity action
            await _depositService.ProcessMaturityAsync(deposit.Id, deposit.MaturityAction, Guid.Empty);
            result.MaturityActionsProcessed++;
        }

        result.ProcessedCount++;
    }
}