using System.Text.Json;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.SQS;
using Amazon.SQS.Model;

using LedgerTransacional.Functions.Transactions;
using LedgerTransacional.Models.Entities;
using LedgerTransacional.UnitTests.Builders;
using LedgerTransacional.UnitTests.Fakes;

using Moq;

namespace LedgerTransacional.UnitTests.Functions;

public class CreateTransactionTests
{
    [Fact]
    public async Task FunctionHandler_ShouldReturnCreatedTransaction_WhenRequestIsValid()
    {
        // Arrange
        var transactionRequest = new CreateTransactionRequestBuilder()
            .WithReferenceId("REF001")
            .WithDescription("Test Transaction")
            .WithCurrency("USD")
            .WithEntry("acc-001", "DEBIT", 100m, "Debit Entry")
            .WithEntry("acc-002", "CREDIT", 100m, "Credit Entry")
            .WithMetadataItem("Source", "API")
            .Build();

        var requestJson = JsonSerializer.Serialize(transactionRequest);

        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            Body = requestJson
        };

        var transaction = new TransactionBuilder()
            .WithTransactionId("txn-001")
            .WithReferenceId(transactionRequest.ReferenceId)
            .WithDescription(transactionRequest.Description)
            .WithStatus("PENDING")
            .WithTotalAmount(100m)
            .WithCurrency(transactionRequest.Currency)
            .WithMetadata(transactionRequest.Metadata)
            .Build();

        var entries = new List<Entry>
        {
            new EntryBuilder()
                .WithEntryId("entry-001")
                .WithTransactionId(transaction.TransactionId)
                .WithAccountId("acc-001")
                .WithEntryType("DEBIT")
                .WithAmount(100m)
                .WithDescription("Debit Entry")
                .Build(),
            new EntryBuilder()
                .WithEntryId("entry-002")
                .WithTransactionId(transaction.TransactionId)
                .WithAccountId("acc-002")
                .WithEntryType("CREDIT")
                .WithAmount(100m)
                .WithDescription("Credit Entry")
                .Build()
        };

        var account1 = new AccountBuilder()
            .WithAccountId("acc-001")
            .WithName("Account 1")
            .WithType("ASSET")
            .WithCurrency("USD")
            .WithCurrentBalance(1000m)
            .Build();

        var account2 = new AccountBuilder()
            .WithAccountId("acc-002")
            .WithName("Account 2")
            .WithType("LIABILITY")
            .WithCurrency("USD")
            .WithCurrentBalance(500m)
            .Build();

        // Create fake services
        var fakeTransactionService = new FakeTransactionService();
        fakeTransactionService.SetupCreateTransactionAsync(transactionRequest, transaction);
        fakeTransactionService.SetupGetTransactionEntriesAsync(transaction.TransactionId, entries);

        var fakeAccountService = new FakeAccountService();
        fakeAccountService.SetupGetAccountAsync("acc-001", account1);
        fakeAccountService.SetupGetAccountAsync("acc-002", account2);

        // Mock SQS client
        var sqsClientMock = new Mock<IAmazonSQS>();
        sqsClientMock
            .Setup(m => m.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageResponse { MessageId = "msg-001" });

        // Use FakeLambdaContext
        var context = new FakeLambdaContext();

        var function = new CreateTransaction(
            fakeTransactionService,
            fakeAccountService,
            sqsClientMock.Object,
            "https://sqs.us-east-1.amazonaws.com/123456789012/TestQueue");

        // Act
        var result = await function.FunctionHandler(apiGatewayRequest, context);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(201);
        result.Headers.ShouldContainKey("Content-Type");
        result.Headers["Content-Type"].ShouldBe("application/json");
        result.Body.ShouldNotBeNullOrWhiteSpace();
        result.Body.ShouldContain("txn-001");
        result.Body.ShouldContain("Test Transaction");

