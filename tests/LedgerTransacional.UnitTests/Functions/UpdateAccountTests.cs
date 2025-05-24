using System.Text.Json;

using Amazon.Lambda.APIGatewayEvents;

using LedgerTransacional.Functions.Accounts;
using LedgerTransacional.UnitTests.Builders;
using LedgerTransacional.UnitTests.Fakes;

namespace LedgerTransacional.UnitTests.Functions;

public class UpdateAccountTests
{
    [Fact]
    public async Task FunctionHandler_ShouldUpdateAccount_WhenRequestIsValid()
    {
        // Arrange
        var accountId = "acc-001";
        var existingAccount = new AccountBuilder()
            .WithAccountId(accountId)
            .WithName("Original Account Name")
            .WithType("ASSET")
            .WithCurrency("USD")
            .WithCurrentBalance(100m)
            .WithCreatedAt(DateTime.UtcNow.AddDays(-10))
            .WithUpdatedAt(DateTime.UtcNow.AddDays(-5))
            .Build();

        var updateRequest = new CreateAccountRequestBuilder()
            .WithName("Updated Account Name")
            .WithType("ASSET")
            .WithCurrency("USD")
            .WithoutInitialBalance()
            .Build();

        var updatedAccountResponse = new AccountResponseBuilder()
            .WithAccountId(accountId)
            .WithName(updateRequest.Name)
            .WithType(updateRequest.Type)
            .WithCurrency(updateRequest.Currency)
            .WithCurrentBalance(existingAccount.CurrentBalance) // Balance should not be updated via this endpoint
            .WithCreatedAt(DateTime.UtcNow)
            .WithIsActive(true)
            .Build();

        var requestBody = JsonSerializer.Serialize(updateRequest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                { "accountId", accountId }
            },
            Body = requestBody
        };

        var fakeAccountService = new FakeAccountService();
        fakeAccountService.SetupGetAccountAsync(accountId, existingAccount);
        fakeAccountService.SetupUpdateAccountAsync(accountId, updateRequest, updatedAccountResponse);

        var function = new UpdateAccount(fakeAccountService);

        // Use FakeLambdaContext instead of mocking
        var context = new FakeLambdaContext();

        // Act
        var result = await function.FunctionHandler(apiGatewayRequest, context);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(200);
        result.Headers.ShouldContainKey("Content-Type");
        result.Headers["Content-Type"].ShouldBe("application/json");
        result.Body.ShouldNotBeNullOrWhiteSpace();
        result.Body.ShouldContain("Updated Account Name");
        result.Body.ShouldContain(accountId);
    }

    [Fact]
    public async Task FunctionHandler_ShouldReturnBadRequest_WhenRequestBodyIsEmpty()
    {
        // Arrange
        var accountId = "acc-001";
        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                { "accountId", accountId }
            },
            Body = null
        };

        var fakeAccountService = new FakeAccountService();
        var function = new UpdateAccount(fakeAccountService);

        // Use FakeLambdaContext instead of mocking
        var context = new FakeLambdaContext();

        // Act
        var result = await function.FunctionHandler(apiGatewayRequest, context);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(400);
        result.Body.ShouldContain("Request body cannot be empty");
    }

    [Fact]
    public async Task FunctionHandler_ShouldReturnBadRequest_WhenAccountIdIsMissing()
    {
        // Arrange
        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            PathParameters = null,
            Body = "{\"name\":\"Test Account\",\"type\":\"ASSET\",\"currency\":\"USD\"}"
        };

        var fakeAccountService = new FakeAccountService();
        var function = new UpdateAccount(fakeAccountService);

        // Use FakeLambdaContext instead of mocking
        var context = new FakeLambdaContext();

        // Act
        var result = await function.FunctionHandler(apiGatewayRequest, context);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(400);
        result.Body.ShouldContain("Account ID is required");
    }

    [Fact]
    public async Task FunctionHandler_ShouldReturnNotFound_WhenAccountDoesNotExist()
    {
        // Arrange
        var accountId = "acc-not-found";
        var updateRequest = new CreateAccountRequestBuilder()
            .WithName("Updated Account Name")
            .WithType("ASSET")
            .WithCurrency("USD")
            .WithoutInitialBalance()
            .Build();

        var requestBody = JsonSerializer.Serialize(updateRequest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                { "accountId", accountId }
            },
            Body = requestBody
        };

        var fakeAccountService = new FakeAccountService();
        // Not setting up GetAccountAsync means it will return null

        var function = new UpdateAccount(fakeAccountService);

        // Use FakeLambdaContext instead of mocking
        var context = new FakeLambdaContext();

        // Act
        var result = await function.FunctionHandler(apiGatewayRequest, context);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(404);
        result.Body.ShouldContain($"Account with ID {accountId} not found");
    }

    [Fact]
    public async Task FunctionHandler_ShouldReturnInternalServerError_WhenServiceThrowsException()
    {
        // Arrange
        var accountId = "acc-001";
        var existingAccount = new AccountBuilder()
            .WithAccountId(accountId)
            .WithName("Original Account Name")
            .WithType("ASSET")
            .WithCurrency("USD")
            .WithCurrentBalance(100m)
            .WithCreatedAt(DateTime.UtcNow.AddDays(-10))
            .WithUpdatedAt(DateTime.UtcNow.AddDays(-5))
            .Build();

        var updateRequest = new CreateAccountRequestBuilder()
            .WithName("Updated Account Name")
            .WithType("ASSET")
            .WithCurrency("USD")
            .WithoutInitialBalance()
            .Build();

        var requestBody = JsonSerializer.Serialize(updateRequest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                { "accountId", accountId }
            },
            Body = requestBody
        };

        var fakeAccountService = new FakeAccountService();
        fakeAccountService.SetupGetAccountAsync(accountId, existingAccount);
        fakeAccountService.SetupUpdateAccountAsyncToThrow(accountId, updateRequest, new Exception("Database connection failed"));

        var function = new UpdateAccount(fakeAccountService);

        // Use FakeLambdaContext instead of mocking
        var context = new FakeLambdaContext();

        // Act
        var result = await function.FunctionHandler(apiGatewayRequest, context);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(500);
        result.Body.ShouldContain("An error occurred while updating the account");
    }
}