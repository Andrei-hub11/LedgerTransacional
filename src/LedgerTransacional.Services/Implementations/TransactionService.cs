using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;

using LedgerTransacional.Models.DTOs;
using LedgerTransacional.Models.DTOs.Requests;
using LedgerTransacional.Models.Entities;
using LedgerTransacional.Services.Interfaces;

namespace LedgerTransacional.Services.Implementations;

public class TransactionService : ITransactionService
{
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly IDynamoDBContext _dynamoDbContext;

    public TransactionService(IAmazonDynamoDB dynamoDbClient)
    {
        _dynamoDbClient = dynamoDbClient ?? throw new ArgumentNullException(nameof(dynamoDbClient));
        _dynamoDbContext = new DynamoDBContextBuilder().WithDynamoDBClient(() => dynamoDbClient).Build();
    }

    public async Task<Transaction> CreateTransactionAsync(CreateTransactionRequest request)
    {
        ValidateCreateTransactionRequest(request);

        var transactionId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;

        decimal totalAmount = request.Entries
            .Where(e => e.EntryType.Equals("DEBIT", StringComparison.OrdinalIgnoreCase))
            .Sum(e => e.Amount);

        var transaction = new Transaction
        {
            TransactionId = transactionId,
            ReferenceId = request.ReferenceId,
            TransactionDate = timestamp,
            Description = request.Description,
            Status = "PENDING",
            TotalAmount = totalAmount,
            Currency = request.Currency,
            Metadata = request.Metadata ?? [],
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        };

        var entries = new List<Entry>();
        foreach (var entryDto in request.Entries)
        {
            var entry = new Entry
            {
                EntryId = Guid.NewGuid().ToString(),
                TransactionId = transactionId,
                AccountId = entryDto.AccountId,
                EntryType = entryDto.EntryType.ToUpper(),
                Amount = entryDto.Amount,
                Description = entryDto.Description ?? transaction.Description,
                CreatedAt = timestamp
            };
            entries.Add(entry);
        }

        // Start a DynamoDB transaction to save everything atomically
        var transactionConfig = new TransactWriteItemsRequest
        {
            TransactItems = new List<TransactWriteItem>()
        };

        var transactionItem = _dynamoDbContext.ToDocument(transaction);
        transactionConfig.TransactItems.Add(new TransactWriteItem
        {
            Put = new Put
            {
                TableName = "Transactions",
                Item = transactionItem.ToAttributeMap()
            }
        });

        foreach (var entry in entries)
        {
            var entryItem = _dynamoDbContext.ToDocument(entry);
            transactionConfig.TransactItems.Add(new TransactWriteItem
            {
                Put = new Put
                {
                    TableName = "Entries",
                    Item = entryItem.ToAttributeMap()
                }
            });
        }

        await _dynamoDbClient.TransactWriteItemsAsync(transactionConfig);

        return transaction;
    }

    public async Task<Transaction> GetTransactionAsync(string transactionId)
    {
        if (string.IsNullOrEmpty(transactionId))
            throw new ArgumentException("Transaction ID cannot be null or empty", nameof(transactionId));

        return await _dynamoDbContext.LoadAsync<Transaction>(transactionId);
    }

    public async Task<List<Entry>> GetTransactionEntriesAsync(string transactionId)
    {
        if (string.IsNullOrEmpty(transactionId))
            throw new ArgumentException("Transaction ID cannot be null or empty", nameof(transactionId));

        var conditions = new List<ScanCondition>
            {
                new ScanCondition("TransactionId", ScanOperator.Equal, transactionId)
            };

        var search = _dynamoDbContext.ScanAsync<Entry>(conditions);
        return await search.GetRemainingAsync();
    }

    public async Task<List<Transaction>> ListTransactionsAsync(DateTime? startDate = null, DateTime? endDate = null, string status = null, string accountId = null)
    {
        var scanConditions = new List<ScanCondition>();

        if (status != null)
        {
            scanConditions.Add(new ScanCondition("Status", ScanOperator.Equal, status));
        }

        var allTransactions = await _dynamoDbContext.ScanAsync<Transaction>(scanConditions).GetRemainingAsync();

        if (startDate.HasValue)
        {
            allTransactions = allTransactions.Where(t => t.TransactionDate >= startDate.Value).ToList();
        }

        if (endDate.HasValue)
        {
            allTransactions = allTransactions.Where(t => t.TransactionDate <= endDate.Value).ToList();
        }

        if (!string.IsNullOrEmpty(accountId))
        {
            var accountTransactionIds = new HashSet<string>();

            var entriesConditions = new List<ScanCondition>
                {
                    new ScanCondition("AccountId", ScanOperator.Equal, accountId)
                };

            var accountEntries = await _dynamoDbContext.ScanAsync<Entry>(entriesConditions).GetRemainingAsync();

            foreach (var entry in accountEntries)
            {
                accountTransactionIds.Add(entry.TransactionId);
            }

            allTransactions = allTransactions.Where(t => accountTransactionIds.Contains(t.TransactionId)).ToList();
        }

        return allTransactions;
    }

