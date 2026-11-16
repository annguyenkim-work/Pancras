namespace Pancras.Application.Common;

public enum SupplierError
{
    None = 0,
    Incomplete,
    DuplicateCode,
    InvalidEmail,
    InvalidPhone,
    TooLong,
    NotFound,
    HasOpenPurchases,
    InvalidSwift,
    Forbidden,
    VersionConflict
}

public enum SupplierField
{
    Code,
    Name,
    Country,
    ContactName,
    Phone,
    Email,
    BankName,
    Account,
    SwiftCode
}

public enum SupplierIssueKind
{
    Missing,
    TooLong,
    InvalidEmail,
    InvalidPhone,
    Duplicate,
    DuplicateInactive,
    InvalidSwift
}

public sealed record SupplierIssue(SupplierField Field, SupplierIssueKind Kind, int? Limit = null);
