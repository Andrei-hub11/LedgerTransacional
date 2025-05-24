using LedgerTransacional.Models.Entities;

namespace LedgerTransacional.UnitTests.Builders;

public class AccountBuilder
{
    private string _accountId = "acc-001";
    private string _name = "Test Account";
    private string _type = "ASSET";
    private string _currency = "USD";
    private decimal _currentBalance = 1000m;
    private bool _isActive = true;
    private DateTime _createdAt = DateTime.UtcNow.AddDays(-10);
    private DateTime _updatedAt = DateTime.UtcNow.AddDays(-5);

    public AccountBuilder WithAccountId(string accountId)
    {
        _accountId = accountId;
        return this;
    }

    public AccountBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public AccountBuilder WithType(string type)
    {
        _type = type;
        return this;
    }

    public AccountBuilder WithCurrency(string currency)
    {
        _currency = currency;
        return this;
    }

    public AccountBuilder WithCurrentBalance(decimal currentBalance)
    {
        _currentBalance = currentBalance;
        return this;
    }

    public AccountBuilder WithIsActive(bool isActive)
    {
        _isActive = isActive;
        return this;
    }

    public AccountBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public AccountBuilder WithUpdatedAt(DateTime updatedAt)
    {
        _updatedAt = updatedAt;
        return this;
    }

    public Account Build()
    {
        return new Account
        {
            AccountId = _accountId,
            Name = _name,
            Type = _type,
            Currency = _currency,
            CurrentBalance = _currentBalance,
            IsActive = _isActive,
            CreatedAt = _createdAt,
            UpdatedAt = _updatedAt
        };
    }
}