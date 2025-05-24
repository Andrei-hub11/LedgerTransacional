namespace LedgerTransacional.Models.DTOs.Responses;

public record EntryResponse(
    string EntryId,
    string AccountId,
    string AccountName,
    string EntryType,
    decimal Amount,
    string Description
);