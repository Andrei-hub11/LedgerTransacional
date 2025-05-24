using System.ComponentModel.DataAnnotations;

namespace LedgerTransacional.Models.DTOs.Requests;

public record AccountStatementRequest(
    [Required] string AccountId,
    DateTime? StartDate,
    DateTime? EndDate
);