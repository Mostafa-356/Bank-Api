using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Bank.Domain.Policies.Account;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Bank.Application.Services;

public class AccountLifecycleService : IAccountLifecycleService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFeeCalculationService _feeCalculationService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AccountLifecycleService> _logger;
    private readonly IAccountDormancyPolicy _dormancyPolicy;

    public AccountLifecycleService(
        IUnitOfWork unitOfWork,
        IFeeCalculationService feeCalculationService,
        IAuditLogService auditLogService,
        ILogger<AccountLifecycleService> logger,
        IAccountDormancyPolicy dormancyPolicy)
    {
        _unitOfWork = unitOfWork;
        _feeCalculationService = feeCalculationService;
        _auditLogService = auditLogService;
        _logger = logger;
        _dormancyPolicy = dormancyPolicy;
    }

    /// <summary>
    /// Masks a sensitive identifier (accountId or userId) for logging purposes.
    /// Returns a hash prefix to avoid storing full sensitive identifiers in logs.
    /// </summary>
    private static string MaskSensitiveId(Guid id)
    {
        using (var sha256 = SHA256.Create())
        {
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(id.ToString()));
            return BitConverter.ToString(hash, 0, 4).Replace("-", "").ToLowerInvariant();
        }
    }

    public async Task<bool> CloseAccountAsync(Guid accountId, string reason, Guid userId)
    {
        try
        {
            var maskedId = MaskSensitiveId(accountId);
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            if (account == null)
            {
                _logger.LogWarning("Account {MaskedAccountId} not found for closure", maskedId);
                return false;
            }

            if (account.Status == AccountStatus.Closed)
            {
                _logger.LogWarning("Account {MaskedAccountId} is already closed", maskedId);
                return false;
            }

            // Check for pending transactions or holds
            if (account.HasHolds)
            {
                _logger.LogWarning("Cannot close account {MaskedAccountId} with active holds", maskedId);
                return false;
            }

            // Calculate and apply any final fees
            await ApplyAccountFeesAsync(accountId, userId);

            // Update account status
            var previousStatus = account.Status;
            account.Status = AccountStatus.Closed;
            account.ClosedDate = DateTime.UtcNow;
            account.ClosureReason = reason;
            account.UpdatedAt = DateTime.UtcNow;
            account.UpdatedBy = userId.ToString();

            // Add status history
            var statusHistory = new AccountStatusHistory
            {
                AccountId = accountId,
                FromStatus = previousStatus,
                ToStatus = AccountStatus.Closed,
                Reason = reason,
                ChangedByUserId = userId
            };

            await _unitOfWork.Repository<AccountStatusHistory>().AddAsync(statusHistory);
            _unitOfWork.Repository<Account>().Update(account);

            await _auditLogService.LogUserActionAsync(
                userId,
                "ACCOUNT_CLOSED",
                "Account",
                accountId.ToString(),
                null,
                $"Reason: {reason}");

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Account {MaskedAccountId} closed successfully", maskedId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing account {MaskedAccountId}", MaskSensitiveId(accountId));
            return false;
        }
    }

    public async Task<bool> ReopenAccountAsync(Guid accountId, Guid userId)
    {
        try
        {
            var maskedId = MaskSensitiveId(accountId);
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            if (account == null)
            {
                _logger.LogWarning("Account {MaskedAccountId} not found for reopening", maskedId);
                return false;
            }

            if (account.Status != AccountStatus.Closed)
            {
                _logger.LogWarning("Account {MaskedAccountId} is not closed", maskedId);
                return false;
            }

            // Update account status
            var previousStatus = account.Status;
            account.Status = AccountStatus.Active;
            account.ClosedDate = null;
            account.ClosureReason = null;
            account.UpdatedAt = DateTime.UtcNow;
            account.UpdatedBy = userId.ToString();

            // Add status history
            var statusHistory = new AccountStatusHistory
            {
                AccountId = accountId,
                FromStatus = previousStatus,
                ToStatus = AccountStatus.Active,
                Reason = "Account reopened",
                ChangedByUserId = userId
            };

            await _unitOfWork.Repository<AccountStatusHistory>().AddAsync(statusHistory);
            _unitOfWork.Repository<Account>().Update(account);

            await _auditLogService.LogUserActionAsync(
                userId,
                "ACCOUNT_REOPENED",
                "Account",
                accountId.ToString(),
                null,
                "Account reopened");

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Account {MaskedAccountId} reopened successfully", maskedId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reopening account {MaskedAccountId}", MaskSensitiveId(accountId));
            return false;
        }
    }

    public async Task<bool> MarkAccountDormantAsync(Guid accountId)
    {
        try
        {
            var maskedId = MaskSensitiveId(accountId);
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            if (account == null)
            {
                _logger.LogWarning("Account {MaskedAccountId} not found for dormancy marking", maskedId);
                return false;
            }

            if (account.Status == AccountStatus.Dormant)
            {
                _logger.LogWarning("Account {MaskedAccountId} is already dormant", maskedId);
                return false;
            }

            // Update account status
            var previousStatus = account.Status;
            _dormancyPolicy.MarkAsDormant(account);
            account.UpdatedAt = DateTime.UtcNow;
            account.UpdatedBy = "SYSTEM";

            // Add status history
            var statusHistory = new AccountStatusHistory
            {
                AccountId = accountId,
                FromStatus = previousStatus,
                ToStatus = AccountStatus.Dormant,
                Reason = "Account marked dormant due to inactivity",
                ChangedByUserId = Guid.Empty // System action
            };

            await _unitOfWork.Repository<AccountStatusHistory>().AddAsync(statusHistory);
            _unitOfWork.Repository<Account>().Update(account);

            await _auditLogService.LogSystemEventAsync(
                "ACCOUNT_MARKED_DORMANT",
                "Account",
                accountId.ToString(),
                "Account marked dormant due to inactivity");

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Account {MaskedAccountId} marked as dormant", maskedId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking account {MaskedAccountId} as dormant", MaskSensitiveId(accountId));
            return false;
        }
    }

    public async Task<bool> ReactivateAccountAsync(Guid accountId, Guid userId)
    {
        try
        {
            var maskedId = MaskSensitiveId(accountId);
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            if (account == null)
            {
                _logger.LogWarning("Account {MaskedAccountId} not found for reactivation", maskedId);
                return false;
            }

            if (account.Status != AccountStatus.Dormant)
            {
                _logger.LogWarning("Account {MaskedAccountId} is not dormant", maskedId);
                return false;
            }

            // Update account status
            var previousStatus = account.Status;
            account.Status = AccountStatus.Active;
            account.DormancyDate = null;
            _dormancyPolicy.UpdateActivity(account);
            account.UpdatedAt = DateTime.UtcNow;
            account.UpdatedBy = userId.ToString();

            // Add status history
            var statusHistory = new AccountStatusHistory
            {
                AccountId = accountId,
                FromStatus = previousStatus,
                ToStatus = AccountStatus.Active,
                Reason = "Account reactivated by user",
                ChangedByUserId = userId
            };

            await _unitOfWork.Repository<AccountStatusHistory>().AddAsync(statusHistory);
            _unitOfWork.Repository<Account>().Update(account);

            await _auditLogService.LogUserActionAsync(
                userId,
                "ACCOUNT_REACTIVATED",
                "Account",
                accountId.ToString(),
                null,
                "Account reactivated from dormant status");

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Account {MaskedAccountId} reactivated successfully", maskedId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reactivating account {MaskedAccountId}", MaskSensitiveId(accountId));
            return false;
        }
    }

    public async Task<bool> SuspendAccountAsync(Guid accountId, string reason, Guid userId)
    {
        try
        {
            var maskedId = MaskSensitiveId(accountId);
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            if (account == null)
            {
                _logger.LogWarning("Account {MaskedAccountId} not found for suspension", maskedId);
                return false;
            }

            if (account.Status == AccountStatus.Suspended)
            {
                _logger.LogWarning("Account {MaskedAccountId} is already suspended", maskedId);
                return false;
            }

            // Update account status
            var previousStatus = account.Status;
            account.Status = AccountStatus.Suspended;
            account.UpdatedAt = DateTime.UtcNow;
            account.UpdatedBy = userId.ToString();

            // Add status history
            var statusHistory = new AccountStatusHistory
            {
                AccountId = accountId,
                FromStatus = previousStatus,
                ToStatus = AccountStatus.Suspended,
                Reason = reason,
                ChangedByUserId = userId
            };

            await _unitOfWork.Repository<AccountStatusHistory>().AddAsync(statusHistory);
            _unitOfWork.Repository<Account>().Update(account);

            await _auditLogService.LogUserActionAsync(
                userId,
                "ACCOUNT_SUSPENDED",
                "Account",
                accountId.ToString(),
                null,
                $"Reason: {reason}");

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Account {MaskedAccountId} suspended successfully", maskedId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suspending account {MaskedAccountId}", MaskSensitiveId(accountId));
            return false;
        }
    }

    public async Task<bool> UnsuspendAccountAsync(Guid accountId, Guid userId)
    {
        try
        {
            var maskedId = MaskSensitiveId(accountId);
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            if (account == null)
            {
                _logger.LogWarning("Account {MaskedAccountId} not found for unsuspension", maskedId);
                return false;
            }

            if (account.Status != AccountStatus.Suspended)
            {
                _logger.LogWarning("Account {MaskedAccountId} is not suspended", maskedId);
                return false;
            }

            // Update account status
            var previousStatus = account.Status;
            account.Status = AccountStatus.Active;
            account.UpdatedAt = DateTime.UtcNow;
            account.UpdatedBy = userId.ToString();

            // Add status history
            var statusHistory = new AccountStatusHistory
            {
                AccountId = accountId,
                FromStatus = previousStatus,
                ToStatus = AccountStatus.Active,
                Reason = "Account unsuspended",
                ChangedByUserId = userId
            };

            await _unitOfWork.Repository<AccountStatusHistory>().AddAsync(statusHistory);
            _unitOfWork.Repository<Account>().Update(account);

            await _auditLogService.LogUserActionAsync(
                userId,
                "ACCOUNT_UNSUSPENDED",
                "Account",
                accountId.ToString(),
                null,
                "Account unsuspended");

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Account {MaskedAccountId} unsuspended successfully", maskedId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsuspending account {MaskedAccountId}", MaskSensitiveId(accountId));
            return false;
        }
    }

    public async Task<bool> FreezeAccountAsync(Guid accountId, string reason, Guid userId)
    {
        try
        {
            var maskedId = MaskSensitiveId(accountId);
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            if (account == null)
            {
                _logger.LogWarning("Account {MaskedAccountId} not found for freezing", maskedId);
                return false;
            }

            if (account.Status == AccountStatus.Frozen)
            {
                _logger.LogWarning("Account {MaskedAccountId} is already frozen", maskedId);
                return false;
            }

            // Update account status
            var previousStatus = account.Status;
            account.Status = AccountStatus.Frozen;
            account.UpdatedAt = DateTime.UtcNow;
            account.UpdatedBy = userId.ToString();

            // Add status history
            var statusHistory = new AccountStatusHistory
            {
                AccountId = accountId,
                FromStatus = previousStatus,
                ToStatus = AccountStatus.Frozen,
                Reason = reason,
                ChangedByUserId = userId
            };

            await _unitOfWork.Repository<AccountStatusHistory>().AddAsync(statusHistory);
            _unitOfWork.Repository<Account>().Update(account);

            await _auditLogService.LogUserActionAsync(
                userId,
                "ACCOUNT_FROZEN",
                "Account",
                accountId.ToString(),
                null,
                $"Reason: {reason}");

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Account {MaskedAccountId} frozen successfully", maskedId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error freezing account {MaskedAccountId}", MaskSensitiveId(accountId));
            return false;
        }
    }

    public async Task<bool> UnfreezeAccountAsync(Guid accountId, Guid userId)
    {
        try
        {
            var maskedId = MaskSensitiveId(accountId);
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            if (account == null)
            {
                _logger.LogWarning("Account {MaskedAccountId} not found for unfreezing", maskedId);
                return false;
            }

            if (account.Status != AccountStatus.Frozen)
            {
                _logger.LogWarning("Account {MaskedAccountId} is not frozen", maskedId);
                return false;
            }

            // Update account status
            var previousStatus = account.Status;
            account.Status = AccountStatus.Active;
            account.UpdatedAt = DateTime.UtcNow;
            account.UpdatedBy = userId.ToString();

            // Add status history
            var statusHistory = new AccountStatusHistory
            {
                AccountId = accountId,
                FromStatus = previousStatus,
                ToStatus = AccountStatus.Active,
                Reason = "Account unfrozen",
                ChangedByUserId = userId
            };

            await _unitOfWork.Repository<AccountStatusHistory>().AddAsync(statusHistory);
            _unitOfWork.Repository<Account>().Update(account);

            await _auditLogService.LogUserActionAsync(
                userId,
                "ACCOUNT_UNFROZEN",
                "Account",
                accountId.ToString(),
                null,
                "Account unfrozen");

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Account {MaskedAccountId} unfrozen successfully", maskedId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unfreezing account {MaskedAccountId}", MaskSensitiveId(accountId));
            return false;
        }
    }

    public async Task<bool> ApplyHoldAsync(Guid accountId, decimal amount, string reason, Guid userId)
    {
        try
        {
            var maskedId = MaskSensitiveId(accountId);
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            if (account == null)
            {
                _logger.LogWarning("Account {MaskedAccountId} not found for hold application", maskedId);
                return false;
            }

            if (amount <= 0)
            {
                _logger.LogWarning("Invalid hold amount {Amount} for account {MaskedAccountId}", amount, maskedId);
                return false;
            }

            // Create hold
            var hold = new AccountHold
            {
                AccountId = accountId,
                Type = AccountHoldType.Administrative,
                Amount = amount,
                Description = reason,
                PlacedDate = DateTime.UtcNow,
                PlacedByUserId = userId
            };

            await _unitOfWork.Repository<AccountHold>().AddAsync(hold);

            // Update account
            account.HasHolds = true;
            account.UpdatedAt = DateTime.UtcNow;
            account.UpdatedBy = userId.ToString();

            _unitOfWork.Repository<Account>().Update(account);

            await _auditLogService.LogUserActionAsync(
                userId,
                "HOLD_APPLIED",
                "Account",
                accountId.ToString(),
                null,
                $"Hold amount: {amount:C}, Reason: {reason}");

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Hold applied to account {MaskedAccountId} for amount {Amount}", maskedId, amount);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying hold to account {MaskedAccountId}", MaskSensitiveId(accountId));
            return false;
        }
    }

    public async Task<bool> ReleaseHoldAsync(Guid holdId, Guid userId)
    {
        try
        {
            var hold = await _unitOfWork.Repository<AccountHold>().GetByIdAsync(holdId);
            if (hold == null)
            {
                _logger.LogWarning("Hold {HoldId} not found for release", holdId);
                return false;
            }

            if (!hold.IsActive)
            {
                _logger.LogWarning("Hold {HoldId} is already released", holdId);
                return false;
            }

            // Release hold
            hold.Release(userId);

            _unitOfWork.Repository<AccountHold>().Update(hold);

            // Check if account has any other active holds
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(hold.AccountId);
            if (account != null)
            {
                var activeHolds = await _unitOfWork.Repository<AccountHold>()
                    .FindAsync(h => h.AccountId == hold.AccountId && h.IsActive);
                
                account.HasHolds = activeHolds.Any();
                account.UpdatedAt = DateTime.UtcNow;
                account.UpdatedBy = userId.ToString();

                _unitOfWork.Repository<Account>().Update(account);
            }

            await _auditLogService.LogUserActionAsync(
                userId,
                "HOLD_RELEASED",
                "Account",
                hold.AccountId.ToString(),
                null,
                $"Hold ID: {holdId}");

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Hold {HoldId} released successfully", holdId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing hold {HoldId}", holdId);
            return false;
        }
    }

    public async Task<bool> AddRestrictionAsync(Guid accountId, AccountRestrictionType restrictionType, string reason, Guid userId)
    {
        try
        {
            var maskedId = MaskSensitiveId(accountId);
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            if (account == null)
            {
                _logger.LogWarning("Account {MaskedAccountId} not found for restriction addition", maskedId);
                return false;
            }

            // Check if restriction already exists
            var existingRestriction = await _unitOfWork.Repository<AccountRestriction>()
                .FirstOrDefaultAsync(r => r.AccountId == accountId && r.Type == restrictionType && r.IsActive);

            if (existingRestriction != null)
            {
                _logger.LogWarning("Restriction {RestrictionType} already exists for account {MaskedAccountId}", restrictionType, maskedId);
                return false;
            }

            // Create restriction
            var restriction = new AccountRestriction
            {
                AccountId = accountId,
                Type = restrictionType,
                Description = reason,
                AppliedDate = DateTime.UtcNow,
                AppliedByUserId = userId
            };

            await _unitOfWork.Repository<AccountRestriction>().AddAsync(restriction);

            // Update account
            account.HasRestrictions = true;
            account.UpdatedAt = DateTime.UtcNow;
            account.UpdatedBy = userId.ToString();

            _unitOfWork.Repository<Account>().Update(account);

            await _auditLogService.LogUserActionAsync(
                userId,
                "RESTRICTION_ADDED",
                "Account",
                accountId.ToString(),
                null,
                $"Restriction: {restrictionType}, Reason: {reason}");

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Restriction {RestrictionType} added to account {MaskedAccountId}", restrictionType, maskedId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding restriction to account {MaskedAccountId}", MaskSensitiveId(accountId));
            return false;
        }
    }

    public async Task<bool> RemoveRestrictionAsync(Guid restrictionId, Guid userId)
    {
        try
        {
            var restriction = await _unitOfWork.Repository<AccountRestriction>().GetByIdAsync(restrictionId);
            if (restriction == null)
            {
                _logger.LogWarning("Restriction {RestrictionId} not found for removal", restrictionId);
                return false;
            }

            if (!restriction.IsActive)
            {
                _logger.LogWarning("Restriction {RestrictionId} is already removed", restrictionId);
                return false;
            }

            // Remove restriction
            restriction.Remove(userId);

            _unitOfWork.Repository<AccountRestriction>().Update(restriction);

            // Check if account has any other active restrictions
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(restriction.AccountId);
            if (account != null)
            {
                var activeRestrictions = await _unitOfWork.Repository<AccountRestriction>()
                    .FindAsync(r => r.AccountId == restriction.AccountId && r.IsActive);
                
                account.HasRestrictions = activeRestrictions.Any();
                account.UpdatedAt = DateTime.UtcNow;
                account.UpdatedBy = userId.ToString();

                _unitOfWork.Repository<Account>().Update(account);
            }

            await _auditLogService.LogUserActionAsync(
                userId,
                "RESTRICTION_REMOVED",
                "Account",
                restriction.AccountId.ToString(),
                null,
                $"Restriction ID: {restrictionId}");

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Restriction {RestrictionId} removed successfully", restrictionId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing restriction {RestrictionId}", restrictionId);
            return false;
        }
    }

    public async Task<decimal> CalculateAccountFeesAsync(Guid accountId, DateTime fromDate, DateTime toDate)
    {
        try
        {
            var maskedId = MaskSensitiveId(accountId);
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            if (account == null)
            {
                _logger.LogWarning("Account {MaskedAccountId} not found for fee calculation", maskedId);
                return 0;
            }

            decimal totalFees = 0;

            // Calculate maintenance fees
            totalFees += await _feeCalculationService.CalculateMaintenanceFeeAsync(account, fromDate, toDate);

            // Calculate inactivity fees if applicable
            var daysSinceLastActivity = (DateTime.UtcNow - account.LastActivityDate).Days;
            if (daysSinceLastActivity > 90) // 3 months
            {
                totalFees += await _feeCalculationService.CalculateInactivityFeeAsync(account, daysSinceLastActivity);
            }

            return totalFees;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating fees for account {MaskedAccountId}", MaskSensitiveId(accountId));
            return 0;
        }
    }

    public async Task<bool> ApplyAccountFeesAsync(Guid accountId, Guid userId)
    {
        try
        {
            var maskedId = MaskSensitiveId(accountId);
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            if (account == null)
            {
                _logger.LogWarning("Account {MaskedAccountId} not found for fee application", maskedId);
                return false;
            }

            return await ApplyAccountFeesCoreAsync(account, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying fees to account {MaskedAccountId}", MaskSensitiveId(accountId));
            return false;
        }
    }

    /// <summary>
    /// Core fee application logic. Accepts an already-loaded <see cref="Account"/> so
    /// batch callers (e.g. <see cref="ProcessMonthlyMaintenanceFeesAsync"/>) avoid the N+1 re-fetch.
    /// </summary>
    private async Task<bool> ApplyAccountFeesCoreAsync(Account account, Guid userId)
    {
        var fromDate = account.LastFeeCalculationDate ?? account.OpenedDate;
        var toDate   = DateTime.UtcNow;

        var totalFees = await CalculateAccountFeesAsync(account.Id, fromDate, toDate);

        if (totalFees > 0)
        {
            var fee = new AccountFee
            {
                AccountId   = account.Id,
                Type        = FeeType.MonthlyMaintenance,
                Amount      = totalFees,
                Description = $"Monthly maintenance fee for period {fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd}",
                AppliedDate = DateTime.UtcNow
            };

            await _unitOfWork.Repository<AccountFee>().AddAsync(fee);

            account.Balance              -= totalFees;
            account.LastFeeCalculationDate = toDate;
            account.UpdatedAt            = toDate;
            account.UpdatedBy            = userId.ToString();

            _unitOfWork.Repository<Account>().Update(account);

            await _auditLogService.LogUserActionAsync(
                userId, "FEES_APPLIED", "Account", account.Id.ToString(),
                null, $"Fees applied: {totalFees:C}");

            _logger.LogInformation(
                "Fees {Amount} applied to account {MaskedAccountId}", totalFees, MaskSensitiveId(account.Id));
        }

        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<List<Account>> GetDormantAccountsAsync(int daysSinceLastActivity)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysSinceLastActivity);
            var accounts = await _unitOfWork.Repository<Account>()
                .FindAsync(a => a.Status == AccountStatus.Active && 
                               a.LastActivityDate < cutoffDate);

            return accounts.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dormant accounts");
            return new List<Account>();
        }
    }

    public async Task<List<Account>> GetAccountsForFeeProcessingAsync()
    {
        try
        {
            var accounts = await _unitOfWork.Repository<Account>()
                .FindAsync(a => a.Status == AccountStatus.Active && 
                               (a.LastFeeCalculationDate == null || 
                                a.LastFeeCalculationDate < DateTime.UtcNow.AddMonths(-1)));

            return accounts.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting accounts for fee processing");
            return new List<Account>();
        }
    }

    public async Task<bool> ProcessMonthlyMaintenanceFeesAsync()
    {
        try
        {
            var accounts = await GetAccountsForFeeProcessingAsync();
            var processedCount = 0;

            // Use ApplyAccountFeesCoreAsync directly â€” accounts are already loaded, avoiding N+1.
            foreach (var account in accounts)
            {
                if (await ApplyAccountFeesCoreAsync(account, Guid.Empty))
                    processedCount++;
            }

            await _auditLogService.LogSystemEventAsync(
                "MONTHLY_FEES_PROCESSED",
                "System",
                "FeeProcessing",
                $"Processed {processedCount} accounts");

            _logger.LogInformation("Monthly maintenance fees processed for {Count} accounts", processedCount);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing monthly maintenance fees");
            return false;
        }
    }
}