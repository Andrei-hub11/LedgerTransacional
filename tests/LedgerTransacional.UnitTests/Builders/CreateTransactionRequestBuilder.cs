using LedgerTransacional.Models.DTOs;
using LedgerTransacional.Models.DTOs.Requests;

namespace LedgerTransacional.UnitTests.Builders;

public class CreateTransactionRequestBuilder
{
    private string _referenceId = "REF-001";
    private string _description = "Test Transaction";
    private string _currency = "USD";
    private List<TransactionEntryDto> _entries = new();
    private Dictionary<string, string> _metadata = new();

    public CreateTransactionRequestBuilder WithReferenceId(string referenceId)
    {
        _referenceId = referenceId;
        return this;
    }

    public CreateTransactionRequestBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public CreateTransactionRequestBuilder WithCurrency(string currency)
    {
        _currency = currency;
        return this;
    }

    public CreateTransactionRequestBuilder WithEntries(List<TransactionEntryDto> entries)
    {
        _entries = entries;
        return this;
    }

    public CreateTransactionRequestBuilder WithEntry(string accountId, string entryType, decimal amount, string description)
    {
        _entries.Add(new TransactionEntryDto(accountId, entryType, amount, description));
        return this;
    }

    public CreateTransactionRequestBuilder WithMetadata(Dictionary<string, string> metadata)
    {
        _metadata = metadata;
        return this;
    }

    public CreateTransactionRequestBuilder WithMetadataItem(string key, string value)
    {
        _metadata[key] = value;
        return this;
    }

    public CreateTransactionRequest Build()
    {
        return new CreateTransactionRequest(
            ReferenceId: _referenceId,
            Description: _description,
            Currency: _currency,
            Entries: _entries,
            Metadata: _metadata
        );
    }
}