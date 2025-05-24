using LedgerTransacional.Models.Entities;

namespace LedgerTransacional.UnitTests.Builders;

public class EntryBuilder
{
    private string _entryId = "entry-001";
    private string _transactionId = "txn-001";
    private string _accountId = "acc-001";
    private string _entryType = "DEBIT";
    private decimal _amount = 100m;
    private string _description = "Test Entry";
    private DateTime _createdAt = DateTime.UtcNow;

    public EntryBuilder WithEntryId(string entryId)
    {
        _entryId = entryId;
        return this;
    }

    public EntryBuilder WithTransactionId(string transactionId)
    {
        _transactionId = transactionId;
        return this;
    }

    public EntryBuilder WithAccountId(string accountId)
    {
        _accountId = accountId;
        return this;
    }

    public EntryBuilder WithEntryType(string entryType)
    {
        _entryType = entryType;
        return this;
    }

    public EntryBuilder WithAmount(decimal amount)
    {
        _amount = amount;
        return this;
    }

    public EntryBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public EntryBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public Entry Build()
    {
        return new Entry
        {
            EntryId = _entryId,
            TransactionId = _transactionId,
            AccountId = _accountId,
            EntryType = _entryType,
            Amount = _amount,
            Description = _description,
            CreatedAt = _createdAt
        };
    }
}