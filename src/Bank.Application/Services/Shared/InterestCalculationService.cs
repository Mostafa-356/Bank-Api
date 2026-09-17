using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Bank.Domain.Policies.Account;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

public class InterestCalculationService : IInterestCalculationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<InterestCalculationService> _logger;
    private readonly IAccountDormancyPolicy _dormancyPolicy;

    public InterestCalculationService(
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService,
        ILogger<InterestCalculationService> logger,
        IAccountDormancyPolicy dormancyPolicy)
    {
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
        _logger = logger;
        _dormancyPolicy = dormancyPolicy;
    }

    public async Task<decimal> CalculateSimpleInterestAsync(Account account, DateTime fromDate, DateTime toDate)
    {
        try
        {
            if (account.InterestRate <= 0 || account.Balance <= 0)
                return 0;

            var days = (toDate - fromDate).TotalDays;
            if (days <= 0) return 0;

            // Simple Interest = Principal Ã— Rate Ã— Time / 365
            var interest = account.Balance * (account.InterestRate / 100) * (decimal)(days / 365);
            return Math.Round(interest, 2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating simple interest for account {AccountId}", account.Id);
            return 0;
        }
    }

    public async Task<decimal> CalculateCompoundInterestAsync(Account account, DateTime fromDate, DateTime toDate, int compoundingFrequency = 12)
    {
        try
        {
            if (account.InterestRate <= 0 || account.Balance <= 0)
                return 0;

            var years = (toDate - fromDate).TotalDays / 365;
            if (years <= 0) return 0;

            // Compound Interest = P(1 + r/n)^(nt) - P
            var principal = account.Balance;
            var rate = (double)(account.InterestRate / 100);
            var n = compoundingFrequency;
            var t = years;

            var compoundAmount = principal * (decimal)Math.Pow(1 + (rate / n), n * t);
            var interest = compoundAmount - principal;

            return Math.Round(interest, 2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating compound interest for account {AccountId}", account.Id);
            return 0;
        }
    }

    public async Task<decimal> CalculateDailyInterestAsync(Account account, DateTime date)
    {
        try
        {
            if (account.InterestRate <= 0 || account.Balance <= 0)
                return 0;

            // Daily Interest = Balance Ã— (Annual Rate / 365)
            var dailyRate = account.InterestRate / 100 / 365;
            var dailyInterest = account.Balance * dailyRate;

            return Math.Round(dailyInterest, 4); // Keep more precision for daily calculations
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating daily interest for account {AccountId}", account.Id);
            return 0;
        }
    }

    private static string MaskGuid(Guid value)
    {
        var text = value.ToString("N");
        return $"***{text[^4..]}";
    }

    public async Task<bool> ApplyInterestAsync(Guid accountId, Guid userId)
    {
        try
        {
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            if (account == null)
            {
                _logger.LogWarning("Account {AccountId} not found for interest application", MaskGuid(accountId));
                return false;
            }

            return await ApplyInterestCoreAsync(account, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying interest to account {AccountId}", MaskGuid(accountId));
            return false;
        }
    }

    /// <summary>
    /// Core interest application logic. Accepts an already-loaded <see cref="Account"/> so
    /// batch callers (e.g. <see cref="ProcessMonthlyInterestAsync"/>) avoid the N+1 re-fetch.
    /// </summary>
    private async Task<bool> ApplyInterestCoreAsync(Account account, Guid userId)
    {
        if (account.Status != AccountStatus.Active && account.Status != AccountStatus.Dormant)
        {
            _logger.LogWarning("Account {AccountId} is not eligible for interest calculation", account.Id);
            return false;
        }

        var fromDate = account.LastInterestCalculationDate ?? account.OpenedDate;
        var toDate = DateTime.UtcNow;

        decimal interest = account.CompoundingFrequency switch
        {
            InterestCompoundingFrequency.Daily       => await CalculateCompoundInterestAsync(account, fromDate, toDate, 365),
            InterestCompoundingFrequency.Monthly     => await CalculateCompoundInterestAsync(account, fromDate, toDate, 12),
            InterestCompoundingFrequency.Quarterly   => await CalculateCompoundInterestAsync(account, fromDate, toDate, 4),
            InterestCompoundingFrequency.SemiAnnually => await CalculateCompoundInterestAsync(account, fromDate, toDate, 2),
            InterestCompoundingFrequency.Annually    => await CalculateCompoundInterestAsync(account, fromDate, toDate, 1),
            _                                        => await CalculateSimpleInterestAsync(account, fromDate, toDate)
        };

        if (interest > 0)
        {
            var transaction = new Transaction
            {
                FromAccountId = Guid.Empty,
                ToAccountId   = account.Id,
                Amount        = interest,
                Type          = TransactionType.ACH,
                Status        = TransactionStatus.Completed,
                Description   = $"Interest credit for period {fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd}",
                CreatedAt     = DateTime.UtcNow
            };

            await _unitOfWork.Repository<Transaction>().AddAsync(transaction);

            account.Balance += interest;
            account.LastInterestCalculationDate = toDate;
            _dormancyPolicy.UpdateActivity(account);

            _unitOfWork.Repository<Account>().Update(account);
            await _unitOfWork.SaveChangesAsync();

            await _auditLogService.LogAsync(
                "Interest Applied",
                $"Interest of {interest:C} applied to account {account.Id}",
                userId);

            _logger.LogInformation(
                "Interest of {Interest:C} applied to account {AccountId}", interest, account.Id);
        }

        return true;
    }

    public async Task<bool> ProcessMonthlyInterestAsync()
    {
        try
        {
            var accountsForProcessing = await GetAccountsForInterestProcessingAsync();
            var processedCount = 0;

            // Use ApplyInterestCoreAsync directly â€” accounts are already loaded, avoiding N+1.
            foreach (var account in accountsForProcessing)
            {
                var success = await ApplyInterestCoreAsync(account, Guid.Empty);
                if (success) processedCount++;
            }

            await _auditLogService.LogAsync("Monthly Interest Processed", $"Processed interest for {processedCount} accounts", null);
            _logger.LogInformation("Processed monthly interest for {ProcessedCount} accounts", processedCount);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing monthly interest");
            return false;
        }
    }

    public async Task<List<Account>> GetAccountsForInterestProcessingAsync()
    {
        try
        {
            var accounts = await _unitOfWork.Repository<Account>().GetAllAsync();
            var currentDate = DateTime.UtcNow;
            
            return accounts.Where(a => 
                (a.Status == AccountStatus.Active || a.Status == AccountStatus.Dormant) &&
                a.InterestRate > 0 &&
                a.Balance > 0 &&
                (a.LastInterestCalculationDate == null || 
                 a.LastInterestCalculationDate.Value.Month != currentDate.Month ||
                 a.LastInterestCalculationDate.Value.Year != currentDate.Year))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving accounts for interest processing");
            return new List<Account>();
        }
    }

    public async Task<decimal> GetInterestRateAsync(AccountType accountType, decimal balance)
    {
        try
        {
            // Tiered interest rates based on account type and balance
            return accountType switch
            {
                AccountType.Savings => balance switch
                {
                    >= 100000 => 2.5m,
                    >= 50000 => 2.0m,
                    >= 10000 => 1.5m,
                    >= 1000 => 1.0m,
                    _ => 0.5m
                },
                AccountType.Premium => balance switch
                {
                    >= 100000 => 3.5m,
                    >= 50000 => 3.0m,
                    >= 10000 => 2.5m,
                    _ => 2.0m
                },
                AccountType.Business => balance switch
                {
                    >= 500000 => 2.0m,
                    >= 100000 => 1.5m,
                    >= 50000 => 1.0m,
                    _ => 0.5m
                },
                AccountType.Checking => 0.1m, // Minimal interest for checking accounts
                _ => 0m
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting interest rate for account type {AccountType}", accountType);
            return 0;
        }
    }

    public async Task<bool> UpdateInterestRateAsync(Guid accountId, decimal newRate, Guid userId)
    {
        try
        {
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            var maskedAccountId = SecureLoggingService.MaskGuid(accountId);
            if (account == null)
            {
                _logger.LogWarning("Account {AccountId} not found for interest rate update", maskedAccountId);
                return false;
            }

            if (newRate < 0 || newRate > 10) // Reasonable bounds
            {
                _logger.LogWarning("Invalid interest rate {Rate} for account {AccountId}", newRate, maskedAccountId);
                return false;
            }

            var oldRate = account.InterestRate;
            account.InterestRate = newRate;

            _unitOfWork.Repository<Account>().Update(account);
            await _unitOfWork.SaveChangesAsync();

            await _auditLogService.LogAsync("Interest Rate Updated", 
                $"Interest rate for account {maskedAccountId} updated from {oldRate}% to {newRate}%", userId);
            _logger.LogInformation("Interest rate for account {AccountId} updated from {OldRate}% to {NewRate}%", 
                maskedAccountId, oldRate, newRate);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating interest rate for account {AccountId}", SecureLoggingService.MaskGuid(accountId));
            return false;
        }
    }

}

