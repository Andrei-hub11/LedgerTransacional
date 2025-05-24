using Amazon.DynamoDBv2.DataModel;


namespace LedgerTransacional.Models.Entities;

/// <summary>
/// Represents an account in the ledger
/// </summary>
[DynamoDBTable("Accounts")]
public class Account
{
    [DynamoDBHashKey]
    public string AccountId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty; // ASSET, LIABILITY, EQUITY, REVENUE, EXPENSE

    public string Currency { get; set; } = string.Empty;

    public decimal CurrentBalance { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsActive { get; set; }
}