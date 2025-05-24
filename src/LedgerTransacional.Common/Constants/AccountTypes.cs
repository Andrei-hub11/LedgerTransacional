namespace LedgerTransacional.Common.Constants;

/// <summary>
/// Ledger account types
/// </summary>
public static class AccountTypes
{
    public const string Asset = "ASSET";
    public const string Liability = "LIABILITY";
    public const string Equity = "EQUITY";
    public const string Revenue = "REVENUE";
    public const string Expense = "EXPENSE";

    /// <summary>
    /// Checks if the type is valid
    /// </summary>
    public static bool IsValidType(string accountType)
    {
        if (string.IsNullOrWhiteSpace(accountType))
            return false;

        string type = accountType.Trim().ToUpperInvariant();

        return type == Asset ||
               type == Liability ||
               type == Equity ||
               type == Revenue ||
               type == Expense;
    }

    /// <summary>
    /// Returns all valid account types
    /// </summary>
    public static string[] GetAllTypes()
    {
        return [Asset, Liability, Equity, Revenue, Expense];
    }
}