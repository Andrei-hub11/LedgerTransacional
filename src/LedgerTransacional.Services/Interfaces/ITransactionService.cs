using LedgerTransacional.Models.DTOs.Requests;
using LedgerTransacional.Models.Entities;

namespace LedgerTransacional.Services.Interfaces;

public interface ITransactionService
{
    /// <summary>
    /// Creates a new transaction and its related entries
    /// </summary>
    /// <param name="request">Transaction data to be created</param>
    /// <returns>The created transaction</returns>
    Task<Transaction> CreateTransactionAsync(CreateTransactionRequest request);

    /// <summary>
    /// Finds a specific transaction by ID
    /// </summary>
    /// <param name="transactionId">Transaction ID</param>
    /// <returns>The found transaction or null</returns>
    Task<Transaction> GetTransactionAsync(string transactionId);

    /// <summary>
    /// Finds all entries related to a transaction
    /// </summary>
    /// <param name="transactionId">Transaction ID</param>
    /// <returns>List of entries</returns>
    Task<List<Entry>> GetTransactionEntriesAsync(string transactionId);

    /// <summary>
    /// Lists transactions with optional filters
    /// </summary>
    /// <param name="startDate">Start date (optional)</param>
    /// <param name="endDate">End date (optional)</param>
    /// <param name="status">Transaction status (optional)</param>
    /// <param name="accountId">Related account ID (optional)</param>
    /// <returns>List of filtered transactions</returns>
    Task<List<Transaction>> ListTransactionsAsync(DateTime? startDate = null, DateTime? endDate = null, string status = null, string accountId = null);

    /// <summary>
    /// Updates a transaction status
    /// </summary>
    /// <param name="transaction">Transaction to be updated</param>
    Task UpdateTransactionStatusAsync(Transaction transaction);

    /// <summary>
    /// Creates a reversal transaction to reverse an existing transaction
    /// </summary>
    /// <param name="transactionId">ID of the transaction to be reversed</param>
    /// <param name="description">Optional description for the reversal</param>
    /// <returns>The created reversal transaction</returns>
    Task<Transaction> ReverseTransactionAsync(string transactionId, string description = null);
}