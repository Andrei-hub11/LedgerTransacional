using System.Text.Json;

using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.SQS;
using Amazon.SQS.Model;

using LedgerTransacional.Functions.Common;
using LedgerTransacional.Models.DTOs.Requests;
using LedgerTransacional.Models.DTOs.Responses;
using LedgerTransacional.Services.Implementations;
using LedgerTransacional.Services.Interfaces;

namespace LedgerTransacional.Functions.Transactions;

public class CreateTransaction
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
    public CreateTransaction()
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
    public CreateTransaction(ITransactionService transactionService, IAccountService accountService, IAmazonSQS sqsClient, string transactionQueueUrl)
    {
        _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
        _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
        _sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
        _transactionQueueUrl = transactionQueueUrl ?? throw new ArgumentNullException(nameof(transactionQueueUrl));
    }

    /// <summary>
    /// Lambda function to create a new transaction
    /// </summary>
    /// <param name="request">API Gateway request</param>
    /// <param name="context">Lambda context</param>
    /// <returns>HTTP response</returns>
    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            context.Logger.LogLine("Processing request to create a new transaction");

            if (string.IsNullOrEmpty(request.Body))
            {
                return ApiResponseBuilder.BadRequest("Request body cannot be empty");
            }

            var transactionRequest = JsonSerializer.Deserialize<CreateTransactionRequest>(request.Body, _jsonOptions);

            if (transactionRequest == null)
            {
                return ApiResponseBuilder.BadRequest("Failed to deserialize request body");
            }

            var transaction = await _transactionService.CreateTransactionAsync(transactionRequest);
            context.Logger.LogLine($"Transaction created with ID: {transaction.TransactionId}");

            var entries = await _transactionService.GetTransactionEntriesAsync(transaction.TransactionId);
            context.Logger.LogLine($"Retrieved {entries.Count} entries for the transaction");

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
                transaction.TransactionId,
                transaction.ReferenceId,
                transaction.TransactionDate,
                transaction.Description,
                transaction.Status,
                transaction.TotalAmount,
                transaction.Currency,
                entryResponses,
                transaction.Metadata,
                transaction.CreatedAt
            );

            var sqsMessageBody = JsonSerializer.Serialize(transaction, _jsonOptions);

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
                                StringValue = transaction.TransactionId
                            }
                        }
                    }
            };

            await _sqsClient.SendMessageAsync(sendMessageRequest);
            context.Logger.LogLine($"Transaction {transaction.TransactionId} sent to SQS queue for processing");

            return ApiResponseBuilder.Created(JsonSerializer.Serialize(response, _jsonOptions));
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
            return ApiResponseBuilder.InternalServerError("An error occurred while processing the transaction");
        }
    }
}