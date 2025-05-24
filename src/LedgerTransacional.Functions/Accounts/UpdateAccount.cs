using System.Text.Json;

using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

using LedgerTransacional.Functions.Common;
using LedgerTransacional.Models.DTOs.Requests;
using LedgerTransacional.Services.Implementations;
using LedgerTransacional.Services.Interfaces;

namespace LedgerTransacional.Functions.Accounts;

public class UpdateAccount
{
    private readonly IAccountService _accountService;
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public UpdateAccount()
    {
        var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
        var dynamoDbClient = new AmazonDynamoDBClient(Amazon.RegionEndpoint.GetBySystemName(region));
        _accountService = new AccountService(dynamoDbClient);
    }

    public UpdateAccount(IAccountService accountService)
    {
        _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            context.Logger.LogLine("Processing request to update an account");

            // Extract account ID from the path parameters
            if (request.PathParameters == null || !request.PathParameters.TryGetValue("accountId", out var accountId))
            {
                context.Logger.LogLine("Missing accountId in path parameters");
                return ApiResponseBuilder.BadRequest("Account ID is required");
            }

            // Validate request body
            if (string.IsNullOrEmpty(request.Body))
            {
                context.Logger.LogLine("Request body is empty");
                return ApiResponseBuilder.BadRequest("Request body cannot be empty");
            }

            // Parse request body
            var updateRequest = JsonSerializer.Deserialize<CreateAccountRequest>(request.Body, _jsonOptions);
            if (updateRequest == null)
            {
                context.Logger.LogLine("Failed to deserialize request body");
                return ApiResponseBuilder.BadRequest("Invalid request format");
            }

            // Check if account exists
            var existingAccount = await _accountService.GetAccountAsync(accountId);
            if (existingAccount == null)
            {
                context.Logger.LogLine($"Account with ID {accountId} not found");
                return ApiResponseBuilder.NotFound($"Account with ID {accountId} not found");
            }

            // Update the account
            context.Logger.LogLine($"Updating account {accountId}");
            var updatedAccount = await _accountService.UpdateAccountAsync(accountId, updateRequest);

            // Return the updated account
            return ApiResponseBuilder.Ok(JsonSerializer.Serialize(updatedAccount, _jsonOptions));
        }
        catch (ArgumentException ex)
        {
            context.Logger.LogLine($"Validation error: {ex.Message}");
            return ApiResponseBuilder.BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            context.Logger.LogLine($"Not found error: {ex.Message}");
            return ApiResponseBuilder.NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Error processing request: {ex.Message}");
            context.Logger.LogLine(ex.StackTrace);
            return ApiResponseBuilder.InternalServerError("An error occurred while updating the account");
        }
    }
}