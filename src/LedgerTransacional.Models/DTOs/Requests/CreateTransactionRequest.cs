using System.ComponentModel.DataAnnotations;


namespace LedgerTransacional.Models.DTOs.Requests;

public record CreateTransactionRequest(
    string ReferenceId,
    [Required] string Description,
    [Required] string Currency,
    [Required] List<TransactionEntryDto> Entries,
    Dictionary<string, string> Metadata
);