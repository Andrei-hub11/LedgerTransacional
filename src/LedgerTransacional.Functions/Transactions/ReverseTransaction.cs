using System.Text.Json;

using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.SQS;
using Amazon.SQS.Model;

using LedgerTransacional.Functions.Common;
using LedgerTransacional.Models.DTOs.Responses;
using LedgerTransacional.Services.Implementations;
using LedgerTransacional.Services.Interfaces;

namespace LedgerTransacional.Functions.Transactions;

public class ReverseTransaction
{
    private readonly ITransactionService _transactionService;
    private readonly IAccountService _accountService;
    private readonly IAmazonSQS _sqsClient;
    private readonly string _transactionQueueUrl;
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Default constructor - used by AWS Lambda runtime
    /// </summary>
    public ReverseTransaction()
    {
        var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
        var dynamoDbClient = new AmazonDynamoDBClient(Amazon.RegionEndpoint.GetBySystemName(region));
        _transactionService = new TransactionService(dynamoDbClient);
        _accountService = new AccountService(dynamoDbClient);
        _sqsClient = new AmazonSQSClient();

        _transactionQueueUrl = Environment.GetEnvironmentVariable("TRANSACTION_QUEUE_URL") ?? string.Empty;

        if (string.IsNullOrEmpty(_transactionQueueUrl))
        {
            string queueName = "TransactionQueue";
            string accountId = Environment.GetEnvironmentVariable("AWS_ACCOUNT_ID");

            if (!string.IsNullOrEmpty(accountId))
            {
                _transactionQueueUrl = $"https://sqs.{region}.amazonaws.com/{accountId}/{queueName}";
            }
            else
            {
                throw new InvalidOperationException("TRANSACTION_QUEUE_URL environment variable is not configured and AWS_ACCOUNT_ID is not available");
            }
        }
    }

    /// <summary>
    /// Constructor used for dependency injection in tests
    /// </summary>
    public ReverseTransaction(ITransactionService transactionService, IAccountService accountService, IAmazonSQS sqsClient = null, string transactionQueueUrl = null)
    {
        _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
        _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
        _sqsClient = sqsClient ?? new AmazonSQSClient();
        _transactionQueueUrl = transactionQueueUrl ?? "https://sqs.us-east-1.amazonaws.com/123456789012/TransactionQueue";
    }

    /// <summary>
    /// Lambda function to reverse an existing transaction
    /// </summary>
    /// <param name="request">API Gateway request</param>
    /// <param name="context">Lambda context</param>
    /// <returns>HTTP response</returns>
    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            context.Logger.LogLine("Processing request to reverse a transaction");

            if (!request.PathParameters.TryGetValue("transactionId", out var transactionId) || string.IsNullOrEmpty(transactionId))
            {
                return ApiResponseBuilder.BadRequest("Transaction ID is required");
            }

            string description = null;
            if (request.QueryStringParameters != null &&
                request.QueryStringParameters.TryGetValue("description", out var descParam) &&
                !string.IsNullOrEmpty(descParam))
            {
                description = descParam;
            }

            var transaction = await _transactionService.GetTransactionAsync(transactionId);
            if (transaction == null)
            {
                return ApiResponseBuilder.NotFound($"Transaction with ID {transactionId} not found");
            }

            var reversalTransaction = await _transactionService.ReverseTransactionAsync(transactionId, description);
            context.Logger.LogLine($"Transaction {transactionId} reversed. Reversal transaction ID: {reversalTransaction.TransactionId}");

            var entries = await _transactionService.GetTransactionEntriesAsync(reversalTransaction.TransactionId);
            context.Logger.LogLine($"Retrieved {entries.Count} entries for the reversal transaction");

            var entryResponses = new List<EntryResponse>();

            foreach (var entry in entries)
            {
                var account = await _accountService.GetAccountAsync(entry.AccountId);

                var entryResponse = new EntryResponse(
                    entry.EntryId,
                    entry.AccountId,
                    account.Name,
                    entry.EntryType,
                    entry.Amount,
                    entry.Description
                );

                entryResponses.Add(entryResponse);
            }

            var response = new TransactionResponse(
                reversalTransaction.TransactionId,
                reversalTransaction.ReferenceId,
                reversalTransaction.TransactionDate,
                reversalTransaction.Description,
                reversalTransaction.Status,
                reversalTransaction.TotalAmount,
                reversalTransaction.Currency,
                entryResponses,
                reversalTransaction.Metadata,
                reversalTransaction.CreatedAt
            );

            var sqsMessageBody = JsonSerializer.Serialize(reversalTransaction, _jsonOptions);

            var sendMessageRequest = new SendMessageRequest
            {
                QueueUrl = _transactionQueueUrl,
                MessageBody = sqsMessageBody,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    {
                        "TransactionId", new MessageAttributeValue
                        {
                            DataType = "String",
                            StringValue = reversalTransaction.TransactionId
                        }
                    },
                    {
                        "IsReversal", new MessageAttributeValue
                        {
                            DataType = "String",
                            StringValue = "true"
                        }
                    }
                }
            };

            await _sqsClient.SendMessageAsync(sendMessageRequest);
            context.Logger.LogLine($"Reversal transaction {reversalTransaction.TransactionId} sent to SQS queue for processing");

            return ApiResponseBuilder.Ok(JsonSerializer.Serialize(response, _jsonOptions));
        }
        catch (InvalidOperationException ex)
        {
            context.Logger.LogLine($"Invalid operation: {ex.Message}");
            return ApiResponseBuilder.BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            context.Logger.LogLine($"Validation error: {ex.Message}");
            return ApiResponseBuilder.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Error processing request: {ex.Message}");
            context.Logger.LogLine(ex.StackTrace);
            return ApiResponseBuilder.InternalServerError("An error occurred while reversing the transaction");
        }
    }
}