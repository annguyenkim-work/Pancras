# Truy cập dữ liệu và migration

> Trạng thái: **Đã chốt** (phiên 2026-11-16).

**Mục đích:** Cách dùng EF Core, repository, concurrency, xóa mềm và migration. Kết hợp với quyết định "giao dịch nằm trong repository" ở [use-cases-and-results.md](use-cases-and-results.md).

Công nghệ: PostgreSQL qua Npgsql, EF Core 10, đặt tên `snake_case` (`EFCore.NamingConventions`). Code ở `src/Pancras.Infrastructure/Persistence`.

## 1. Cấu hình thực thể

- **Mỗi thực thể một `IEntityTypeConfiguration`** (ví dụ `CatalogItemConfiguration`). Không rải attribute EF trong Domain.
- Tiền và số thập phân: `HasPrecision`. Chuỗi: `HasMaxLength`. Giá trị bắt buộc: `IsRequired`.
- **Id là `Guid` v7 do ứng dụng tạo** (`Guid.CreateVersion7()` trong Domain), không do database sinh.
- Cột và bảng `snake_case` tự động; thuộc tính C# vẫn `PascalCase`.
- Dấu vết bản ghi (`UpdatedAt`, `UpdatedBy`) do `AuditStampInterceptor` gắn tự động cho thực thể kế thừa `AuditStamped`; không tự gán trong handler.

## 2. Truy vấn

- **Đọc dùng `AsNoTracking()`.** Chỉ nạp entity có tracking khi sắp ghi.
- **Không lazy loading.** Tránh N+1: dùng `Include` hoặc `Select` có chủ đích.
- **Phân trang và lọc chạy trong database** (`Skip`/`Take`, `Where`); không nạp cả bảng rồi cắt trong bộ nhớ.
- Danh sách lớn dùng `Select` ra kiểu `Row`, không nạp cả đồ thị thực thể.
- Mọi thao tác I/O là `async` có `CancellationToken`.

## 3. Repository

- Interface `I*Repository` ở Application, mỗi interface một file; cài đặt ở `Infrastructure/Persistence`.
- **Một phương thức repository = một giao dịch trọn vẹn.** Thao tác nhiều bước là một phương thức có tên nghiệp vụ (xem use-cases-and-results.md). Handler không mở giao dịch.
- **Lỗi trùng khóa (unique violation) bắt trong repository** và trả `bool`/mã lỗi (ví dụ `TryAddAsync` trả `false`), không để `DbUpdateException` lọt ra handler.
- Đặt tên theo ý: `...ReadOnlyAsync` cho đọc không tracking, `...ForUpdateAsync` cho đọc để ghi.

## 4. Concurrency

- **Mọi thực thể có thể bị hai người sửa cùng lúc phải có `Version`** (số nguyên, `IsConcurrencyToken`). Hiện có ở `StockBalance`, `CatalogItem`, `PurchaseOrder`; `Employee`, `Supplier`, `StockLocation`, `TransferSlip` kế thừa `VersionedAuditStamped`, nên `AuditStampInterceptor` tự tăng `Version` mỗi lần có thay đổi thật.
- Lần ghi thua bị **từ chối, không tự retry**, không đổi số liệu.
- Repository bắt `DbUpdateConcurrencyException` và trả mã lỗi **Conflict** của module; UI hiện câu tiếng Việt ("Dữ liệu vừa được người khác cập nhật, hãy tải lại trang.", xem `formatting-and-copy.md`).
- Khi thêm thực thể mới: tự hỏi "hai người có sửa cùng lúc được không?"; nếu có thì thêm `Version`.

## 5. Xóa mềm

- **Không xóa cứng dữ liệu nghiệp vụ.** Người, đối tác, danh mục, kho/showroom dùng **`IsActive`** (vô hiệu hóa, kích hoạt lại); vẫn hiện trong lịch sử.
- Phiếu, đơn: chuyển trạng thái `Hủy`, không xóa.
- Một kiểu duy nhất là `IsActive`. Bộ lọc mặc định ("chỉ đang hoạt động") áp ở truy vấn đọc của từng use case, không dùng query filter toàn cục.

## 6. Migration

- Dùng project Infrastructure làm cả project và startup project:

```bash
dotnet ef migrations add <Name> --project src/Pancras.Infrastructure --startup-project src/Pancras.Infrastructure
dotnet ef database update --project src/Pancras.Infrastructure --startup-project src/Pancras.Infrastructure
```

- **Local:** chạy `database update` ngay sau khi thêm migration.
- **Production: không tự migrate lúc ứng dụng khởi động.** Áp dụng chủ động khi deploy bằng script (`dotnet ef migrations script`) hoặc bundle, **sao lưu database trước**.
- Tên migration `PascalCase` mô tả thay đổi (`AddCatalogItemVersion`). Một migration cho một thay đổi có ý nghĩa.
- **Không sửa migration đã áp dụng**; sai thì thêm migration mới.
- Kèm migration phải có test/kiểm tra thủ công cho dữ liệu cũ khi đổi cột có dữ liệu.
- Chuỗi kết nối nằm ở user-secrets (id `pancras-web-7c1a9e2b`), không đưa vào git (xem `security-and-authorization.md`).

## Nợ lệch so với quy tắc

| Vấn đề | Hiện trạng |
| --- | --- |
| `Version` chưa trọn vẹn | Lần ghi chồng nhau thật sự đã thành `VersionConflict` của Danh mục, Nhà cung cấp, Kho/showroom (repository trả `SaveOutcome.Conflict`). Nhân viên: sửa và Disable/Active cũng trả `VersionConflict` (`TrySaveChangesAsync`); các thao tác nhân viên khác (đổi mật khẩu, 2FA, đăng nhập) vẫn lưu trực tiếp. Mua hàng và Kho gộp chung vào `SaveFailed`. `TransferSlip` chỉ dựa vào kiểm tra trạng thái (`TransferStateChanged`), form không gửi `Version` |
| Gạch dưới tên | Bảng "đọc không tracking" (`...ReadOnlyAsync`) chưa nhất quán ở mọi repository |

## Chờ chốt (chưa có hiệu lực)

- **Chốt kỹ thuật 2026-11-16 (đề xuất của AI theo thực tế code, chủ dự án có thể đổi):** thêm chỉ mục khi có truy vấn lọc/sắp xếp/khóa duy nhất thật sự cần (mã duy nhất, khóa ngoại hay lọc); mỗi chỉ mục mới đi kèm migration và lý do trong mô tả migration; không thêm "phòng xa".
- Quy tắc sao lưu và khôi phục database: ghi ở `06-operations/deployment.md` (RPO/RTO 1 ngày, giữ 14 ngày).
- Quy tắc sao lưu và khôi phục database — để ở `06-operations/deployment.md`.
- **Đã chốt và làm 2026-11-16:** danh sách rất lớn dùng phân trang keyset (mất nhảy tới trang bất kỳ và tổng số dòng); các danh sách còn lại giữ Skip/Take 20/50. Hiện đã áp cho nhật ký hệ thống (`AuditCursor`, `_KeysetPager`). "Lịch sử tồn" hiện chưa có danh sách phân trang riêng; khi làm thì dùng keyset theo mẫu này.
