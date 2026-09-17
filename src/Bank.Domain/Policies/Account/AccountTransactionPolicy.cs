using Bank.Domain.Entities;
using Bank.Domain.Enums;
using System.Linq;

namespace Bank.Domain.Policies.Account;

public interface IAccountTransactionPolicy
{
    bool CanDebit(Entities.Account account, decimal amount);
    bool CanCredit(Entities.Account account);
}

public class AccountTransactionPolicy : IAccountTransactionPolicy
{
    public bool CanDebit(Entities.Account account, decimal amount)
    {
        return IsActive(account) && 
               !account.HasRestrictions && 
               account.Balance >= amount &&
               !account.Restrictions.Any(r => r.Type == AccountRestrictionType.NoDebits && r.IsActive);
    }
    
    public bool CanCredit(Entities.Account account)
    {
        return (account.Status == AccountStatus.Active || account.Status == AccountStatus.Inactive) && 
               !account.Restrictions.Any(r => r.Type == AccountRestrictionType.NoCredits && r.IsActive);
    }

    private bool IsActive(Entities.Account account)
    {
        return account.Status == AccountStatus.Active && !account.HasHolds;
    }
}
