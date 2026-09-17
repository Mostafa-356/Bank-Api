using Bank.Domain.Entities;
using System;
using System.Linq;

namespace Bank.Domain.Policies.Account;

public interface IJointAccountPolicy
{
    bool HasJointHolder(Entities.Account account, Guid userId);
    bool CanUserAccess(Entities.Account account, Guid userId);
    bool RequiresMultipleSignaturesForAmount(Entities.Account account, decimal amount);
    int GetActiveJointHoldersCount(Entities.Account account);
}

public class JointAccountPolicy : IJointAccountPolicy
{
    public bool HasJointHolder(Entities.Account account, Guid userId)
    {
        return account.IsJointAccount && account.JointHolders.Any(jh => jh.UserId == userId && jh.IsActive);
    }
    
    public bool CanUserAccess(Entities.Account account, Guid userId)
    {
        if (account.UserId == userId) return true; // Primary account holder
        return HasJointHolder(account, userId);
    }
    
    public bool RequiresMultipleSignaturesForAmount(Entities.Account account, decimal amount)
    {
        return account.IsJointAccount && 
               account.RequiresMultipleSignatures && 
               account.MultipleSignatureThreshold.HasValue && 
               amount >= account.MultipleSignatureThreshold.Value;
    }
    
    public int GetActiveJointHoldersCount(Entities.Account account)
    {
        return account.JointHolders.Count(jh => jh.IsActive) + 1; // +1 for primary holder
    }
}
