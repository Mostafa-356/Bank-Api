using Microsoft.AspNetCore.Identity;
using Bank.Domain.Enums;
using Bank.Domain.ValueObjects;
using Bank.Domain.Common;
using AccountEntity = Bank.Domain.Entities.Account;

namespace Bank.Domain.Entities;

/// <summary>
/// Application user entity extending ASP.NET Core Identity.
/// Uses Guid as the primary key type.
/// </summary>
public class User : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    public string FullName => $"{FirstName} {LastName}";

    public ICollection<AccountEntity> Accounts { get; set; } = new List<AccountEntity>();

    public void SoftDelete(string? deletedBy = null)
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedBy = deletedBy;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }
}

/// <summary>
/// Application role entity extending ASP.NET Core Identity.
/// Uses Guid as the primary key type.
/// </summary>
public class Role : IdentityRole<Guid>
{
    public string? Description { get; set; }
}
