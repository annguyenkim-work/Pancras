# Quy ước đặt tên

> Trạng thái: **Đã chốt một phần** (phiên 2026-11-16).

**Mục đích:** Đặt tên class, handler, file, method, bảng, route để đọc tên là biết nó làm gì và nằm ở đâu.

## Đã chốt

### Use case và handler

- **Ghi (đổi dữ liệu):** `<Action><Entity>CommandHandler` — ví dụ `CreateEmployeeCommandHandler`, `ApproveDisposalSlipCommandHandler`.
- **Đọc (truy vấn):** `<Verb><Entity>Handler` với động từ `List`, `Get`, `Resolve`, `Export` — ví dụ `ListEmployeesHandler`, `GetSupplierProfileHandler`.
- **Mỗi handler một file, tên file trùng tên class.** Command/query và kiểu kết quả của handler đó nằm cùng file.
- Handler nằm trong `Application/<Module>/`, namespace `Pancras.Application.<Module>`.

### Kiểu đầu vào và đầu ra của use case

- **Lệnh ghi:** `<Action><Entity>Command` (ví dụ `CreateEmployeeCommand`). Dòng con của lệnh: `<Entity>LineInput` / `<...>Input`.
- **Truy vấn đọc:** `<Verb><Entity>Query` (ví dụ `ListEmployeesQuery`, `GetSupplierProfileQuery`), đi cùng handler `<Verb><Entity>Handler`. Bộ lọc và phân trang là thuộc tính của Query, không phải kiểu rời `...ListFilter` / `...PageRequest`.
- **Kết quả đọc** (hậu tố theo vai trò, đang dùng trong code; không dùng `Dto`):

| Hậu tố | Nghĩa |
| --- | --- |
| `Row` | Một dòng trong bảng |
| `Card` | Một thẻ trong danh sách thẻ |
| `Page` | Kết quả phân trang: `Items`, `Total`, `PageNumber`, `PageSize` |
| `Detail` | Chi tiết đầy đủ một bản ghi |
| `Profile` | Hồ sơ người/đối tác |
| `View` | Phần phụ chỉ đọc |
| `Summary` | Bản tóm tắt |

### Interface, repository, lỗi

- Interface bắt đầu bằng `I`. Repository: `I<Entity>Repository` (hoặc `I<Module>Repository`), khai báo trong Application, cài đặt ở `Infrastructure/Persistence`.
- **Mỗi interface một file riêng**, tên file trùng tên interface.
- **Enum lỗi nghiệp vụ:** `<Module>Error` (ví dụ `EmployeeError`, `SupplierError`), mỗi module một file `<Module>Errors.cs` trong thư mục module. `Common/Result.cs` chỉ giữ kiểu `Result` dùng chung.
- Lớp phân quyền trong Domain: `<Module>Access`, mỗi lớp một file trong thư mục module của nó.

### C# chung

- Class, method, property, enum, enum member: `PascalCase`. Method bất đồng bộ có hậu tố `Async`.
- Tham số, biến cục bộ: `camelCase`. Trường private: `_camelCase` hoặc primary constructor, thống nhất trong một file.
- `Nullable` bật; không để cảnh báo biên dịch (`--warnaserror`).

### Cơ sở dữ liệu và route

- Bảng và cột trong PostgreSQL: `snake_case` (do `EFCore.NamingConventions`); thuộc tính C# vẫn `PascalCase`.
- Route (URL) bằng tiếng Anh: `/inventory`, `/purchasing`, `/employees`, `/catalog`, `/suppliers`. Chữ trên UI bằng tiếng Việt.

## Nợ lệch so với quy ước

| Vấn đề | Hiện trạng |
| --- | --- |
| Handler gộp file | `Employees/TwoFactorHandlers.cs` (5 handler), `Inventory/TransferWorkflowHandlers.cs` (7), `Purchasing/PurchaseWorkflowHandlers.cs` (2), `Inventory/StocktakeApplication.cs`, `*AttachmentHandlers.cs` — tách khi chạm tới |
| Interface gộp file | Repository và DTO gom trong `<Module>Contracts.cs` (Employees, Catalog, Inventory, Locations, Auditing) — tách mỗi interface một file |
| Enum lỗi gộp một file | Toàn bộ `LoginError`, `EmployeeError`, `SupplierError`... nằm trong `Common/Result.cs` |
| Lớp Access lạc chỗ | `SupplierAccess`, `LocationAccess` nằm trong `EmployeeAccess.cs`; `InventoryAccess` nằm trong `InventoryText.cs` |
| Truy vấn không có `Query` | Đang dùng `CatalogListFilter`, `StockListFilter`, `SupplierListFilter`, `CatalogPageRequest`, `SupplierPageRequest`; handler đọc nhận tham số rời — đổi thành `<Verb><Entity>Query` khi chạm tới |
| Hậu tố khác | `*Input` ngoài dòng con (`ReceivedLineInput`...), `CreatedCatalogItem`, `*Result` (`TwoFactorEnabledResult`) cần rà lại tên |

## Chờ chốt (chưa có hiệu lực)

- Các hậu tố kết quả trong bảng trên là ghi nhận lại từ code, chưa được rà từng cái; xác nhận khi viết `04-domain/modules`.
- Tên PageModel, handler của Razor Page (`OnPostXxxAsync`) và tên file partial (`_Xxx.cshtml`).
- Tên test (`MethodName_State_Expected` hay tiếng Việt) — xem `testing.md`.
