using Bank.Domain.Entities;
using Bank.Domain.Enums;

namespace Bank.Domain.Specifications.Account;

public class ActiveAccountsSpecification : BaseSpecification<Entities.Account>
{
    public ActiveAccountsSpecification() 
        : base(a => a.Status == AccountStatus.Active && !a.HasHolds)
    {
        AddOrderByDescending(a => a.LastActivityDate);
    }
}
