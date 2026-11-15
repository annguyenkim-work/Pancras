namespace Pancras.Application.Common;

public enum LoginError
{
    None = 0,
    MissingCredentials,
    InvalidCredentials,
    InvalidCode
}

public enum EmployeeError
{
    None = 0,
    ProfileIncomplete,
    InvalidRole,
    RoleRequired,
    NotFound,
    NameUnusable,
    CodeExhausted,
    Forbidden,
    LocationRequired,
    LocationNotFound,
    LeadExists,
    LastAdmin,
    WeakPassword,
    WrongPassword,
    InvalidCode,
    TwoFactorAlreadyEnabled,
    TwoFactorNotEnabled
}

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
    Forbidden
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

public enum StockError
{
    None = 0,
    InvalidTransfer,
    ShipmentRejected,
    ReceiptRejected,
    CancelRejected,
    ReasonRequired,
    QuantityRequired,
    InvalidText,
    NotYourLocation,
    NotCreator,
    TransferStateChanged,
    InsufficientStock,
    ReturnRejected,
    AttachmentRequired,
    ApprovalRejected,
    TransferNotFound,
    AttachmentInvalid,
    AttachmentTooLarge,
    AttachmentLimit,
    MissingEvidence,
    NegativeStock,
    ApprovalForbidden,
    DisposalRejected,
    Forbidden,
    MissingTarget,
    InvalidQuantity,
    EvidenceTooLong,
    CountRequired,
    InvalidCount
}

public enum PurchaseError
{
    None = 0,
    InvalidOrder,
    DuplicateCode,
    MissingMeasure,
    SplitGroup,
    AlreadyReceived,
    NegativeMoney,
    UngroupedCharge,
    ManualChargeLocked,
    AttachmentInvalid,
    AttachmentTooLarge,
    AttachmentLimit,
    AttachmentNotFound,
    ZeroPrice,
    ChargeFailed,

    /// <summary>Dữ liệu hợp lệ nhưng không ghi được (lỗi hệ thống hoặc có người khác vừa sửa). Khác với <see cref="InvalidOrder"/>.</summary>
    SaveFailed,
    InvalidQuantity,

    /// <summary>Đơn chưa duyệt, đã nhận đủ hoặc đã hủy nên không nhận hàng được.</summary>
    NotReceivable,

    /// <summary>Số thực nhận âm, nhận lố khi chưa phải lần cuối, hoặc không nhận món nào.</summary>
    InvalidReceiptQuantity,

    /// <summary>Lần nhận cuối lệch số đặt mà không ghi lý do.</summary>
    ReceiptNoteRequired,

    /// <summary>Trạng thái hiện tại không cho phép bước chuyển này.</summary>
    InvalidTransition,

    ReasonRequired,

    /// <summary>Lý do trả về nháp hoặc hủy dài hơn 500 ký tự.</summary>
    ReasonTooLong,

    /// <summary>Người khác đã lưu đơn sau khi form này được mở.</summary>
    VersionConflict,

    /// <summary>Số tiền điều chỉnh sau khi làm tròn không vừa cột numeric(18,0).</summary>
    AmountTooLarge,

    /// <summary>Đơn đã có lần nhận hàng hoặc đã hủy nên không sửa được nữa.</summary>
    NotEditable,

    /// <summary>Khoản điều chỉnh sau nhập không hợp lệ (đơn chưa nhận đủ, số tiền 0, thiếu tên hoặc lý do).</summary>
    AdjustmentInvalid,

    /// <summary>Vai trò hiện tại không được thực hiện thao tác này.</summary>
    Forbidden,

    /// <summary>Đơn đã hủy nên không nhận thêm file đính kèm.</summary>
    OrderCancelled
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
        IReadOnlyList<CatalogIssue>? catalogIssues,
        StockError stockError,
        PurchaseError purchaseError,
        string? detail = null)
    {
        IsSuccess = isSuccess;
        Value = value;
        LoginError = loginError;
        EmployeeError = employeeError;
        SupplierError = supplierError;
        SupplierIssues = supplierIssues ?? [];
        CatalogError = catalogError;
        CatalogIssues = catalogIssues ?? [];
        StockError = stockError;
        PurchaseError = purchaseError;
        Detail = detail;
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

    public StockError StockError { get; }

    public PurchaseError PurchaseError { get; }

    /// <summary>Chi tiết ngắn đi kèm lỗi (ví dụ mã SKU thiếu hàng). Là dữ liệu thô, trang tự ghép thành câu.</summary>
    public string? Detail { get; }

    public static Result<T> Ok(T value) =>
        new(true, value, LoginError.None, EmployeeError.None, SupplierError.None, null, CatalogError.None, null, StockError.None, PurchaseError.None);

    public static Result<T> Fail(LoginError error) =>
        new(false, default, error, EmployeeError.None, SupplierError.None, null, CatalogError.None, null, StockError.None, PurchaseError.None);

    public static Result<T> Fail(EmployeeError error) =>
        new(false, default, LoginError.None, error, SupplierError.None, null, CatalogError.None, null, StockError.None, PurchaseError.None);

    public static Result<T> Fail(SupplierError error) =>
        new(false, default, LoginError.None, EmployeeError.None, error, null, CatalogError.None, null, StockError.None, PurchaseError.None);

    public static Result<T> Fail(SupplierError error, IReadOnlyList<SupplierIssue> issues) =>
        new(false, default, LoginError.None, EmployeeError.None, error, issues, CatalogError.None, null, StockError.None, PurchaseError.None);

    public static Result<T> Fail(CatalogError error) =>
        new(false, default, LoginError.None, EmployeeError.None, SupplierError.None, null, error, null, StockError.None, PurchaseError.None);

    public static Result<T> Fail(CatalogError error, IReadOnlyList<CatalogIssue> issues) =>
        new(false, default, LoginError.None, EmployeeError.None, SupplierError.None, null, error, issues, StockError.None, PurchaseError.None);

    public static Result<T> Fail(StockError error) =>
        new(false, default, LoginError.None, EmployeeError.None, SupplierError.None, null, CatalogError.None, null, error, PurchaseError.None);

    public static Result<T> Fail(StockError error, string detail) =>
        new(false, default, LoginError.None, EmployeeError.None, SupplierError.None, null, CatalogError.None, null, error, PurchaseError.None, detail);

    public static Result<T> Fail(PurchaseError error) =>
        new(false, default, LoginError.None, EmployeeError.None, SupplierError.None, null, CatalogError.None, null, StockError.None, error);
}
