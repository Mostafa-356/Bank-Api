using Bank.Domain.Entities;
using Bank.Domain.Enums;
using System;

namespace Bank.Domain.Policies.Account;

public interface IAccountDormancyPolicy
{
    bool IsDormant(Entities.Account account);
    void MarkAsDormant(Entities.Account account);
    void UpdateActivity(Entities.Account account);
}

public class AccountDormancyPolicy : IAccountDormancyPolicy
{
    public bool IsDormant(Entities.Account account)
    {
        return account.Status == AccountStatus.Dormant || 
               (account.Status == AccountStatus.Active && 
                DateTime.UtcNow.Subtract(account.LastActivityDate).TotalDays >= account.DormancyPeriodDays);
    }
    
    public void UpdateActivity(Entities.Account account)
    {
        account.LastActivityDate = DateTime.UtcNow;
        if (account.Status == AccountStatus.Dormant)
        {
            account.Status = AccountStatus.Active;
            account.DormancyDate = null;
        }
    }
    
    public void MarkAsDormant(Entities.Account account)
    {
        if (account.Status == AccountStatus.Active)
        {
            account.Status = AccountStatus.Dormant;
            account.DormancyDate = DateTime.UtcNow;
        }
    }
}
