using LedgerTransacional.Models.DTOs.Requests;
using LedgerTransacional.Models.DTOs.Responses;
using LedgerTransacional.Models.Entities;


namespace LedgerTransacional.Services.Interfaces;

public interface IAccountService
{
    /// <summary>
    /// Creates a new account
    /// </summary>
    /// <param name="request">Account creation information</param>
    /// <returns>Created account</returns>
    Task<AccountResponse> CreateAccountAsync(CreateAccountRequest request);

    /// <summary>
    /// Gets an account by ID
    /// </summary>
    /// <param name="accountId">Account ID</param>
    /// <returns>Account, if found</returns>
    Task<Account> GetAccountAsync(string accountId);

    /// <summary>
    /// Gets an account by ID and returns as DTO
    /// </summary>
    /// <param name="accountId">Account ID</param>
    /// <returns>Account DTO, if found</returns>
    Task<AccountResponse> GetAccountResponseAsync(string accountId);

    /// <summary>
    /// Lists accounts with optional filtering
    /// </summary>
    /// <param name="type">Account type (optional)</param>
    /// <param name="currency">Account currency (optional)</param>
    /// <param name="isActive">Account activation status (optional)</param>
    /// <returns>List of accounts matching the filters</returns>
    Task<List<AccountResponse>> ListAccountsAsync(string? type = null, string? currency = null, bool? isActive = null);

    /// <summary>
    /// Updates an existing account
    /// </summary>
    /// <param name="accountId">Account ID</param>
    /// <param name="request">Update information</param>
    /// <returns>Updated account</returns>
    Task<AccountResponse> UpdateAccountAsync(string accountId, CreateAccountRequest request);

    /// <summary>
    /// Updates an account's balance
    /// </summary>
    /// <param name="account">Account with updated balance</param>
    /// <returns>Completed operation</returns>
    Task UpdateAccountAsync(Account account);

    /// <summary>
    /// Deactivates an account
    /// </summary>
    /// <param name="accountId">Account ID</param>
    /// <returns>Completed operation</returns>
    Task<bool> DeactivateAccountAsync(string accountId);
}