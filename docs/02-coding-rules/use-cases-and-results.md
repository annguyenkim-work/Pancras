# Use case, Result và lỗi nghiệp vụ

> Trạng thái: **Đã chốt một phần** (phiên 2026-11-16).

**Mục đích:** Một use case viết thế nào, trả kết quả ra sao, kiểm tra đầu vào ở đâu, đăng ký DI thế nào.

## Đã chốt

### Một use case = một handler

- Mỗi use case là một class handler, một file (xem [naming.md](naming.md)). Command/query, validator và kiểu kết quả của nó nằm cùng file.
- Handler nhận command/query và `CancellationToken`, trả `Result`-style; **không** trả HTML, `IActionResult` hay chữ hiển thị.
- Mọi handler inject `ILogger<T>` (xem `async-logging-errors.md`).
- **Đăng ký DI bằng tay** trong `src/Pancras.Web/Program.cs` (scoped), cùng với repository `I*Repository` → cài đặt ở `Infrastructure/Persistence`. Thêm handler mà không đăng ký sẽ lỗi lúc chạy; không dùng quét assembly tự động.

### Kết quả, không ném exception

- **Lỗi nghiệp vụ trả bằng giá trị, không ném exception.** Handler trả `Result`/`Result<T>` kèm enum lỗi của module (`EmployeeError`, `SupplierError`, `LoginError`...).
- Application/Domain không tạo câu tiếng Việt. PageModel map enum lỗi sang chữ ở một chỗ cho mỗi module (ví dụ `SupplierErrorMessages.cs`).
- Page handler không có `try/catch` cho lỗi nghiệp vụ; lỗi bất ngờ để `UseExceptionHandler` ở `Program.cs` xử lý.
- Enum lỗi: mỗi module một file `<Module>Errors.cs` trong thư mục module; luôn có `None = 0`.

### Kiểm tra dữ liệu đầu vào

- **Bắt buộc dùng FluentValidation cho mọi command có input người dùng.** Validator `<Command>Validator : AbstractValidator<<Command>>` nằm cùng file handler.
- Mỗi luật gắn `WithErrorCode(nameof(<Module>Error.X))` để map ngược sang enum lỗi; không dùng chuỗi lỗi tự do.
- Bất biến của thực thể (ví dụ tồn kho không âm) nằm trong Domain và vẫn được kiểm ở đó; validator chỉ chặn input sai trước khi vào Domain.
- Kiểm tra quyền (`<Module>Access`) nằm ở handler, trước khi đụng dữ liệu (xem [role-based-ui.md](../03-ui-ux/role-based-ui.md)).

### Dạng `Result`

- Chuẩn là **`Result<T, TError>` generic**, trong đó `TError` là enum lỗi riêng của module (`EmployeeError`...), thành công mang `Value`, thất bại mang `Error`. Thêm module không sửa kiểu chung.
- Lỗi theo trường (danh sách `Issue`) là kiểu riêng của module, đi kèm trong kết quả của module đó.
- Class `Result<T>` hiện tại (một thuộc tính lỗi cho mỗi module) là di sản; chuyển dần khi chạm tới handler.

### Chữ ký handler

- Mỗi handler có **đúng một phương thức công khai `HandleAsync(command/query, CancellationToken)`**. Hàm phụ là `private`/`internal`.

### Audit

- Handler ghi nhật ký qua `IAuditLog.RecordAsync` **sau khi lưu thành công** cho: mọi thao tác ghi dữ liệu (tạo, sửa, duyệt, hủy, đổi trạng thái) và sự kiện bảo mật (đăng nhập thất bại, khóa/mở khóa, đặt lại mật khẩu, đổi vai trò, từ chối do thiếu quyền).
- Không ghi thao tác đọc.
- `detail` không chứa mật khẩu, mã xác thực, token hay PII.
- Lỗi khi ghi nhật ký chỉ log cảnh báo, **không làm hỏng thao tác chính**.
- `action` là **mã dạng `nhóm.sự_kiện`** (hằng số trong `AuditActions`, ví dụ `login.failed`, `employee.role_changed`, `stock.transfer_approved`); **Web đổi mã sang câu tiếng Việt** khi hiển thị. `targetType` và `targetId` đặt theo cùng một quy ước trong cả module.

### Giao dịch

- Giao dịch nằm trong repository: **một phương thức repository = một giao dịch trọn vẹn**. Handler không mở giao dịch, không có `IUnitOfWork`.
- Thao tác cần nhiều bước phải là một phương thức repository có tên nghiệp vụ (ví dụ tạo phiếu chuyển kho rồi xuất xe). Chi tiết EF xem `data-access.md`.

## Mẫu handler (rút từ `CreateEmployeeCommandHandler`)

```csharp
public sealed class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(c => c.FullName)
            .Must(name => EmployeeRules.Present(name, 200))
            .WithErrorCode(nameof(EmployeeError.ProfileIncomplete));
        // ...
    }
}

public sealed class CreateEmployeeCommandHandler( /* repositories, ILogger<...> */ )
{
    public async Task<Result<...>> HandleAsync(CreateEmployeeCommand command, CancellationToken cancellationToken)
    {
        // 1) kiểm quyền  2) validate  3) gọi Domain  4) lưu  5) ghi audit  6) trả Result
    }
}
```

## Nợ lệch so với quy ước

| Vấn đề | Hiện trạng |
| --- | --- |
| Kiểm tra viết tay | `CatalogValidation.cs`, `SupplierIssue`/`CatalogIssue` — không đi qua FluentValidation; chuyển khi chạm tới |
| Tên phương thức | 63 handler dùng `HandleAsync`; ngoại lệ `ListTransfersAsync`, `ListStocktakesAsync`, `ListLocationsAsync`, `ListItemsAsync`, `ListDisposalsAsync`, `ListCountriesAsync`, `ListAdjustmentsAsync`, `ExportAsync`... cần đổi hoặc tách |
| Kiểu trả về | Vài handler đọc trả thẳng `StockItemDetail?`, `SupplierCardPage` thay vì `Result` |

## Chờ chốt (chưa có hiệu lực)

- Ghi chi tiết quy ước `action`/`targetType`/`targetId` của audit cho từng module — làm ở `04-domain/modules/notifications-and-audit.md`.
