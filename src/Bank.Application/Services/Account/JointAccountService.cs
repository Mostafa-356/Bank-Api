using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Bank.Domain.Policies.Account;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

public class JointAccountService : IJointAccountService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogService _auditLogService;
    private readonly IAccountService _accountService;
    private readonly ILogger<JointAccountService> _logger;
    private readonly IJointAccountPolicy _jointAccountPolicy;
    private readonly IAccountTransactionPolicy _transactionPolicy;

    public JointAccountService(
        IUnitOfWork unitOfWork,
        IUserRepository userRepository,
        IAuditLogService auditLogService,
        IAccountService accountService,
        ILogger<JointAccountService> logger,
        IJointAccountPolicy jointAccountPolicy,
        IAccountTransactionPolicy transactionPolicy)
    {
        _unitOfWork = unitOfWork;
        _userRepository = userRepository;
        _auditLogService = auditLogService;
        _accountService = accountService;
        _logger = logger;
        _jointAccountPolicy = jointAccountPolicy;
        _transactionPolicy = transactionPolicy;
    }

    public async Task<bool> AddJointHolderAsync(Guid accountId, Guid userId, JointAccountRole role, Guid addedByUserId)
    {
        try
        {
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            if (account == null)
            {
                _logger.LogWarning("Account {AccountId} not found for joint holder addition", accountId);
                return false;
            }

            // Check if user is already a joint holder
            if (_jointAccountPolicy.HasJointHolder(account, userId) || account.UserId == userId)
            {
                _logger.LogWarning("User {UserId} is already associated with account {AccountId}", userId, accountId);
                return false;
            }

            // Verify the user exists
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return false;
            }

            var jointHolder = new JointAccountHolder
            {
                AccountId = account.Id,
                UserId = userId,
                Role = role,
                AddedByUserId = addedByUserId,
                RequiresSignature = role != JointAccountRole.ViewOnly,
                TransactionLimit = role == JointAccountRole.SecondaryHolder ? 10000 : null,
                DailyLimit = role == JointAccountRole.SecondaryHolder ? 25000 : null
            };

            await _unitOfWork.Repository<JointAccountHolder>().AddAsync(jointHolder);

            // Mark account as joint account if not already
            if (!account.IsJointAccount)
            {
                account.IsJointAccount = true;
                _unitOfWork.Repository<Account>().Update(account);
            }

            await _unitOfWork.SaveChangesAsync();

            await _auditLogService.LogAsync("Joint Holder Added", 
                $"User {userId} added as {role} to account {accountId}", addedByUserId);
            _logger.LogInformation("Joint holder {UserId} added to account {AccountId} with role {Role}", 
                userId, accountId, role);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding joint holder to account {AccountId}", accountId);
            return false;
        }
    }

    public async Task<bool> RemoveJointHolderAsync(Guid accountId, Guid userId, Guid removedByUserId)
    {
        try
        {
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            if (account == null)
            {
                _logger.LogWarning("Account {AccountId} not found for joint holder removal", accountId);
                return false;
            }

            var jointHolder = account.JointHolders.FirstOrDefault(jh => jh.UserId == userId && jh.IsActive);
            if (jointHolder == null)
            {
                _logger.LogWarning("Active joint holder {UserId} not found for account {AccountId}", userId, accountId);
                return false;
            }

            jointHolder.Remove(removedByUserId);
            _unitOfWork.Repository<JointAccountHolder>().Update(jointHolder);

            // Check if account should remain as joint account
            var activeJointHolders = account.JointHolders.Count(jh => jh.IsActive);
            if (activeJointHolders == 0)
            {
                account.IsJointAccount = false;
                account.RequiresMultipleSignatures = false;
                _unitOfWork.Repository<Account>().Update(account);
            }

            await _unitOfWork.SaveChangesAsync();

            await _auditLogService.LogAsync("Joint Holder Removed", 
                $"User {userId} removed from account {accountId}", removedByUserId);
            _logger.LogInformation("Joint holder {UserId} removed from account {AccountId}", userId, accountId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing joint holder from account {AccountId}", accountId);
            return false;
        }
    }

    public async Task<bool> UpdateJointHolderRoleAsync(Guid accountId, Guid userId, JointAccountRole newRole, Guid updatedByUserId)
    {
        var maskedAccountId = MaskGuid(accountId);
        try
        {
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            if (account == null)
            {
                _logger.LogWarning("Account {AccountId} not found for joint holder role update", maskedAccountId);
                return false;
            }

            var jointHolder = account.JointHolders.FirstOrDefault(jh => jh.UserId == userId && jh.IsActive);
            if (jointHolder == null)
            {
                _logger.LogWarning("Active joint holder {UserId} not found for account {AccountId}", userId, maskedAccountId);
                return false;
            }

            var oldRole = jointHolder.Role;
            jointHolder.Role = newRole;
            jointHolder.RequiresSignature = newRole != JointAccountRole.ViewOnly;

            // Update limits based on role
            switch (newRole)
            {
                case JointAccountRole.SecondaryHolder:
                    jointHolder.TransactionLimit = 10000;
                    jointHolder.DailyLimit = 25000;
                    break;
                case JointAccountRole.Signatory:
                    jointHolder.TransactionLimit = 5000;
                    jointHolder.DailyLimit = 15000;
                    break;
                case JointAccountRole.ViewOnly:
                    jointHolder.TransactionLimit = 0;
                    jointHolder.DailyLimit = 0;
                    break;
                default:
                    jointHolder.TransactionLimit = null;
                    jointHolder.DailyLimit = null;
                    break;
            }

            _unitOfWork.Repository<JointAccountHolder>().Update(jointHolder);
            await _unitOfWork.SaveChangesAsync();

            await _auditLogService.LogAsync("Joint Holder Role Updated", 
                $"User {userId} role updated from {oldRole} to {newRole} for account {maskedAccountId}", updatedByUserId);
            _logger.LogInformation("Joint holder {UserId} role updated from {OldRole} to {NewRole} for account {AccountId}", 
                userId, oldRole, newRole, maskedAccountId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating joint holder role for account {AccountId}", maskedAccountId);
            return false;
        }
    }

    public async Task<List<JointAccountHolder>> GetJointHoldersAsync(Guid accountId)
    {
        try
        {
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            if (account == null)
            {
                _logger.LogWarning("Account {AccountId} not found", accountId);
                return new List<JointAccountHolder>();
            }

            return account.JointHolders.Where(jh => jh.IsActive).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving joint holders for account {AccountId}", accountId);
            return new List<JointAccountHolder>();
        }
    }

    public async Task<bool> CanUserAccessAccountAsync(Guid accountId, Guid userId)
    {
        // Delegate to AccountService to avoid duplication
        return await _accountService.CanUserAccessAccountAsync(accountId, userId);
    }

    public async Task<bool> CanUserPerformTransactionAsync(Guid accountId, Guid userId, decimal amount)
    {
        try
        {
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            if (account == null) return false;

            // Primary account holder can always perform transactions (subject to account limits)
            if (account.UserId == userId)
                return _transactionPolicy.CanDebit(account, amount);

            // Check joint holder permissions
            var jointHolder = account.JointHolders.FirstOrDefault(jh => jh.UserId == userId && jh.IsActive);
            if (jointHolder == null) return false;

            return jointHolder.CanPerformTransaction(amount) && _transactionPolicy.CanDebit(account, amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking transaction permission for account {AccountId}", accountId);
            return false;
        }
    }

    public async Task<bool> RequiresMultipleSignaturesAsync(Guid accountId, decimal amount)
    {
        try
        {
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            if (account == null) return false;

            return _jointAccountPolicy.RequiresMultipleSignaturesForAmount(account, amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking multiple signature requirement for account {AccountId}", accountId);
            return false;
        }
    }

    public async Task<List<Account>> GetAccountsForUserAsync(Guid userId)
    {
        try
        {
            var accounts = await _unitOfWork.Repository<Account>().GetAllAsync();
            
            return accounts.Where(a => 
                a.UserId == userId || // Primary holder
                a.JointHolders.Any(jh => jh.UserId == userId && jh.IsActive)) // Joint holder
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving accounts for user {UserId}", userId);
            return new List<Account>();
        }
    }

    public async Task<bool> ConvertToJointAccountAsync(Guid accountId, Guid convertedByUserId)
    {
        try
        {
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            if (account == null)
            {
                _logger.LogWarning("Account {AccountId} not found for joint account conversion", accountId);
                return false;
            }

            if (account.IsJointAccount)
            {
                _logger.LogWarning("Account {AccountId} is already a joint account", accountId);
                return false;
            }

            account.IsJointAccount = true;
            account.RequiresMultipleSignatures = false; // Can be enabled later
            account.MultipleSignatureThreshold = 10000; // Default threshold

            _unitOfWork.Repository<Account>().Update(account);
            await _unitOfWork.SaveChangesAsync();

            await _auditLogService.LogAsync("Account Converted to Joint", 
                $"Account {accountId} converted to joint account", convertedByUserId);
            _logger.LogInformation("Account {AccountId} converted to joint account", accountId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting account {AccountId} to joint account", accountId);
            return false;
        }
    }

    public async Task<bool> ConvertToSingleAccountAsync(Guid accountId, Guid remainingHolderId, Guid convertedByUserId)
    {
        try
        {
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            if (account == null)
            {
                _logger.LogWarning("Account {AccountId} not found for single account conversion", accountId);
                return false;
            }

            if (!account.IsJointAccount)
            {
                _logger.LogWarning("Account {AccountId} is not a joint account", accountId);
                return false;
            }

            // Remove all joint holders
            var activeJointHolders = account.JointHolders.Where(jh => jh.IsActive).ToList();
            foreach (var jointHolder in activeJointHolders)
            {
                jointHolder.Remove(convertedByUserId, "Account converted to single holder");
                _unitOfWork.Repository<JointAccountHolder>().Update(jointHolder);
            }

            // Update account properties
            account.IsJointAccount = false;
            account.RequiresMultipleSignatures = false;
            account.MultipleSignatureThreshold = null;
            account.MinimumSignaturesRequired = 1;

            // Update primary holder if different
            if (account.UserId != remainingHolderId)
            {
                account.UserId = remainingHolderId;
            }

            _unitOfWork.Repository<Account>().Update(account);
            await _unitOfWork.SaveChangesAsync();

            await _auditLogService.LogAsync("Account Converted to Single", 
                $"Account {accountId} converted to single holder account", convertedByUserId);
            _logger.LogInformation("Account {AccountId} converted to single holder account", accountId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting account {AccountId} to single account", accountId);
            return false;
        }
    }
    private static string MaskGuid(Guid value)
    {
        var text = value.ToString("N");
        return $"{text[..8]}***";
    }
}

