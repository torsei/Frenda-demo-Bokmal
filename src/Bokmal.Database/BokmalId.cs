namespace Bokmal.Database;

/// <summary>
/// The single place new identifiers are created.
///
/// Every key in Bokmal is a GUID the application assigns before saving, which is what lets
/// a whole object graph be built in memory and written in one transaction, and what lets
/// the seed scripts hard-code identifiers without any risk of colliding with generated
/// ones.
///
/// Version 7 rather than <c>Guid.NewGuid()</c>: v4 is random, so every insert lands at an
/// arbitrary point in the index and fragments it. A v7 value carries a timestamp in its
/// high bits, so values are effectively ascending and inserts stay at the end of the index
/// the way an identity key would -- without giving up either global uniqueness or the
/// ability to generate the value client-side.
///
/// <c>Guid.NewGuid()</c> cannot be reconfigured to do this: it is contractually a v4
/// generator. So it is banned instead -- see BannedSymbols.txt, where calling it is a
/// compile error that points here.
/// </summary>
public static class BokmalId
{
    public static Guid New() => Guid.CreateVersion7();
}
