using System.Text.Json;

using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

using LedgerTransacional.Functions.Common;
using LedgerTransacional.Services.Implementations;
using LedgerTransacional.Services.Interfaces;

namespace LedgerTransacional.Functions.Transactions;

public class GetTransactions
{
    private readonly ITransactionService _transactionService;
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public GetTransactions()
    {
        var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
        var dynamoDbClient = new AmazonDynamoDBClient(Amazon.RegionEndpoint.GetBySystemName(region));
        _transactionService = new TransactionService(dynamoDbClient);
    }

    public GetTransactions(ITransactionService transactionService)
    {
        _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            context.Logger.LogLine("Processing request to list transactions");

            DateTime? startDate = null;
            DateTime? endDate = null;
            string status = null;
            string accountId = null;

            if (request.QueryStringParameters != null)
            {
                if (request.QueryStringParameters.TryGetValue("startDate", out var startDateStr) &&
                    DateTime.TryParse(startDateStr, out var parsedStartDate))
                {
                    startDate = parsedStartDate;
                }

                if (request.QueryStringParameters.TryGetValue("endDate", out var endDateStr) &&
                    DateTime.TryParse(endDateStr, out var parsedEndDate))
                {
                    endDate = parsedEndDate;
                }

                if (request.QueryStringParameters.TryGetValue("status", out var statusValue))
                {
                    status = statusValue;
                }

                if (request.QueryStringParameters.TryGetValue("accountId", out var accountIdValue))
                {
                    accountId = accountIdValue;
                }
            }

            context.Logger.LogLine($"Filtering transactions with parameters: startDate={startDate}, endDate={endDate}, status={status}, accountId={accountId}");

            var transactions = await _transactionService.ListTransactionsAsync(startDate, endDate, status, accountId);

            context.Logger.LogLine($"Found {transactions.Count} transactions matching criteria");

            return ApiResponseBuilder.Ok(JsonSerializer.Serialize(transactions, _jsonOptions));
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Error processing request: {ex.Message}");
            context.Logger.LogLine(ex.StackTrace);
            return ApiResponseBuilder.InternalServerError("An error occurred while retrieving transactions");
        }
    }
}