using System.Text.Json;

using Amazon.DynamoDBv2;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;

using LedgerTransacional.Models.Entities;
using LedgerTransacional.Services.Implementations;
using LedgerTransacional.Services.Interfaces;

namespace LedgerTransacional.Functions.Transactions;

public class ProcessTransaction
{
    private readonly ITransactionService _transactionService;
    private readonly IAccountService _accountService;
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ProcessTransaction()
    {
        var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
        var dynamoDbClient = new AmazonDynamoDBClient(Amazon.RegionEndpoint.GetBySystemName(region));
        _transactionService = new TransactionService(dynamoDbClient);
        _accountService = new AccountService(dynamoDbClient);
    }

    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        context.Logger.LogLine($"Beginning to process {sqsEvent.Records.Count} records...");

        foreach (var record in sqsEvent.Records)
        {
            try
            {
                context.Logger.LogLine($"Processing message {record.MessageId}");

                var transaction = JsonSerializer.Deserialize<Transaction>(record.Body, _jsonOptions);
                if (transaction == null)
                {
                    throw new InvalidOperationException("Failed to deserialize transaction from message body");
                }

                context.Logger.LogLine($"Transaction ID: {transaction.TransactionId}, Status: {transaction.Status}");

                var entries = await _transactionService.GetTransactionEntriesAsync(transaction.TransactionId);
                context.Logger.LogLine($"Found {entries.Count} entries for transaction {transaction.TransactionId}");

                foreach (var entry in entries)
                {
                    var account = await _accountService.GetAccountAsync(entry.AccountId);
                    if (account == null)
                    {
                        throw new InvalidOperationException($"Account {entry.AccountId} not found");
                    }

                    decimal balanceChange = entry.EntryType.Equals("DEBIT", StringComparison.OrdinalIgnoreCase)
                        ? -entry.Amount
                        : entry.Amount;

                    account.CurrentBalance += balanceChange;
                    account.UpdatedAt = DateTime.UtcNow;

                    await _accountService.UpdateAccountAsync(account);
                    context.Logger.LogLine($"Updated balance for account {entry.AccountId} with {balanceChange} {(entry.EntryType.Equals("DEBIT", StringComparison.OrdinalIgnoreCase) ? "debit" : "credit")}");
                }

                transaction.Status = "COMPLETED";
                transaction.UpdatedAt = DateTime.UtcNow;
                await _transactionService.UpdateTransactionStatusAsync(transaction);

                context.Logger.LogLine($"Message {record.MessageId} processed successfully");
            }
            catch (Exception ex)
            {
                context.Logger.LogLine($"Error processing message {record.MessageId}: {ex.Message}");

                try
                {
                    var failedTransaction = JsonSerializer.Deserialize<Transaction>(record.Body, _jsonOptions);
                    if (failedTransaction != null)
                    {
                        failedTransaction.Status = "FAILED";
                        failedTransaction.UpdatedAt = DateTime.UtcNow;
                        await _transactionService.UpdateTransactionStatusAsync(failedTransaction);
                    }
                }
                catch (Exception innerEx)
                {
                    context.Logger.LogLine($"Could not update failed status for message {record.MessageId}: {innerEx.Message}");
                }
            }
        }

        context.Logger.LogLine("SQS message processing complete");
    }
}