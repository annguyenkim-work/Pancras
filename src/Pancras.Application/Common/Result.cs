namespace Pancras.Application.Common;

public enum LoginError
{
    None = 0,
    MissingCredentials,
    InvalidCredentials
}

public enum EmployeeError
{
    None = 0,
    ProfileIncomplete,
    InvalidRole,
    NotFound,
    NameUnusable,
    CodeExhausted
}

public enum SupplierError
{
    None = 0,
    Incomplete,
    DuplicateCode,
    InvalidEmail,
    InvalidPhone,
    TooLong,
    NotFound
}

public enum SupplierField
{
    Code,
    Name,
    Country,
    ContactName,
    Phone,
    Email,
    Account
}

public enum SupplierIssueKind
{
    Missing,
    TooLong,
    InvalidEmail,
    InvalidPhone,
    Duplicate
}

public enum CatalogError
{
    None = 0,
    Incomplete,
    TooLong,
    DuplicateCode,
    Negative,
    NotFound,
    PriceListForbidden,
    UndecidedSalePrice,
    MissingFilePrice
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
    InvalidNumber
}

public sealed record CatalogIssue(CatalogField Field, CatalogIssueKind Kind, int? Limit = null);

public sealed record SupplierIssue(SupplierField Field, SupplierIssueKind Kind, int? Limit = null);

public sealed class Result<T>
{
    private Result(
        bool isSuccess,
        T? value,
        LoginError loginError,
        EmployeeError employeeError,
        SupplierError supplierError,
        IReadOnlyList<SupplierIssue>? supplierIssues,
        CatalogError catalogError,
        IReadOnlyList<CatalogIssue>? catalogIssues)
    {
        IsSuccess = isSuccess;
        Value = value;
        LoginError = loginError;
        EmployeeError = employeeError;
        SupplierError = supplierError;
        SupplierIssues = supplierIssues ?? [];
        CatalogError = catalogError;
        CatalogIssues = catalogIssues ?? [];
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T? Value { get; }

    public LoginError LoginError { get; }

    public EmployeeError EmployeeError { get; }

    public SupplierError SupplierError { get; }

    public IReadOnlyList<SupplierIssue> SupplierIssues { get; }

    public CatalogError CatalogError { get; }

    public IReadOnlyList<CatalogIssue> CatalogIssues { get; }

    public static Result<T> Ok(T value) =>
        new(true, value, LoginError.None, EmployeeError.None, SupplierError.None, null, CatalogError.None, null);

    public static Result<T> Fail(LoginError error) =>
        new(false, default, error, EmployeeError.None, SupplierError.None, null, CatalogError.None, null);

    public static Result<T> Fail(EmployeeError error) =>
        new(false, default, LoginError.None, error, SupplierError.None, null, CatalogError.None, null);

    public static Result<T> Fail(SupplierError error) =>
        new(false, default, LoginError.None, EmployeeError.None, error, null, CatalogError.None, null);

    public static Result<T> Fail(SupplierError error, IReadOnlyList<SupplierIssue> issues) =>
        new(false, default, LoginError.None, EmployeeError.None, error, issues, CatalogError.None, null);

    public static Result<T> Fail(CatalogError error) =>
        new(false, default, LoginError.None, EmployeeError.None, SupplierError.None, null, error, null);

    public static Result<T> Fail(CatalogError error, IReadOnlyList<CatalogIssue> issues) =>
        new(false, default, LoginError.None, EmployeeError.None, SupplierError.None, null, error, issues);
}
