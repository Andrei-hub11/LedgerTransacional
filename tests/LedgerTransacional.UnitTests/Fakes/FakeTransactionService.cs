using LedgerTransacional.Models.DTOs.Requests;
using LedgerTransacional.Models.Entities;
using LedgerTransacional.Services.Interfaces;

namespace LedgerTransacional.UnitTests.Fakes;

public class FakeTransactionService : ITransactionService
{
    private List<Transaction> _listTransactionsAsyncResult;
    private Exception _listTransactionsAsyncException;
    private DateTime? _listTransactionsStartDate;
    private DateTime? _listTransactionsEndDate;
    private string _listTransactionsStatus;
    private string _listTransactionsAccountId;

    private readonly Dictionary<string, Transaction> _getTransactionAsyncResults = new();
    private readonly Dictionary<string, Exception> _getTransactionAsyncExceptions = new();

    private readonly Dictionary<string, List<Entry>> _getTransactionEntriesAsyncResults = new();
    private readonly Dictionary<string, Exception> _getTransactionEntriesAsyncExceptions = new();

    private readonly Dictionary<string, Transaction> _createTransactionAsyncResults = new();
    private readonly Dictionary<string, Exception> _createTransactionAsyncExceptions = new();

    private readonly Dictionary<string, Transaction> _reverseTransactionAsyncResults = new();
    private readonly Dictionary<string, Exception> _reverseTransactionAsyncExceptions = new();

    public void SetupListTransactionsAsync(List<Transaction> transactions)
    {
        _listTransactionsAsyncResult = transactions;
        _listTransactionsAsyncException = null;
        _listTransactionsStartDate = null;
        _listTransactionsEndDate = null;
        _listTransactionsStatus = null;
        _listTransactionsAccountId = null;
    }

    public void SetupListTransactionsAsync(List<Transaction> transactions, DateTime? startDate, DateTime? endDate, string status, string accountId)
    {
        _listTransactionsAsyncResult = transactions;
        _listTransactionsAsyncException = null;
        _listTransactionsStartDate = startDate;
        _listTransactionsEndDate = endDate;
        _listTransactionsStatus = status;
        _listTransactionsAccountId = accountId;
    }

    public void SetupListTransactionsAsyncToThrow(Exception exception)
    {
        _listTransactionsAsyncResult = null;
        _listTransactionsAsyncException = exception;
    }

    public void SetupGetTransactionAsync(string transactionId, Transaction transaction)
    {
        _getTransactionAsyncResults[transactionId] = transaction;

        if (_getTransactionAsyncExceptions.ContainsKey(transactionId))
        {
            _getTransactionAsyncExceptions.Remove(transactionId);
        }
    }

    public void SetupGetTransactionAsyncToThrow(string transactionId, Exception exception)
    {
        _getTransactionAsyncExceptions[transactionId] = exception;

        if (_getTransactionAsyncResults.ContainsKey(transactionId))
        {
            _getTransactionAsyncResults.Remove(transactionId);
        }
    }

    public void SetupGetTransactionEntriesAsync(string transactionId, List<Entry> entries)
    {
        _getTransactionEntriesAsyncResults[transactionId] = entries;

        if (_getTransactionEntriesAsyncExceptions.ContainsKey(transactionId))
        {
            _getTransactionEntriesAsyncExceptions.Remove(transactionId);
        }
    }

    public void SetupGetTransactionEntriesAsyncToThrow(string transactionId, Exception exception)
    {
        _getTransactionEntriesAsyncExceptions[transactionId] = exception;

        if (_getTransactionEntriesAsyncResults.ContainsKey(transactionId))
        {
            _getTransactionEntriesAsyncResults.Remove(transactionId);
        }
    }

    public void SetupCreateTransactionAsync(CreateTransactionRequest request, Transaction transaction)
    {
        var key = GetCreateTransactionRequestKey(request);
        _createTransactionAsyncResults[key] = transaction;

        if (_createTransactionAsyncExceptions.ContainsKey(key))
        {
            _createTransactionAsyncExceptions.Remove(key);
        }
    }

    public void SetupCreateTransactionAsyncToThrow(CreateTransactionRequest request, Exception exception)
    {
        var key = GetCreateTransactionRequestKey(request);
        _createTransactionAsyncExceptions[key] = exception;

        if (_createTransactionAsyncResults.ContainsKey(key))
        {
            _createTransactionAsyncResults.Remove(key);
        }
    }

