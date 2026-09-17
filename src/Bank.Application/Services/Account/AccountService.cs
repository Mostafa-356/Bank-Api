using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Interfaces;
using Bank.Domain.Policies.Account;
using Microsoft.EntityFrameworkCore;

namespace Bank.Application.Services;

public class AccountService : IAccountService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJointAccountPolicy _jointAccountPolicy;

    public AccountService(IUnitOfWork unitOfWork, IJointAccountPolicy jointAccountPolicy)
    {
        _unitOfWork = unitOfWork;
        _jointAccountPolicy = jointAccountPolicy;
    }

    public async Task<Account?> GetAccountByIdAsync(Guid id)
    {
        return await _unitOfWork.Repository<Account>().Query()
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<Account?> GetAccountByNumberAsync(string accountNumber)
    {
        return await _unitOfWork.Repository<Account>()
            .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber);
    }

    public async Task<IEnumerable<Account>> GetUserAccountsAsync(Guid userId)
    {
        return await _unitOfWork.Repository<Account>()
            .FindAsync(a => a.UserId == userId);
    }

    public async Task<IEnumerable<Account>> GetAllAccountsAsync()
    {
        return await _unitOfWork.Repository<Account>().Query()
            .Include(a => a.User)
            .ToListAsync();
    }

    public async Task<Account> CreateAccountAsync(Guid userId, string accountHolderName)
    {
        var account = new Account
        {
            AccountNumber = GenerateAccountNumber(),
            AccountHolderName = accountHolderName,
            UserId = userId,
            Balance = 0
        };

        await _unitOfWork.Repository<Account>().AddAsync(account);
        await _unitOfWork.SaveChangesAsync();
        return account;
    }

    public async Task<bool> UpdateBalanceAsync(Guid accountId, decimal amount)
    {
        var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
        if (account == null) return false;

        account.Balance += amount;
        _unitOfWork.Repository<Account>().Update(account);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CanUserAccessAccountAsync(Guid accountId, Guid userId)
    {
        var account = await _unitOfWork.Repository<Account>().Query()
            .Include(a => a.JointHolders)
            .FirstOrDefaultAsync(a => a.Id == accountId);
        
        if (account == null) return false;
        
        return _jointAccountPolicy.CanUserAccess(account, userId);
    }

    public async Task<bool> UpdateAccountAsync(Account account)
    {
        try
        {
            _unitOfWork.Repository<Account>().Update(account);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string GenerateAccountNumber()
    {
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        var randomNumber = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 90000000 + 10000000;
        return $"BNK-{DateTime.UtcNow:yyyyMMdd}-{randomNumber}";
    }
}
