using Amazon.Lambda.APIGatewayEvents;

using LedgerTransacional.Functions.Accounts;
using LedgerTransacional.Models.DTOs.Responses;
using LedgerTransacional.UnitTests.Builders;
using LedgerTransacional.UnitTests.Fakes;

namespace LedgerTransacional.UnitTests.Functions;

public class CreateAccountTests
{
    [Fact]
    public async Task FunctionHandler_ShouldReturnCreatedAccount_WhenRequestIsValid()
    {
        // Arrange
        var request = new CreateAccountRequestBuilder()
            .WithName("Test Account")
            .WithType("ASSET")
            .WithCurrency("USD")
            .WithInitialBalance(100m)
            .Build();

        var requestJson = System.Text.Json.JsonSerializer.Serialize(request);

        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            Body = requestJson
        };

        var expectedResponse = new AccountResponse(
            "acc-001",
            request.Name,
            request.Type,
            request.Currency,
            request.InitialBalance,
            DateTime.UtcNow,
            true
        );

        var fakeAccountService = new FakeAccountService();
        fakeAccountService.SetupCreateAccountAsync(request, expectedResponse);

        var lambdaContext = new FakeLambdaContext();
        var function = new CreateAccount(fakeAccountService);

        // Act
        var result = await function.FunctionHandler(apiGatewayRequest, lambdaContext);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(201);
        result.Headers.ShouldContainKey("Content-Type");
        result.Headers["Content-Type"].ShouldBe("application/json");
        result.Body.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task FunctionHandler_ShouldReturnBadRequest_WhenRequestBodyIsNull()
    {
        // Arrange
        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            Body = null
        };

        var fakeAccountService = new FakeAccountService();
        var lambdaContext = new FakeLambdaContext();
        var function = new CreateAccount(fakeAccountService);

        // Act
        var result = await function.FunctionHandler(apiGatewayRequest, lambdaContext);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(400);
        result.Body.ShouldContain("Request body is required");
    }

    [Fact]
    public async Task FunctionHandler_ShouldReturnBadRequest_WhenServiceThrowsArgumentException()
    {
        // Arrange
        var request = new CreateAccountRequestBuilder()
            .WithName("Test Account")
            .WithType("INVALID_TYPE") // Invalid type to trigger ArgumentException
            .WithCurrency("USD")
            .Build();

        var requestJson = System.Text.Json.JsonSerializer.Serialize(request);

        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            Body = requestJson
        };

        var fakeAccountService = new FakeAccountService();
        fakeAccountService.SetupCreateAccountAsyncToThrow(
            request,
            new ArgumentException("Invalid account type")
        );

        var lambdaContext = new FakeLambdaContext();
        var function = new CreateAccount(fakeAccountService);

        // Act
        var result = await function.FunctionHandler(apiGatewayRequest, lambdaContext);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(400);
        result.Body.ShouldContain("Invalid account type");
    }

    [Fact]
    public async Task FunctionHandler_ShouldReturnInternalServerError_WhenServiceThrowsException()
    {
        // Arrange
        var request = new CreateAccountRequestBuilder()
            .WithName("Test Account")
            .WithType("ASSET")
            .WithCurrency("USD")
            .Build();

        var requestJson = System.Text.Json.JsonSerializer.Serialize(request);

        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            Body = requestJson
        };

        var fakeAccountService = new FakeAccountService();
        fakeAccountService.SetupCreateAccountAsyncToThrow(
            request,
            new Exception("Database connection error")
        );

        var lambdaContext = new FakeLambdaContext();
        var function = new CreateAccount(fakeAccountService);

        // Act
        var result = await function.FunctionHandler(apiGatewayRequest, lambdaContext);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(500);
        result.Body.ShouldContain("An error occurred while creating the account");
    }
}