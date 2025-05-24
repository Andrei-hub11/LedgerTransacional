using LedgerTransacional.Models.DTOs.Responses;

namespace LedgerTransacional.UnitTests.Builders;

public class AccountResponseBuilder
{
    private string _accountId = "acc-001";
    private string _name = "Test Account";
    private string _type = "ASSET";
    private string _currency = "USD";
    private decimal _currentBalance = 1000m;
    private DateTime _createdAt = DateTime.UtcNow;
    private bool _isActive = true;

    public AccountResponseBuilder WithAccountId(string accountId)
    {
        _accountId = accountId;
        return this;
    }

    public AccountResponseBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public AccountResponseBuilder WithType(string type)
    {
        _type = type;
        return this;
    }

    public AccountResponseBuilder WithCurrency(string currency)
    {
        _currency = currency;
        return this;
    }

    public AccountResponseBuilder WithCurrentBalance(decimal currentBalance)
    {
        _currentBalance = currentBalance;
        return this;
    }

    public AccountResponseBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public AccountResponseBuilder WithIsActive(bool isActive)
    {
        _isActive = isActive;
        return this;
    }

    public AccountResponse Build()
    {
        return new AccountResponse(
            AccountId: _accountId,
            Name: _name,
            Type: _type,
            Currency: _currency,
            CurrentBalance: _currentBalance,
            CreatedAt: _createdAt,
            IsActive: _isActive
        );
    }
}