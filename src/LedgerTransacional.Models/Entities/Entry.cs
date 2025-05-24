using Amazon.DynamoDBv2.DataModel;

namespace LedgerTransacional.Models.Entities;

/// <summary>
/// Represents an individual entry in the ledger (debit or credit)
/// </summary>
[DynamoDBTable("Entries")]
public class Entry
{
    [DynamoDBHashKey]
    public string EntryId { get; set; } = string.Empty;

    [DynamoDBRangeKey]
    public string TransactionId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string EntryType { get; set; } = string.Empty; // DEBIT, CREDIT
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}