        // Verify SQS message was sent
        sqsClientMock.Verify(m => m.SendMessageAsync(
            It.Is<SendMessageRequest>(r => r.QueueUrl == "https://sqs.us-east-1.amazonaws.com/123456789012/TestQueue"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FunctionHandler_ShouldReturnBadRequest_WhenRequestBodyIsNull()
    {
        // Arrange
        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            Body = null
        };

        // Create fake services
        var fakeTransactionService = new FakeTransactionService();
        var fakeAccountService = new FakeAccountService();

        // Mock SQS client
        var sqsClientMock = new Mock<IAmazonSQS>();

        // Use FakeLambdaContext
        var context = new FakeLambdaContext();

        var function = new CreateTransaction(
            fakeTransactionService,
            fakeAccountService,
            sqsClientMock.Object,
            "https://sqs.us-east-1.amazonaws.com/123456789012/TestQueue");

        // Act
        var result = await function.FunctionHandler(apiGatewayRequest, context);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(400);
        result.Body.ShouldContain("Request body cannot be empty");
    }

    [Fact]
    public async Task FunctionHandler_ShouldReturnBadRequest_WhenServiceThrowsArgumentException()
    {
        // Arrange
        var transactionRequest = new CreateTransactionRequestBuilder()
            .WithReferenceId("REF001")
            .WithDescription("Test Transaction")
            .WithCurrency("USD")
            .WithEntry("acc-001", "INVALID", 100m, "Invalid Entry") // Invalid entry type
            .WithEntry("acc-002", "CREDIT", 100m, "Credit Entry")
            .Build();

        var requestJson = JsonSerializer.Serialize(transactionRequest);

        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            Body = requestJson
        };

        // Create fake services
        var fakeTransactionService = new FakeTransactionService();
        fakeTransactionService.SetupCreateTransactionAsyncToThrow(
            transactionRequest,
            new ArgumentException("Invalid entry type: INVALID. Must be 'DEBIT' or 'CREDIT'"));

        var fakeAccountService = new FakeAccountService();

        // Mock SQS client
        var sqsClientMock = new Mock<IAmazonSQS>();

        // Use FakeLambdaContext
        var context = new FakeLambdaContext();

        var function = new CreateTransaction(
            fakeTransactionService,
            fakeAccountService,
            sqsClientMock.Object,
            "https://sqs.us-east-1.amazonaws.com/123456789012/TestQueue");

        // Act
        var result = await function.FunctionHandler(apiGatewayRequest, context);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(400);
        result.Body.ShouldContain("Invalid entry type");
    }

    [Fact]
    public async Task FunctionHandler_ShouldReturnInternalServerError_WhenServiceThrowsException()
    {
        // Arrange
        var transactionRequest = new CreateTransactionRequestBuilder()
            .WithReferenceId("REF001")
            .WithDescription("Test Transaction")
            .WithCurrency("USD")
            .WithEntry("acc-001", "DEBIT", 100m, "Debit Entry")
            .WithEntry("acc-002", "CREDIT", 100m, "Credit Entry")
            .Build();

        var requestJson = JsonSerializer.Serialize(transactionRequest);

        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            Body = requestJson
        };

        // Create fake services
        var fakeTransactionService = new FakeTransactionService();
        fakeTransactionService.SetupCreateTransactionAsyncToThrow(
            transactionRequest,
            new Exception("Database connection error"));

        var fakeAccountService = new FakeAccountService();

        // Mock SQS client
        var sqsClientMock = new Mock<IAmazonSQS>();

        // Use FakeLambdaContext
        var context = new FakeLambdaContext();

        var function = new CreateTransaction(
            fakeTransactionService,
            fakeAccountService,
            sqsClientMock.Object,
            "https://sqs.us-east-1.amazonaws.com/123456789012/TestQueue");

        // Act
        var result = await function.FunctionHandler(apiGatewayRequest, context);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(500);
        result.Body.ShouldContain("An error occurred while processing the transaction");
    }
}