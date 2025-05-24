namespace LedgerTransacional.Models.DTOs.Responses;

public record TransactionResponse(
    string TransactionId,
    string ReferenceId,
    DateTime TransactionDate,
    string Description,
    string Status,
    decimal TotalAmount,
    string Currency,
    List<EntryResponse> Entries,
    Dictionary<string, string> Metadata,
    DateTime CreatedAt
);