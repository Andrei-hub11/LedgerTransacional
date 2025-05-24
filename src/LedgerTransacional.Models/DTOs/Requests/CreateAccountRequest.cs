using System.ComponentModel.DataAnnotations;

namespace LedgerTransacional.Models.DTOs.Requests;

public record CreateAccountRequest(
    [Required] string Name,

    [Required] string Type,

    [Required] string Currency,

    decimal InitialBalance = 0
);