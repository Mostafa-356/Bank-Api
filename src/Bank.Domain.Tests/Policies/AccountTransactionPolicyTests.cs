using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Policies.Account;
using FluentAssertions;
using Xunit;

namespace Bank.Domain.Tests.Policies;

public class AccountTransactionPolicyTests
{
    private readonly AccountTransactionPolicy _sut;

    public AccountTransactionPolicyTests()
    {
        _sut = new AccountTransactionPolicy();
    }

    [Fact]
    public void CanDebit_WhenAccountIsActiveAndHasSufficientBalance_ReturnsTrue()
    {
        // Arrange
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Type = AccountType.Checking,
            Balance = 1000m,
            Status = AccountStatus.Active
        };
        var amount = 500m;

        // Act
        var result = _sut.CanDebit(account, amount);

        // Assert
        result.Should().BeTrue("because the account is active and balance exceeds the debit amount");
    }

    [Fact]
    public void CanDebit_WhenAccountIsDormant_ReturnsFalse()
    {
        // Arrange
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Type = AccountType.Checking,
            Balance = 1000m,
            Status = AccountStatus.Dormant
        };
        var amount = 50m;

        // Act
        var result = _sut.CanDebit(account, amount);

        // Assert
        result.Should().BeFalse("because dormant accounts cannot be debited");
    }

    [Fact]
    public void CanDebit_WhenInsufficientBalance_ReturnsFalse()
    {
        // Arrange
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Type = AccountType.Checking,
            Balance = 100m,
            Status = AccountStatus.Active
        };
        var amount = 500m;

        // Act
        var result = _sut.CanDebit(account, amount);

        // Assert
        result.Should().BeFalse("because the debit amount exceeds the available balance");
    }
}
