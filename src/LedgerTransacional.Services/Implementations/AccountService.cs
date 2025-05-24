using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;

using LedgerTransacional.Common.Constants;
using LedgerTransacional.Models.DTOs.Requests;
using LedgerTransacional.Models.DTOs.Responses;
using LedgerTransacional.Models.Entities;
using LedgerTransacional.Services.Interfaces;


namespace LedgerTransacional.Services.Implementations;

/// <summary>
/// Account management service implementation
/// </summary>
public class AccountService : IAccountService
{
    private readonly IDynamoDBContext _dynamoDbContext;

    /// <summary>
    /// Constructor that receives DynamoDB client
    /// </summary>
    /// <param name="dynamoDbClient">DynamoDB client</param>
    public AccountService(IAmazonDynamoDB dynamoDbClient)
    {
        _dynamoDbContext = new DynamoDBContextBuilder().WithDynamoDBClient(() => dynamoDbClient).Build();
    }

    /// <summary>
    /// Creates a new account
    /// </summary>
    public async Task<AccountResponse> CreateAccountAsync(CreateAccountRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Account name is required", nameof(request.Name));

        if (string.IsNullOrWhiteSpace(request.Type))
            throw new ArgumentException("Account type is required", nameof(request.Type));

        if (string.IsNullOrWhiteSpace(request.Currency))
            throw new ArgumentException("Account currency is required", nameof(request.Currency));

        if (!AccountTypes.IsValidType(request.Type))
            throw new ArgumentException($"Invalid account type. Allowed values: {string.Join(", ", AccountTypes.GetAllTypes())}", nameof(request.Type));

        var account = new Account
        {
            AccountId = Guid.NewGuid().ToString(),
            Name = request.Name,
            Type = request.Type,
            Currency = request.Currency.ToUpperInvariant(),
            CurrentBalance = request.InitialBalance,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _dynamoDbContext.SaveAsync(account);

        return MapToAccountResponse(account);
    }

    /// <summary>
    /// Gets an account by ID
    /// </summary>
    public async Task<Account> GetAccountAsync(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            throw new ArgumentException("Account ID is required", nameof(accountId));

        return await _dynamoDbContext.LoadAsync<Account>(accountId);
    }

    /// <summary>
    /// Gets an account by ID and returns it as DTO
    /// </summary>
    public async Task<AccountResponse> GetAccountResponseAsync(string accountId)
    {
        var account = await GetAccountAsync(accountId);

        if (account == null)
            return null;

        return MapToAccountResponse(account);
    }

    /// <summary>
    /// Lists accounts with optional filtering
    /// </summary>
    public async Task<List<AccountResponse>> ListAccountsAsync(string type = null, string currency = null, bool? isActive = null)
    {
        var accounts = new List<Account>();
        var scan = CreateScanConditions(type, currency, isActive);

        var search = _dynamoDbContext.FromScanAsync<Account>(scan);

        do
        {
            var page = await search.GetNextSetAsync();
            accounts.AddRange(page);
        }
        while (!search.IsDone);

        return [.. accounts.Select(MapToAccountResponse)];
    }

    /// <summary>
    /// Updates an existing account
    /// </summary>
    public async Task<AccountResponse> UpdateAccountAsync(string accountId, CreateAccountRequest request)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            throw new ArgumentException("Account ID is required", nameof(accountId));

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Account name is required", nameof(request.Name));

        if (string.IsNullOrWhiteSpace(request.Type))
            throw new ArgumentException("Account type is required", nameof(request.Type));

        if (string.IsNullOrWhiteSpace(request.Currency))
            throw new ArgumentException("Account currency is required", nameof(request.Currency));

        if (!AccountTypes.IsValidType(request.Type))
            throw new ArgumentException($"Invalid account type. Allowed values: {string.Join(", ", AccountTypes.GetAllTypes())}", nameof(request.Type));

        var account = await GetAccountAsync(accountId);

        if (account == null)
            throw new KeyNotFoundException($"Account with ID {accountId} not found");

        if (!account.IsActive)
            throw new InvalidOperationException($"Account with ID {accountId} is inactive");

        account.Name = request.Name;
        account.Type = request.Type;
        account.Currency = request.Currency.ToUpperInvariant();
        account.UpdatedAt = DateTime.UtcNow;

        await _dynamoDbContext.SaveAsync(account);

        return MapToAccountResponse(account);
    }

    /// <summary>
    /// Updates an account's balance
    /// </summary>
    public async Task UpdateAccountAsync(Account account)
    {
        if (account == null)
            throw new ArgumentNullException(nameof(account));

        await _dynamoDbContext.SaveAsync(account);
    }

    /// <summary>
    /// Deactivates an account
    /// </summary>
    public async Task<bool> DeactivateAccountAsync(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            throw new ArgumentException("Account ID is required", nameof(accountId));

        var account = await GetAccountAsync(accountId);

        if (account == null)
            throw new KeyNotFoundException($"Account with ID {accountId} not found");

        if (!account.IsActive)
            return true;

        account.IsActive = false;
        account.UpdatedAt = DateTime.UtcNow;

        await _dynamoDbContext.SaveAsync(account);

        return true;
    }

    #region Helper Methods

    /// <summary>
    /// Maps Account entity to AccountResponse DTO
    /// </summary>
    private static AccountResponse MapToAccountResponse(Account account)
    {
        if (account == null)
            return null;

        return new AccountResponse(
            account.AccountId,
            account.Name,
            account.Type,
            account.Currency,
            account.CurrentBalance,
            account.CreatedAt,
            account.IsActive
        );
    }

    /// <summary>
    /// Creates scan conditions for DynamoDB based on filters
    /// </summary>
    private static ScanOperationConfig CreateScanConditions(string? type, string? currency, bool? isActive)
    {
        var scan = new ScanOperationConfig();
        var scanFilter = new ScanFilter();

        if (!string.IsNullOrWhiteSpace(type))
        {
            scanFilter.AddCondition("Type", ScanOperator.Equal, type);
        }

        if (!string.IsNullOrWhiteSpace(currency))
        {
            scanFilter.AddCondition("Currency", ScanOperator.Equal, currency.ToUpperInvariant());
        }

        if (isActive.HasValue)
        {
            scanFilter.AddCondition("IsActive", ScanOperator.Equal, isActive.Value);
        }

        if (scanFilter.ToConditions().Count > 0)
        {
            scan.Filter = scanFilter;
        }

        return scan;
    }

    #endregion
}