    public async Task UpdateTransactionStatusAsync(Transaction transaction)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));

        transaction.UpdatedAt = DateTime.UtcNow;
        await _dynamoDbContext.SaveAsync(transaction);
    }

    public async Task<Transaction> ReverseTransactionAsync(string transactionId, string description = null)
    {
        var originalTransaction = await GetTransactionAsync(transactionId);
        if (originalTransaction == null)
            throw new InvalidOperationException($"Transaction {transactionId} not found");

        if (originalTransaction.Status != "COMPLETED")
            throw new InvalidOperationException($"Cannot reverse a transaction with status {originalTransaction.Status}");

        var originalEntries = await GetTransactionEntriesAsync(transactionId);
        if (!originalEntries.Any())
            throw new InvalidOperationException($"No entries found for transaction {transactionId}");

        var reverseRequest = new CreateTransactionRequest
        (
            ReferenceId: $"REVERSE-{originalTransaction.TransactionId}",
            Description: description ?? $"Reversal of transaction {originalTransaction.TransactionId}",
            Currency: originalTransaction.Currency,
            Entries: [],
            Metadata: new Dictionary<string, string>
                {
                    { "OriginalTransactionId", originalTransaction.TransactionId },
                    { "ReverseOperation", "true" }
                }
        );

        if (originalTransaction.Metadata != null)
        {
            foreach (var meta in originalTransaction.Metadata)
            {
                if (!reverseRequest.Metadata.ContainsKey(meta.Key))
                {
                    reverseRequest.Metadata.Add(meta.Key, meta.Value);
                }
            }
        }

        foreach (var originalEntry in originalEntries)
        {
            var reverseType = originalEntry.EntryType.Equals("DEBIT", StringComparison.OrdinalIgnoreCase)
                ? "CREDIT"
                : "DEBIT";

            reverseRequest.Entries.Add(new TransactionEntryDto(
                originalEntry.AccountId,
                reverseType,
                originalEntry.Amount,
                $"Reversal: {originalEntry.Description}"
            ));
        }

        return await CreateTransactionAsync(reverseRequest);
    }

    private void ValidateCreateTransactionRequest(CreateTransactionRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrEmpty(request.Description))
            throw new ArgumentException("Transaction description is required", nameof(request.Description));

        if (string.IsNullOrEmpty(request.Currency))
            throw new ArgumentException("Transaction currency is required", nameof(request.Currency));

        if (request.Entries == null || request.Entries.Count == 0)
            throw new ArgumentException("Transaction must have at least one entry", nameof(request.Entries));

        if (request.Entries.Count < 2)
            throw new ArgumentException("Transaction must have at least two entries to maintain accounting balance", nameof(request.Entries));

        foreach (var entry in request.Entries)
        {
            if (string.IsNullOrEmpty(entry.AccountId))
                throw new ArgumentException("AccountId is required for all entries");

            if (string.IsNullOrEmpty(entry.EntryType))
                throw new ArgumentException("EntryType is required for all entries");

            if (!entry.EntryType.Equals("DEBIT", StringComparison.OrdinalIgnoreCase) &&
                !entry.EntryType.Equals("CREDIT", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Invalid EntryType: {entry.EntryType}. Must be 'DEBIT' or 'CREDIT'");

            if (entry.Amount <= 0)
                throw new ArgumentException("Entry amount must be greater than zero");
        }

        decimal totalDebits = request.Entries
            .Where(e => e.EntryType.Equals("DEBIT", StringComparison.OrdinalIgnoreCase))
            .Sum(e => e.Amount);

        decimal totalCredits = request.Entries
            .Where(e => e.EntryType.Equals("CREDIT", StringComparison.OrdinalIgnoreCase))
            .Sum(e => e.Amount);

        if (Math.Abs(totalDebits - totalCredits) > 0.001m)
        {
            throw new ArgumentException($"Transaction is not balanced. Total debits: {totalDebits}, Total credits: {totalCredits}");
        }
    }
}