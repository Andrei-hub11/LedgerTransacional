using Amazon.Lambda.APIGatewayEvents;
using Amazon.SQS;
using Amazon.SQS.Model;

using LedgerTransacional.Functions.Transactions;
using LedgerTransacional.Models.Entities;
using LedgerTransacional.UnitTests.Builders;
using LedgerTransacional.UnitTests.Fakes;

using Moq;

namespace LedgerTransacional.UnitTests.Functions;

public class ReverseTransactionTests
{
    [Fact]
    public async Task FunctionHandler_ShouldReturnReversalTransaction_WhenTransactionExists()
    {
        // Arrange
        var originalTransactionId = "txn-001";
        var reversalTransactionId = "txn-rev-001";

        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                { "transactionId", originalTransactionId }
            }
        };

        // Setup original transaction
        var originalTransaction = new TransactionBuilder()
            .WithTransactionId(originalTransactionId)
            .WithReferenceId("REF001")
            .WithDescription("Original Transaction")
            .WithStatus("COMPLETED") // Must be completed to be reversed
            .WithTotalAmount(100m)
            .WithCurrency("USD")
            .WithMetadataItem("Source", "API")
            .Build();

        // Setup reversal transaction
        var reversalTransaction = new TransactionBuilder()
            .WithTransactionId(reversalTransactionId)
            .WithReferenceId($"REVERSE-{originalTransactionId}")
            .WithDescription($"Reversal of transaction {originalTransactionId}")
            .WithStatus("PENDING")
            .WithTotalAmount(100m)
            .WithCurrency("USD")
            .WithMetadataItem("OriginalTransactionId", originalTransactionId)
            .WithMetadataItem("ReverseOperation", "true")
            .WithMetadataItem("Source", "API")
            .Build();

        // Setup entries for reversal transaction
        var reversalEntries = new List<Entry>
        {
            new EntryBuilder()
                .WithEntryId("entry-rev-001")
                .WithTransactionId(reversalTransactionId)
                .WithAccountId("acc-001")
                .WithEntryType("CREDIT") // Reversed from DEBIT
                .WithAmount(100m)
                .WithDescription("Reversal: Debit Entry")
                .Build(),
            new EntryBuilder()
                .WithEntryId("entry-rev-002")
                .WithTransactionId(reversalTransactionId)
                .WithAccountId("acc-002")
                .WithEntryType("DEBIT") // Reversed from CREDIT
                .WithAmount(100m)
                .WithDescription("Reversal: Credit Entry")
                .Build()
        };

        // Setup accounts
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
        fakeTransactionService.SetupGetTransactionAsync(originalTransactionId, originalTransaction);
        fakeTransactionService.SetupReverseTransactionAsync(originalTransactionId, reversalTransaction);
        fakeTransactionService.SetupGetTransactionEntriesAsync(reversalTransactionId, reversalEntries);

        var fakeAccountService = new FakeAccountService();
        fakeAccountService.SetupGetAccountAsync("acc-001", account1);
        fakeAccountService.SetupGetAccountAsync("acc-002", account2);

        // Mock SQS client
        var sqsClientMock = new Mock<IAmazonSQS>();
        sqsClientMock
            .Setup(m => m.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageResponse { MessageId = "msg-rev-001" });

        // Use FakeLambdaContext
        var context = new FakeLambdaContext();

        var function = new ReverseTransaction(
            fakeTransactionService,
            fakeAccountService,
            sqsClientMock.Object,
            "https://sqs.us-east-1.amazonaws.com/123456789012/TestQueue");

        // Act
        var result = await function.FunctionHandler(apiGatewayRequest, context);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(200);
        result.Headers.ShouldContainKey("Content-Type");
        result.Headers["Content-Type"].ShouldBe("application/json");
        result.Body.ShouldNotBeNullOrWhiteSpace();
        result.Body.ShouldContain(reversalTransactionId);
        result.Body.ShouldContain("REVERSE-");
        result.Body.ShouldContain("Reversal of transaction");

        // Verify SQS message was sent
        sqsClientMock.Verify(m => m.SendMessageAsync(
            It.Is<SendMessageRequest>(r =>
                r.QueueUrl == "https://sqs.us-east-1.amazonaws.com/123456789012/TestQueue" &&
                r.MessageAttributes.ContainsKey("IsReversal") &&
                r.MessageAttributes["IsReversal"].StringValue == "true"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FunctionHandler_ShouldReturnReversalTransaction_WithCustomDescription_WhenProvided()
    {
        // Arrange
        var originalTransactionId = "txn-001";
        var reversalTransactionId = "txn-rev-001";
        var customDescription = "Custom reversal description";

        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                { "transactionId", originalTransactionId }
            },
            QueryStringParameters = new Dictionary<string, string>
            {
                { "description", customDescription }
            }
        };

        // Setup original transaction
        var originalTransaction = new TransactionBuilder()
            .WithTransactionId(originalTransactionId)
            .WithReferenceId("REF001")
            .WithDescription("Original Transaction")
            .WithStatus("COMPLETED")
            .WithTotalAmount(100m)
            .WithCurrency("USD")
            .Build();

        // Setup reversal transaction with custom description
        var reversalTransaction = new TransactionBuilder()
            .WithTransactionId(reversalTransactionId)
            .WithReferenceId($"REVERSE-{originalTransactionId}")
            .WithDescription(customDescription)
            .WithStatus("PENDING")
            .WithTotalAmount(100m)
            .WithCurrency("USD")
            .WithMetadataItem("OriginalTransactionId", originalTransactionId)
            .WithMetadataItem("ReverseOperation", "true")
            .Build();

        // Setup entries for reversal transaction
        var reversalEntries = new List<Entry>
        {
            new EntryBuilder()
                .WithEntryId("entry-rev-001")
                .WithTransactionId(reversalTransactionId)
                .WithAccountId("acc-001")
                .WithEntryType("CREDIT")
                .WithAmount(100m)
                .WithDescription("Reversal: Debit Entry")
                .Build(),
            new EntryBuilder()
                .WithEntryId("entry-rev-002")
                .WithTransactionId(reversalTransactionId)
                .WithAccountId("acc-002")
                .WithEntryType("DEBIT")
                .WithAmount(100m)
                .WithDescription("Reversal: Credit Entry")
                .Build()
        };

        // Setup accounts
        var account1 = new AccountBuilder()
            .WithAccountId("acc-001")
            .WithName("Account 1")
            .WithType("ASSET")
            .WithCurrency("USD")
            .Build();

        var account2 = new AccountBuilder()
            .WithAccountId("acc-002")
            .WithName("Account 2")
            .WithType("LIABILITY")
            .WithCurrency("USD")
            .Build();

        // Create fake services
        var fakeTransactionService = new FakeTransactionService();
        fakeTransactionService.SetupGetTransactionAsync(originalTransactionId, originalTransaction);
        fakeTransactionService.SetupReverseTransactionAsync(originalTransactionId, reversalTransaction, customDescription);
        fakeTransactionService.SetupGetTransactionEntriesAsync(reversalTransactionId, reversalEntries);

        var fakeAccountService = new FakeAccountService();
        fakeAccountService.SetupGetAccountAsync("acc-001", account1);
        fakeAccountService.SetupGetAccountAsync("acc-002", account2);

        // Mock SQS client
        var sqsClientMock = new Mock<IAmazonSQS>();
        sqsClientMock
            .Setup(m => m.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageResponse { MessageId = "msg-rev-002" });

        // Use FakeLambdaContext
        var context = new FakeLambdaContext();

        var function = new ReverseTransaction(
            fakeTransactionService,
            fakeAccountService,
            sqsClientMock.Object,
            "https://sqs.us-east-1.amazonaws.com/123456789012/TestQueue");

        // Act
        var result = await function.FunctionHandler(apiGatewayRequest, context);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(200);
        result.Body.ShouldContain(customDescription);

        // Verify SQS message was sent with correct attributes
        sqsClientMock.Verify(m => m.SendMessageAsync(
            It.Is<SendMessageRequest>(r => r.QueueUrl == "https://sqs.us-east-1.amazonaws.com/123456789012/TestQueue"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FunctionHandler_ShouldReturnBadRequest_WhenTransactionIdIsMissing()
    {
        // Arrange
        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>()
        };

        // Create fake services
        var fakeTransactionService = new FakeTransactionService();
        var fakeAccountService = new FakeAccountService();

        // Mock SQS client
        var sqsClientMock = new Mock<IAmazonSQS>();

        // Use FakeLambdaContext
        var context = new FakeLambdaContext();

        var function = new ReverseTransaction(
            fakeTransactionService,
            fakeAccountService,
            sqsClientMock.Object,
            "https://sqs.us-east-1.amazonaws.com/123456789012/TestQueue");

        // Act
        var result = await function.FunctionHandler(apiGatewayRequest, context);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(400);
        result.Body.ShouldContain("Transaction ID is required");

        // Verify no SQS message was sent
        sqsClientMock.Verify(m => m.SendMessageAsync(
            It.IsAny<SendMessageRequest>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FunctionHandler_ShouldReturnNotFound_WhenTransactionDoesNotExist()
    {
        // Arrange
        var nonExistentTransactionId = "txn-not-exist";

        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                { "transactionId", nonExistentTransactionId }
            }
        };

        // Create fake services
        var fakeTransactionService = new FakeTransactionService();
        fakeTransactionService.SetupGetTransactionAsync(nonExistentTransactionId, null);

        var fakeAccountService = new FakeAccountService();

        // Mock SQS client
        var sqsClientMock = new Mock<IAmazonSQS>();

        // Use FakeLambdaContext
        var context = new FakeLambdaContext();

        var function = new ReverseTransaction(
            fakeTransactionService,
            fakeAccountService,
            sqsClientMock.Object,
            "https://sqs.us-east-1.amazonaws.com/123456789012/TestQueue");

        // Act
        var result = await function.FunctionHandler(apiGatewayRequest, context);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(404);
        result.Body.ShouldContain("not found");

        // Verify no SQS message was sent
        sqsClientMock.Verify(m => m.SendMessageAsync(
            It.IsAny<SendMessageRequest>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FunctionHandler_ShouldReturnBadRequest_WhenTransactionIsNotInCorrectState()
    {
        // Arrange
        var pendingTransactionId = "txn-pending";

        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                { "transactionId", pendingTransactionId }
            }
        };

        // Setup transaction with PENDING status
        var pendingTransaction = new TransactionBuilder()
            .WithTransactionId(pendingTransactionId)
            .WithReferenceId("REF002")
            .WithDescription("Pending Transaction")
            .WithStatus("PENDING") // Not COMPLETED, so cannot be reversed
            .WithTotalAmount(200m)
            .WithCurrency("USD")
            .Build();

        // Create fake services
        var fakeTransactionService = new FakeTransactionService();
        fakeTransactionService.SetupGetTransactionAsync(pendingTransactionId, pendingTransaction);
        fakeTransactionService.SetupReverseTransactionAsyncToThrow(
            pendingTransactionId,
            new InvalidOperationException($"Cannot reverse a transaction with status PENDING"));

        var fakeAccountService = new FakeAccountService();

        // Mock SQS client
        var sqsClientMock = new Mock<IAmazonSQS>();

        // Use FakeLambdaContext
        var context = new FakeLambdaContext();

        var function = new ReverseTransaction(
            fakeTransactionService,
            fakeAccountService,
            sqsClientMock.Object,
            "https://sqs.us-east-1.amazonaws.com/123456789012/TestQueue");

        // Act
        var result = await function.FunctionHandler(apiGatewayRequest, context);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(400);
        result.Body.ShouldContain("Cannot reverse a transaction with status PENDING");

        // Verify no SQS message was sent
        sqsClientMock.Verify(m => m.SendMessageAsync(
            It.IsAny<SendMessageRequest>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FunctionHandler_ShouldReturnInternalServerError_WhenServiceThrowsException()
    {
        // Arrange
        var transactionId = "txn-error";

        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                { "transactionId", transactionId }
            }
        };

        // Setup a valid transaction
        var transaction = new TransactionBuilder()
            .WithTransactionId(transactionId)
            .WithReferenceId("REF003")
            .WithDescription("Valid Transaction")
            .WithStatus("COMPLETED")
            .WithTotalAmount(300m)
            .WithCurrency("USD")
            .Build();

        // Create fake services
        var fakeTransactionService = new FakeTransactionService();
        fakeTransactionService.SetupGetTransactionAsync(transactionId, transaction);
        fakeTransactionService.SetupReverseTransactionAsyncToThrow(
            transactionId,
            new Exception("Database connection error"));

        var fakeAccountService = new FakeAccountService();

        // Mock SQS client
        var sqsClientMock = new Mock<IAmazonSQS>();

        // Use FakeLambdaContext
        var context = new FakeLambdaContext();

        var function = new ReverseTransaction(
            fakeTransactionService,
            fakeAccountService,
            sqsClientMock.Object,
            "https://sqs.us-east-1.amazonaws.com/123456789012/TestQueue");

        // Act
        var result = await function.FunctionHandler(apiGatewayRequest, context);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(500);
        result.Body.ShouldContain("An error occurred while reversing the transaction");

        // Verify no SQS message was sent
        sqsClientMock.Verify(m => m.SendMessageAsync(
            It.IsAny<SendMessageRequest>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FunctionHandler_ShouldReturnInternalServerError_WhenSQSThrowsException()
    {
        // Arrange
        var originalTransactionId = "txn-001";
        var reversalTransactionId = "txn-rev-001";

        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                { "transactionId", originalTransactionId }
            }
        };

        // Setup original transaction
        var originalTransaction = new TransactionBuilder()
            .WithTransactionId(originalTransactionId)
            .WithReferenceId("REF001")
            .WithDescription("Original Transaction")
            .WithStatus("COMPLETED")
            .WithTotalAmount(100m)
            .WithCurrency("USD")
            .Build();

        // Setup reversal transaction
        var reversalTransaction = new TransactionBuilder()
            .WithTransactionId(reversalTransactionId)
            .WithReferenceId($"REVERSE-{originalTransactionId}")
            .WithDescription($"Reversal of transaction {originalTransactionId}")
            .WithStatus("PENDING")
            .WithTotalAmount(100m)
            .WithCurrency("USD")
            .Build();

        // Setup entries
        var reversalEntries = new List<Entry>
        {
            new EntryBuilder()
                .WithEntryId("entry-rev-001")
                .WithTransactionId(reversalTransactionId)
                .WithAccountId("acc-001")
                .WithEntryType("CREDIT")
                .WithAmount(100m)
                .Build()
        };

        // Create fake services
        var fakeTransactionService = new FakeTransactionService();
        fakeTransactionService.SetupGetTransactionAsync(originalTransactionId, originalTransaction);
        fakeTransactionService.SetupReverseTransactionAsync(originalTransactionId, reversalTransaction);
        fakeTransactionService.SetupGetTransactionEntriesAsync(reversalTransactionId, reversalEntries);

        var fakeAccountService = new FakeAccountService();
        fakeAccountService.SetupGetAccountAsync("acc-001", new AccountBuilder().WithAccountId("acc-001").WithName("Account 1").Build());

        // Mock SQS client to throw exception
        var sqsClientMock = new Mock<IAmazonSQS>();
        sqsClientMock
            .Setup(m => m.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonSQSException("SQS error"));

        // Use FakeLambdaContext
        var context = new FakeLambdaContext();

        var function = new ReverseTransaction(
            fakeTransactionService,
            fakeAccountService,
            sqsClientMock.Object,
            "https://sqs.us-east-1.amazonaws.com/123456789012/TestQueue");

        // Act
        var result = await function.FunctionHandler(apiGatewayRequest, context);

        // Assert
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(500);
        result.Body.ShouldContain("An error occurred while reversing the transaction");
    }
}