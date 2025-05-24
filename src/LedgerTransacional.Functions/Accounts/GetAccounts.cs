using System.Text.Json;

using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

using LedgerTransacional.Functions.Common;
using LedgerTransacional.Services.Implementations;
using LedgerTransacional.Services.Interfaces;

namespace LedgerTransacional.Functions.Accounts;

public class GetAccounts
{
    private readonly IAccountService _accountService;
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public GetAccounts()
    {
        var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
        var dynamoDbClient = new AmazonDynamoDBClient(Amazon.RegionEndpoint.GetBySystemName(region));
        _accountService = new AccountService(dynamoDbClient);
    }

    public GetAccounts(IAccountService accountService)
    {
        _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            context.Logger.LogLine("Processing request to list accounts");

            // Extract query string parameters
            string type = null;
            string currency = null;
            bool? isActive = null;

            if (request.QueryStringParameters != null)
            {
                // Get account type if present
                if (request.QueryStringParameters.TryGetValue("type", out var typeValue))
                {
                    type = typeValue;
                }

                // Get currency if present
                if (request.QueryStringParameters.TryGetValue("currency", out var currencyValue))
                {
                    currency = currencyValue;
                }

                // Parse isActive if present
                if (request.QueryStringParameters.TryGetValue("isActive", out var isActiveStr) &&
                    bool.TryParse(isActiveStr, out var parsedIsActive))
                {
                    isActive = parsedIsActive;
                }
            }

            // Log the parameters being used
            context.Logger.LogLine($"Filtering accounts with parameters: type={type}, currency={currency}, isActive={isActive}");

            // Get accounts with the specified filters
            var accounts = await _accountService.ListAccountsAsync(type, currency, isActive);

            context.Logger.LogLine($"Found {accounts.Count} accounts matching criteria");

            return ApiResponseBuilder.Ok(JsonSerializer.Serialize(accounts, _jsonOptions));
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Error processing request: {ex.Message}");
            context.Logger.LogLine(ex.StackTrace);
            return ApiResponseBuilder.InternalServerError("An error occurred while retrieving accounts");
        }
    }
}