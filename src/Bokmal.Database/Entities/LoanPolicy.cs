namespace Bokmal.Database.Entities;

/// <summary>
/// The library's lending rules, in one place so the API, the seed data and the tests
/// cannot disagree about them.
/// </summary>
public static class LoanPolicy
{
    /// <summary>How long a borrower gets before a loan counts as overdue.</summary>
    public const int LoanPeriodDays = 28;

    /// <summary>
    /// A cap on how much of the shelf one person can hold at once. Without it a single
    /// borrower could empty the library and the availability counts would stop meaning
    /// anything.
    /// </summary>
    public const int MaxActiveLoansPerBorrower = 5;
}
