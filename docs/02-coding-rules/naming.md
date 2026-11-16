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
| Hậu tố khác | `*Input` ngoài dòng con (`ReceivedLineInput`...), `CreatedCatalogItem`, `*Result` (`TwoFactorEnabledResult`) cần rà lại tên |

## Quyết định đã chốt (2026-11-16)

- [Đề xuất của AI, chủ dự án đã duyệt] PageModel tên `IndexModel` trong thư mục trang; handler Razor Page `OnGet`/`OnPost<HànhĐộng>Async`; partial `_<TênKhối>.cshtml` cùng thư mục trang (hoặc `Shared/` nếu dùng chung).
- [Đề xuất của AI, chủ dự án đã duyệt] Tên test tiếng Anh dạng `<Hành động>_<Kết quả mong đợi>` (ví dụ `Create_RejectsBlankName`), như code hiện có.

## Chờ chốt (chưa có hiệu lực)

- Các hậu tố kết quả trong bảng trên là ghi nhận lại từ code, chưa được rà từng cái; xác nhận khi viết `04-domain/modules`.
