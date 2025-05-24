using Amazon.Lambda.APIGatewayEvents;

using LedgerTransacional.Functions.Accounts;
using LedgerTransacional.Models.DTOs.Responses;
using LedgerTransacional.UnitTests.Builders;
using LedgerTransacional.UnitTests.Fakes;

namespace LedgerTransacional.UnitTests.Functions;

public class GetAccountsTests
{
    [Fact]
    public async Task FunctionHandler_ShouldReturnAccounts_WhenServiceReturnsAccounts()
    {
        // Arrange
        var accounts = new List<AccountResponse>
        {
            new AccountResponseBuilder()
                .WithAccountId("acc-001")
                .WithName("Test Account 1")
                .WithType("ASSET")
                .WithCurrency("USD")
                .WithCurrentBalance(100m)
                .WithCreatedAt(DateTime.UtcNow)
                .WithIsActive(true)
                .Build(),
            new AccountResponseBuilder()
                .WithAccountId("acc-002")
                .WithName("Test Account 2")
                .WithType("LIABILITY")
                .WithCurrency("EUR")
                .WithCurrentBalance(200m)
                .WithCreatedAt(DateTime.UtcNow)
                .WithIsActive(true)
                .Build()
        };

        var fakeAccountService = new FakeAccountService();
        fakeAccountService.SetupListAccountsAsync(accounts);

        var function = new GetAccounts(fakeAccountService);
        var request = new APIGatewayProxyRequest();

        // Use FakeLambdaContext instead of mocking
        var context = new FakeLambdaContext();

        // Act
        var result = await function.FunctionHandler(request, context);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(200);
        result.Headers.ShouldContainKey("Content-Type");
        result.Headers["Content-Type"].ShouldBe("application/json");
        result.Body.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task FunctionHandler_ShouldReturnEmptyArray_WhenNoAccountsExist()
    {
        // Arrange
        var accounts = new List<AccountResponse>();

        var fakeAccountService = new FakeAccountService();
        fakeAccountService.SetupListAccountsAsync(accounts);

        var function = new GetAccounts(fakeAccountService);
        var request = new APIGatewayProxyRequest();

        // Use FakeLambdaContext instead of mocking
        var context = new FakeLambdaContext();

        // Act
        var result = await function.FunctionHandler(request, context);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(200);
        result.Body.ShouldBe("[]");
    }

    [Fact]
    public async Task FunctionHandler_ShouldReturn500_WhenServiceThrowsException()
    {
        // Arrange
        var fakeAccountService = new FakeAccountService();
        fakeAccountService.SetupListAccountsAsyncToThrow(new Exception("Test exception"));

        var function = new GetAccounts(fakeAccountService);
        var request = new APIGatewayProxyRequest();

        // Use FakeLambdaContext instead of mocking
        var context = new FakeLambdaContext();

        // Act
        var result = await function.FunctionHandler(request, context);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(500);
        result.Body.ShouldContain("An error occurred while retrieving accounts");
    }

    [Fact]
    public async Task FunctionHandler_ShouldApplyFilters_WhenQueryStringParametersProvided()
    {
        // Arrange
        var accounts = new List<AccountResponse>
        {
            new AccountResponseBuilder()
                .WithAccountId("acc-003")
                .WithName("Test Asset Account")
                .WithType("ASSET")
                .WithCurrency("USD")
                .WithCurrentBalance(300m)
                .WithCreatedAt(DateTime.UtcNow)
                .WithIsActive(true)
                .Build()
        };

        var fakeAccountService = new FakeAccountService();
        fakeAccountService.SetupListAccountsAsync(accounts, "ASSET", "USD", true);

        var function = new GetAccounts(fakeAccountService);
        var request = new APIGatewayProxyRequest
        {
            QueryStringParameters = new Dictionary<string, string>
            {
                { "type", "ASSET" },
                { "currency", "USD" },
                { "isActive", "true" }
            }
        };

        // Use FakeLambdaContext instead of mocking
        var context = new FakeLambdaContext();

        // Act
        var result = await function.FunctionHandler(request, context);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(200);
        result.Body.ShouldContain("Test Asset Account");
    }
}