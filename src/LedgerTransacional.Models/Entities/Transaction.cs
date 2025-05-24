using Amazon.DynamoDBv2.DataModel;

namespace LedgerTransacional.Models.Entities;

/// <summary>
/// Represents a transaction in the ledger
/// </summary>
[DynamoDBTable("Transactions")]
public class Transaction
{
    [DynamoDBHashKey]
    public string TransactionId { get; set; } = string.Empty;
    public string ReferenceId { get; set; } = string.Empty; // External ID/business reference
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // PENDING, COMPLETED, FAILED, REVERSED
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}