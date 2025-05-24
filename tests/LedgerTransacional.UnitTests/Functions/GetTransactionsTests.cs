using Amazon.Lambda.APIGatewayEvents;

using LedgerTransacional.Functions.Transactions;
using LedgerTransacional.Models.Entities;
using LedgerTransacional.Services.Interfaces;
using LedgerTransacional.UnitTests.Builders;
using LedgerTransacional.UnitTests.Fakes;

using Moq;

namespace LedgerTransacional.UnitTests.Functions;

public class GetTransactionsTests
{
    [Fact]
    public async Task FunctionHandler_ShouldReturnTransactions_WhenServiceReturnsTransactions()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            new TransactionBuilder()
                .WithTransactionId("txn-001")
                .WithDescription("Test Transaction 1")
                .WithStatus("COMPLETED")
                .WithTotalAmount(100m)
                .WithCurrency("USD")
                .WithTransactionDate(DateTime.UtcNow.AddDays(-1))
                .WithCreatedAt(DateTime.UtcNow.AddDays(-1))
                .WithUpdatedAt(DateTime.UtcNow.AddDays(-1))
                .Build(),
            new TransactionBuilder()
                .WithTransactionId("txn-002")
                .WithDescription("Test Transaction 2")
                .WithStatus("PENDING")
                .WithTotalAmount(200m)
                .WithCurrency("EUR")
                .WithTransactionDate(DateTime.UtcNow.AddDays(-2))
                .WithCreatedAt(DateTime.UtcNow.AddDays(-2))
                .WithUpdatedAt(DateTime.UtcNow.AddDays(-2))
                .Build()
        };

        var fakeTransactionService = new FakeTransactionService();
        fakeTransactionService.SetupListTransactionsAsync(transactions);

        var function = new GetTransactions(fakeTransactionService);
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

        // Verify content has transaction IDs
        result.Body.ShouldContain("txn-001");
        result.Body.ShouldContain("txn-002");
    }

    [Fact]
    public async Task FunctionHandler_ShouldReturnEmptyArray_WhenNoTransactionsExist()
    {
        // Arrange
        var transactions = new List<Transaction>();

        var fakeTransactionService = new FakeTransactionService();
        fakeTransactionService.SetupListTransactionsAsync(transactions);

        var function = new GetTransactions(fakeTransactionService);
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
    public async Task FunctionHandler_ShouldApplyFilters_WhenQueryStringParametersProvided()
    {
        // Arrange
        var startDate = DateTime.Parse("2023-01-01");
        var endDate = DateTime.Parse("2023-01-31");
        var status = "COMPLETED";
        var accountId = "acc-001";

        var transactions = new List<Transaction>
        {
            new TransactionBuilder()
                .WithTransactionId("txn-filtered")
                .WithDescription("Filtered Transaction")
                .WithStatus(status)
                .WithTotalAmount(300m)
                .WithCurrency("USD")
                .WithTransactionDate(DateTime.Parse("2023-01-15"))
                .WithCreatedAt(DateTime.Parse("2023-01-15"))
                .WithUpdatedAt(DateTime.Parse("2023-01-15"))
                .Build()
        };

        // Moq para verificar exatamente os parâmetros passados
        var mockTransactionService = new Mock<ITransactionService>();
        mockTransactionService
            .Setup(x => x.ListTransactionsAsync(
                It.Is<DateTime?>(d => d.HasValue && d.Value.Date == startDate.Date),
                It.Is<DateTime?>(d => d.HasValue && d.Value.Date == endDate.Date),
                status,
                accountId))
            .ReturnsAsync(transactions);

        var function = new GetTransactions(mockTransactionService.Object);
        var request = new APIGatewayProxyRequest
        {
            QueryStringParameters = new Dictionary<string, string>
            {
                { "startDate", "2023-01-01" },
                { "endDate", "2023-01-31" },
                { "status", status },
                { "accountId", accountId }
            }
        };

        // Use FakeLambdaContext instead of mocking
        var context = new FakeLambdaContext();

        // Act
        var result = await function.FunctionHandler(request, context);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(200);
        result.Body.ShouldContain("txn-filtered");
        result.Body.ShouldContain("Filtered Transaction");

        // Verificar que o método ListTransactionsAsync foi chamado com os parâmetros corretos
        mockTransactionService.Verify(
            x => x.ListTransactionsAsync(
                It.Is<DateTime?>(d => d.HasValue && d.Value.Date == startDate.Date),
                It.Is<DateTime?>(d => d.HasValue && d.Value.Date == endDate.Date),
                status,
                accountId),
            Times.Once);
    }

    [Fact]
    public async Task FunctionHandler_ShouldReturn500_WhenServiceThrowsException()
    {
        // Arrange
        var fakeTransactionService = new FakeTransactionService();
        fakeTransactionService.SetupListTransactionsAsyncToThrow(new Exception("Test exception"));

        var function = new GetTransactions(fakeTransactionService);
        var request = new APIGatewayProxyRequest();

        // Use FakeLambdaContext instead of mocking
        var context = new FakeLambdaContext();

        // Act
        var result = await function.FunctionHandler(request, context);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(500);
        result.Body.ShouldContain("An error occurred while retrieving transactions");
    }
}