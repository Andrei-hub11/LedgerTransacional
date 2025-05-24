using LedgerTransacional.Models.DTOs.Requests;
using LedgerTransacional.Models.DTOs.Responses;
using LedgerTransacional.Models.Entities;
using LedgerTransacional.Services.Interfaces;

namespace LedgerTransacional.UnitTests.Fakes;

public class FakeAccountService : IAccountService
{
    private List<AccountResponse> _listAccountsAsyncResult;
    private Exception _listAccountsAsyncException;
    private string _listAccountsType;
    private string _listAccountsCurrency;
    private bool? _listAccountsIsActive;

    private readonly Dictionary<string, AccountResponse> _createAccountAsyncResults = new();
    private readonly Dictionary<string, Exception> _createAccountAsyncExceptions = new();

    private readonly Dictionary<string, Account> _getAccountAsyncResults = new();
    private readonly Dictionary<string, Exception> _getAccountAsyncExceptions = new();

    private readonly Dictionary<string, AccountResponse> _updateAccountAsyncResults = new();
    private readonly Dictionary<string, Exception> _updateAccountAsyncExceptions = new();

    public void SetupListAccountsAsync(List<AccountResponse> accounts)
    {
        _listAccountsAsyncResult = accounts;
        _listAccountsAsyncException = null;
        _listAccountsType = null;
        _listAccountsCurrency = null;
        _listAccountsIsActive = null;
    }

    public void SetupListAccountsAsync(List<AccountResponse> accounts, string type, string currency, bool? isActive)
    {
        _listAccountsAsyncResult = accounts;
        _listAccountsAsyncException = null;
        _listAccountsType = type;
        _listAccountsCurrency = currency;
        _listAccountsIsActive = isActive;
    }

    public void SetupListAccountsAsyncToThrow(Exception exception)
    {
        _listAccountsAsyncResult = null;
        _listAccountsAsyncException = exception;
    }

    public void SetupCreateAccountAsync(CreateAccountRequest request, AccountResponse response)
    {
        var key = GetCreateAccountRequestKey(request);
        _createAccountAsyncResults[key] = response;

        if (_createAccountAsyncExceptions.ContainsKey(key))
        {
            _createAccountAsyncExceptions.Remove(key);
        }
    }

    public void SetupCreateAccountAsyncToThrow(CreateAccountRequest request, Exception exception)
    {
        var key = GetCreateAccountRequestKey(request);
        _createAccountAsyncExceptions[key] = exception;

        if (_createAccountAsyncResults.ContainsKey(key))
        {
            _createAccountAsyncResults.Remove(key);
        }
    }

    public void SetupGetAccountAsync(string accountId, Account account)
    {
        _getAccountAsyncResults[accountId] = account;

        if (_getAccountAsyncExceptions.ContainsKey(accountId))
        {
            _getAccountAsyncExceptions.Remove(accountId);
        }
    }

    public void SetupGetAccountAsyncToThrow(string accountId, Exception exception)
    {
        _getAccountAsyncExceptions[accountId] = exception;

        if (_getAccountAsyncResults.ContainsKey(accountId))
        {
            _getAccountAsyncResults.Remove(accountId);
        }
    }

    public void SetupUpdateAccountAsync(string accountId, CreateAccountRequest request, AccountResponse response)
    {
        var key = $"{accountId}_{request.Name}_{request.Type}_{request.Currency}";
        _updateAccountAsyncResults[key] = response;

        if (_updateAccountAsyncExceptions.ContainsKey(key))
        {
            _updateAccountAsyncExceptions.Remove(key);
        }
    }

    public void SetupUpdateAccountAsyncToThrow(string accountId, CreateAccountRequest request, Exception exception)
    {
        var key = $"{accountId}_{request.Name}_{request.Type}_{request.Currency}";
        _updateAccountAsyncExceptions[key] = exception;

        if (_updateAccountAsyncResults.ContainsKey(key))
        {
            _updateAccountAsyncResults.Remove(key);
        }
    }

    // IAccountService implementation
    public Task<AccountResponse> CreateAccountAsync(CreateAccountRequest request)
    {
        var key = GetCreateAccountRequestKey(request);

        if (_createAccountAsyncExceptions.TryGetValue(key, out var exception))
        {
            throw exception;
        }

        if (_createAccountAsyncResults.TryGetValue(key, out var result))
        {
            return Task.FromResult(result);
        }

        throw new InvalidOperationException($"CreateAccountAsync not setup for request: {key}");
    }

    public Task<Account> GetAccountAsync(string accountId)
    {
        if (_getAccountAsyncExceptions.TryGetValue(accountId, out var exception))
        {
            throw exception;
        }

        if (_getAccountAsyncResults.TryGetValue(accountId, out var account))
        {
            return Task.FromResult(account);
        }

        return Task.FromResult<Account>(null);
    }

    public Task<AccountResponse> GetAccountResponseAsync(string accountId)
    {
        throw new NotImplementedException("GetAccountResponseAsync not implemented in fake");
    }

    public Task<List<AccountResponse>> ListAccountsAsync(string type = null, string currency = null, bool? isActive = null)
    {
        if (_listAccountsAsyncException != null)
        {
            throw _listAccountsAsyncException;
        }

        // If we have filter parameters set up, verify they match what was requested
        if (_listAccountsType != null || _listAccountsCurrency != null || _listAccountsIsActive != null)
        {
            if (_listAccountsType != type || _listAccountsCurrency != currency || _listAccountsIsActive != isActive)
            {
                return Task.FromResult(new List<AccountResponse>());
            }
        }

        return Task.FromResult(_listAccountsAsyncResult ?? new List<AccountResponse>());
    }

    public Task<AccountResponse> UpdateAccountAsync(string accountId, CreateAccountRequest request)
    {
        var key = $"{accountId}_{request.Name}_{request.Type}_{request.Currency}";

        if (_updateAccountAsyncExceptions.TryGetValue(key, out var exception))
        {
            throw exception;
        }

        if (_updateAccountAsyncResults.TryGetValue(key, out var result))
        {
            return Task.FromResult(result);
        }

        throw new InvalidOperationException($"UpdateAccountAsync not setup for request: {key}");
    }

    public Task UpdateAccountAsync(Account account)
    {
        return Task.CompletedTask;
    }

    public Task<bool> DeactivateAccountAsync(string accountId)
    {
        throw new NotImplementedException("DeactivateAccountAsync not implemented in fake");
    }

    // Helper methods
    private static string GetCreateAccountRequestKey(CreateAccountRequest request)
    {
        return $"{request.Name}|{request.Type}|{request.Currency}|{request.InitialBalance}";
    }
}