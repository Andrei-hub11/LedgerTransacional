using LedgerTransacional.Models.DTOs.Requests;

namespace LedgerTransacional.UnitTests.Builders;

public class CreateAccountRequestBuilder
{
    private string _name = "Test Account";
    private string _type = "ASSET";
    private string _currency = "USD";
    private decimal _initialBalance = 100.0m;
    private bool _hasInitialBalance = true;

    public CreateAccountRequestBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public CreateAccountRequestBuilder WithType(string type)
    {
        _type = type;
        return this;
    }

    public CreateAccountRequestBuilder WithCurrency(string currency)
    {
        _currency = currency;
        return this;
    }

    public CreateAccountRequestBuilder WithInitialBalance(decimal initialBalance)
    {
        _initialBalance = initialBalance;
        _hasInitialBalance = true;
        return this;
    }

    public CreateAccountRequestBuilder WithoutInitialBalance()
    {
        _hasInitialBalance = false;
        return this;
    }

    public CreateAccountRequest Build()
    {
        if (_hasInitialBalance)
        {
            return new CreateAccountRequest(
                Name: _name,
                Type: _type,
                Currency: _currency,
                InitialBalance: _initialBalance
            );
        }
        else
        {
            return new CreateAccountRequest(
                Name: _name,
                Type: _type,
                Currency: _currency
            );
        }
    }
}