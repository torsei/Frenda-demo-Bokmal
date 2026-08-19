namespace Bokmal.Database.Entities;

/// <summary>
/// The values book_copy.status is allowed to hold, mirrored from the CHECK constraint in
/// the schema. The generator gives us a plain string for that column -- SQLite has no
/// enum type to read -- so these constants are what keeps a typo a compile error instead
/// of a row the database rejects at runtime.
/// </summary>
public static class CopyStatuses
{
    public const string Available = "Available";
    public const string OnLoan = "OnLoan";
}