    public void SetupReverseTransactionAsync(string transactionId, Transaction reversalTransaction, string description = null)
    {
        var key = GetReverseTransactionKey(transactionId, description);
        _reverseTransactionAsyncResults[key] = reversalTransaction;

        if (_reverseTransactionAsyncExceptions.ContainsKey(key))
        {
            _reverseTransactionAsyncExceptions.Remove(key);
        }
    }

    public void SetupReverseTransactionAsyncToThrow(string transactionId, Exception exception, string description = null)
    {
        var key = GetReverseTransactionKey(transactionId, description);
        _reverseTransactionAsyncExceptions[key] = exception;

        if (_reverseTransactionAsyncResults.ContainsKey(key))
        {
            _reverseTransactionAsyncResults.Remove(key);
        }
    }

    // ITransactionService implementation
    public Task<Transaction> CreateTransactionAsync(CreateTransactionRequest request)
    {
        var key = GetCreateTransactionRequestKey(request);

        if (_createTransactionAsyncExceptions.TryGetValue(key, out var exception))
        {
            throw exception;
        }

        if (_createTransactionAsyncResults.TryGetValue(key, out var result))
        {
            return Task.FromResult(result);
        }

        throw new InvalidOperationException($"CreateTransactionAsync not setup for request: {key}");
    }

    public Task<Transaction> GetTransactionAsync(string transactionId)
    {
        if (string.IsNullOrEmpty(transactionId))
        {
            throw new ArgumentException("Transaction ID cannot be null or empty", nameof(transactionId));
        }

        if (_getTransactionAsyncExceptions.TryGetValue(transactionId, out var exception))
        {
            throw exception;
        }

        if (_getTransactionAsyncResults.TryGetValue(transactionId, out var transaction))
        {
            return Task.FromResult(transaction);
        }

        return Task.FromResult<Transaction>(null);
    }

    public Task<List<Entry>> GetTransactionEntriesAsync(string transactionId)
    {
        if (string.IsNullOrEmpty(transactionId))
        {
            throw new ArgumentException("Transaction ID cannot be null or empty", nameof(transactionId));
        }

        if (_getTransactionEntriesAsyncExceptions.TryGetValue(transactionId, out var exception))
        {
            throw exception;
        }

        if (_getTransactionEntriesAsyncResults.TryGetValue(transactionId, out var entries))
        {
            return Task.FromResult(entries);
        }

        return Task.FromResult(new List<Entry>());
    }

    public Task<List<Transaction>> ListTransactionsAsync(DateTime? startDate = null, DateTime? endDate = null, string status = null, string accountId = null)
    {
        if (_listTransactionsAsyncException != null)
        {
            throw _listTransactionsAsyncException;
        }

        // If we have filter parameters set up, verify they match what was requested
        if (_listTransactionsStartDate != null || _listTransactionsEndDate != null ||
            _listTransactionsStatus != null || _listTransactionsAccountId != null)
        {
            if (_listTransactionsStartDate != startDate || _listTransactionsEndDate != endDate ||
                _listTransactionsStatus != status || _listTransactionsAccountId != accountId)
            {
                return Task.FromResult(new List<Transaction>());
            }
        }

        return Task.FromResult(_listTransactionsAsyncResult ?? new List<Transaction>());
    }

    public Task UpdateTransactionStatusAsync(Transaction transaction)
    {
        // No specific setup needed for this method in the fake implementation
        return Task.CompletedTask;
    }

    public Task<Transaction> ReverseTransactionAsync(string transactionId, string description = null)
    {
        if (string.IsNullOrEmpty(transactionId))
        {
            throw new ArgumentException("Transaction ID cannot be null or empty", nameof(transactionId));
        }

        var key = GetReverseTransactionKey(transactionId, description);

        if (_reverseTransactionAsyncExceptions.TryGetValue(key, out var exception))
        {
            throw exception;
        }

        if (_reverseTransactionAsyncResults.TryGetValue(key, out var result))
        {
            return Task.FromResult(result);
        }

        throw new InvalidOperationException($"ReverseTransactionAsync not setup for transaction ID: {transactionId} and description: {description}");
    }

    // Helper methods
    private static string GetCreateTransactionRequestKey(CreateTransactionRequest request)
    {
        return $"{request.ReferenceId}|{request.Description}|{request.Currency}";
    }

    private static string GetReverseTransactionKey(string transactionId, string description)
    {
        return $"{transactionId}|{description}";
    }
}