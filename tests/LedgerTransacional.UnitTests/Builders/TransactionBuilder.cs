using LedgerTransacional.Models.Entities;

namespace LedgerTransacional.UnitTests.Builders;

public class TransactionBuilder
{
    private string _transactionId = "txn-001";
    private string _referenceId = "REF-001";
    private string _description = "Test Transaction";
    private string _status = "PENDING";
    private decimal _totalAmount = 100m;
    private string _currency = "USD";
    private Dictionary<string, string> _metadata = new();
    private DateTime _transactionDate = DateTime.UtcNow.AddDays(-1);
    private DateTime _createdAt = DateTime.UtcNow.AddDays(-1);
    private DateTime _updatedAt = DateTime.UtcNow.AddDays(-1);

    public TransactionBuilder WithTransactionId(string transactionId)
    {
        _transactionId = transactionId;
        return this;
    }

    public TransactionBuilder WithReferenceId(string referenceId)
    {
        _referenceId = referenceId;
        return this;
    }

    public TransactionBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public TransactionBuilder WithStatus(string status)
    {
        _status = status;
        return this;
    }

    public TransactionBuilder WithTotalAmount(decimal totalAmount)
    {
        _totalAmount = totalAmount;
        return this;
    }

    public TransactionBuilder WithCurrency(string currency)
    {
        _currency = currency;
        return this;
    }

    public TransactionBuilder WithMetadata(Dictionary<string, string> metadata)
    {
        _metadata = metadata;
        return this;
    }

    public TransactionBuilder WithMetadataItem(string key, string value)
    {
        _metadata[key] = value;
        return this;
    }

    public TransactionBuilder WithTransactionDate(DateTime transactionDate)
    {
        _transactionDate = transactionDate;
        return this;
    }

    public TransactionBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public TransactionBuilder WithUpdatedAt(DateTime updatedAt)
    {
        _updatedAt = updatedAt;
        return this;
    }

    public Transaction Build()
    {
        return new Transaction
        {
            TransactionId = _transactionId,
            ReferenceId = _referenceId,
            Description = _description,
            Status = _status,
            TotalAmount = _totalAmount,
            Currency = _currency,
            Metadata = _metadata,
            TransactionDate = _transactionDate,
            CreatedAt = _createdAt,
            UpdatedAt = _updatedAt
        };
    }
}