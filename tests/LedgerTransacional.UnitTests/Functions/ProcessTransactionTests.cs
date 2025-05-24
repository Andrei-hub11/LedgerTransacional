using System.Reflection;
using System.Text.Json;

using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;

using LedgerTransacional.Functions.Transactions;
using LedgerTransacional.Models.Entities;
using LedgerTransacional.Services.Interfaces;
using LedgerTransacional.UnitTests.Builders;

using Moq;

namespace LedgerTransacional.UnitTests.Functions;

public class ProcessTransactionTests
{
    [Fact]
    public async Task FunctionHandler_ShouldProcessTransactionSuccessfully()
    {
        // Arrange
        var transaction = new TransactionBuilder()
            .WithTransactionId("txn-001")
            .WithReferenceId("REF001")
            .WithDescription("Test Transaction")
            .WithStatus("PENDING")
            .WithTotalAmount(100m)
            .WithCurrency("USD")
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

        var transactionJson = JsonSerializer.Serialize(transaction, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var sqsEvent = new SQSEvent
        {
            Records = new List<SQSEvent.SQSMessage>
            {
                new SQSEvent.SQSMessage
                {
                    Body = transactionJson,
                    MessageId = "msg-001"
                }
            }
        };

        // Mock transaction service
        var mockTransactionService = new Mock<ITransactionService>();
        mockTransactionService
            .Setup(x => x.GetTransactionAsync(transaction.TransactionId))
            .ReturnsAsync(transaction);
        mockTransactionService
            .Setup(x => x.GetTransactionEntriesAsync(transaction.TransactionId))
            .ReturnsAsync(entries);

        // Mock account service
        var mockAccountService = new Mock<IAccountService>();
        mockAccountService
            .Setup(x => x.GetAccountAsync("acc-001"))
            .ReturnsAsync(account1);
        mockAccountService
            .Setup(x => x.GetAccountAsync("acc-002"))
            .ReturnsAsync(account2);

        // Mock Lambda Context and Logger
        var mockLogger = new Mock<ILambdaLogger>();
        var logMessages = new List<string>();
        mockLogger.Setup(x => x.LogLine(It.IsAny<string>()))
            .Callback<string>(s => logMessages.Add(s));

        var mockContext = new Mock<ILambdaContext>();
        mockContext.Setup(x => x.Logger).Returns(mockLogger.Object);

        // Create an instance of ProcessTransaction and set the private fields via reflection
        var function = new ProcessTransaction();
        SetPrivateField(function, "_transactionService", mockTransactionService.Object);
        SetPrivateField(function, "_accountService", mockAccountService.Object);

        // Act
        await function.FunctionHandler(sqsEvent, mockContext.Object);

        // Verify logs indicate success
        mockLogger.Verify(x => x.LogLine(It.Is<string>(s => s.Contains("Processing message") && s.Contains("msg-001"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task FunctionHandler_ShouldHandleInvalidTransactionJson()
    {
        // Arrange
        var invalidJson = "{\"transactionId\": \"txn-001\", invalid json";

        var sqsEvent = new SQSEvent
        {
            Records = new List<SQSEvent.SQSMessage>
            {
                new SQSEvent.SQSMessage
                {
                    Body = invalidJson,
                    MessageId = "msg-001"
                }
            }
        };

        // Mock services
        var mockTransactionService = new Mock<ITransactionService>();
        var mockAccountService = new Mock<IAccountService>();

        // Mock Lambda Context and Logger
        var mockLogger = new Mock<ILambdaLogger>();
        var logMessages = new List<string>();
        mockLogger.Setup(x => x.LogLine(It.IsAny<string>()))
            .Callback<string>(s => logMessages.Add(s));

        var mockContext = new Mock<ILambdaContext>();
        mockContext.Setup(x => x.Logger).Returns(mockLogger.Object);

        // Create an instance of ProcessTransaction and set the private fields via reflection
        var function = new ProcessTransaction();
        SetPrivateField(function, "_transactionService", mockTransactionService.Object);
        SetPrivateField(function, "_accountService", mockAccountService.Object);

        // Act
        await function.FunctionHandler(sqsEvent, mockContext.Object);

        // Verify logs indicate failure
        mockLogger.Verify(x => x.LogLine(It.Is<string>(s => s.Contains("Error processing message") && s.Contains("msg-001"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task FunctionHandler_ShouldHandleTransactionNotFound()
    {
        // Arrange
        var transaction = new TransactionBuilder()
            .WithTransactionId("txn-not-found")
            .WithStatus("PENDING")
            .Build();

        var transactionJson = JsonSerializer.Serialize(transaction, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var sqsEvent = new SQSEvent
        {
            Records = new List<SQSEvent.SQSMessage>
            {
                new SQSEvent.SQSMessage
                {
                    Body = transactionJson,
                    MessageId = "msg-001"
                }
            }
        };

        // Mock services
        var mockTransactionService = new Mock<ITransactionService>();
        mockTransactionService
            .Setup(x => x.GetTransactionAsync(transaction.TransactionId))
            .ReturnsAsync((Transaction)null);
        mockTransactionService
            .Setup(x => x.GetTransactionEntriesAsync(transaction.TransactionId))
            .ReturnsAsync(new List<Entry>());

        var mockAccountService = new Mock<IAccountService>();

        // Mock Lambda Context and Logger
        var mockLogger = new Mock<ILambdaLogger>();
        var logMessages = new List<string>();
        mockLogger.Setup(x => x.LogLine(It.IsAny<string>()))
            .Callback<string>(s => logMessages.Add(s));

        var mockContext = new Mock<ILambdaContext>();
        mockContext.Setup(x => x.Logger).Returns(mockLogger.Object);

        // Create an instance of ProcessTransaction and set the private fields via reflection
        var function = new ProcessTransaction();
        SetPrivateField(function, "_transactionService", mockTransactionService.Object);
        SetPrivateField(function, "_accountService", mockAccountService.Object);

        // Act
        await function.FunctionHandler(sqsEvent, mockContext.Object);

        // Verify logs indicate empty entries found
        mockLogger.Verify(x => x.LogLine(It.Is<string>(s => s.Contains("Found 0 entries for transaction"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task FunctionHandler_ShouldHandleAccountNotFound()
    {
        // Arrange
        var transaction = new TransactionBuilder()
            .WithTransactionId("txn-001")
            .WithStatus("PENDING")
            .WithDescription("Test Transaction")
            .Build();

        var entries = new List<Entry>
        {
            new EntryBuilder()
                .WithEntryId("entry-001")
                .WithTransactionId(transaction.TransactionId)
                .WithAccountId("acc-not-found")
                .WithEntryType("DEBIT")
                .WithAmount(100m)
                .Build()
        };

        var transactionJson = JsonSerializer.Serialize(transaction, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var sqsEvent = new SQSEvent
        {
            Records = new List<SQSEvent.SQSMessage>
            {
                new SQSEvent.SQSMessage
                {
                    Body = transactionJson,
                    MessageId = "msg-001"
                }
            }
        };

        // Mock services
        var mockTransactionService = new Mock<ITransactionService>();
        mockTransactionService
            .Setup(x => x.GetTransactionAsync(transaction.TransactionId))
            .ReturnsAsync(transaction);
        mockTransactionService
            .Setup(x => x.GetTransactionEntriesAsync(transaction.TransactionId))
            .ReturnsAsync(entries);

        var mockAccountService = new Mock<IAccountService>();
        mockAccountService
            .Setup(x => x.GetAccountAsync("acc-not-found"))
            .ReturnsAsync((Account)null);

        // Mock Lambda Context and Logger
        var mockLogger = new Mock<ILambdaLogger>();
        var logMessages = new List<string>();
        mockLogger.Setup(x => x.LogLine(It.IsAny<string>()))
            .Callback<string>(s => logMessages.Add(s));

        var mockContext = new Mock<ILambdaContext>();
        mockContext.Setup(x => x.Logger).Returns(mockLogger.Object);

        // Create an instance of ProcessTransaction and set the private fields via reflection
        var function = new ProcessTransaction();
        SetPrivateField(function, "_transactionService", mockTransactionService.Object);
        SetPrivateField(function, "_accountService", mockAccountService.Object);

        // Act
        await function.FunctionHandler(sqsEvent, mockContext.Object);

        // Verify logs indicate account not found error
        mockLogger.Verify(x => x.LogLine(It.Is<string>(s => s.Contains("Error processing message") && s.Contains("msg-001") && s.Contains("Account") && s.Contains("not found"))), Times.AtLeastOnce);
    }

    // Helper method to set private fields using reflection
    private static void SetPrivateField(object obj, string fieldName, object value)
    {
        var fieldInfo = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (fieldInfo != null)
        {
            fieldInfo.SetValue(obj, value);
        }
        else
        {
            throw new ArgumentException($"Field {fieldName} not found in {obj.GetType().Name}");
        }
    }
}