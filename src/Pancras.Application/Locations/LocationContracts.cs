using Pancras.Application.Auditing;
using Microsoft.Extensions.Logging;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Locations;

public enum LocationError
{
    None = 0,
    Forbidden,
    NameMissing,
    NameTooLong,
    InvalidKind,
    DuplicateName
}

public sealed record LocationOutcome(Guid? Id, LocationError Error)
{
    public bool IsSuccess => Error == LocationError.None;
}

public sealed class CreateLocationCommand
{
    public string? Name { get; set; }

    /// <summary>Null khi không gửi hoặc gửi giá trị không đọc được: model binding bỏ qua giá trị lạ, nên không được mặc định về Kho.</summary>
    public StockLocationKind? Kind { get; set; }
}

public enum LocationManageError
{
    None = 0,
    Forbidden,
    NotFound,
    NameMissing,
    NameTooLong,
    InvalidKind,
    DuplicateName,
    HasStock,
    HasActiveEmployees,
    HasOpenDocuments,
    SaveFailed,
    VersionConflict
}

public sealed class UpdateLocationCommand
{
    public Guid Id { get; set; }

    /// <summary>Phiên bản lúc mở form; khác phiên bản hiện tại nghĩa là có người sửa trước.</summary>
    public int Version { get; set; }

    public string? Name { get; set; }

    public StockLocationKind? Kind { get; set; }
}
