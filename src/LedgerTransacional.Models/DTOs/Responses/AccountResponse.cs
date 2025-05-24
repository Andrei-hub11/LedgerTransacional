namespace LedgerTransacional.Models.DTOs.Responses;

public record AccountResponse(
    string AccountId,
    string Name,
    string Type,
    string Currency,
    decimal CurrentBalance,
    DateTime CreatedAt,
    bool IsActive
);