namespace Pancras.Application.Common;

public enum CatalogError
{
    None = 0,
    Incomplete,
    TooLong,
    DuplicateCode,
    Negative,
    TooLarge,
    NotFound,
    PriceListForbidden,
    CostLocked,
    VersionConflict,
    UndecidedSalePrice,
    MissingFilePrice,
    Forbidden
}

public enum CatalogField
{
    Code,
    Name,
    Category,
    Brand,
    Origin,
    Unit,
    Weight,
    Volume,
    WarrantyMonths,
    MinimumStock,
    Height,
    Length,
    Width,
    Image,
    CostPrice,
    RetailPrice,
    DealerPrice,
    DistributorPrice,
    ProjectPrice
}

public enum CatalogIssueKind
{
    Missing,
    TooLong,
    Negative,
    Duplicate,
    InvalidNumber,
    TooLarge
}

public sealed record CatalogIssue(CatalogField Field, CatalogIssueKind Kind, int? Limit = null);
