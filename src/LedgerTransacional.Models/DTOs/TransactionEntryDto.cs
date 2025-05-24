using System.ComponentModel.DataAnnotations;

namespace LedgerTransacional.Models.DTOs;

public record TransactionEntryDto(
    [Required] string AccountId,

    [Required] string EntryType,

    [Required] decimal Amount,

    string Description
);