# Phân tầng và phụ thuộc

> Trạng thái: **Đã chốt một phần** (phiên 2026-11-16). Phần dưới "Hiện trạng đã đúng" lấy từ code; phần "Chờ chốt" chưa có hiệu lực.

**Mục đích:** Luật phụ thuộc giữa các tầng và những thứ mỗi tầng được/không được làm.

## Chiều phụ thuộc

`Domain` ← `Application` ← `Infrastructure` ← `Web`. Project tham chiếu theo đúng chiều này (kiểm tra ở `*.csproj`):

| Project | Tham chiếu | Gói NuGet nổi bật |
| --- | --- | --- |
| Domain | không | không |
| Application | Domain | FluentValidation, Logging.Abstractions |
| Infrastructure | Domain, Application | EF Core + Npgsql, EFCore.NamingConventions, Identity.Core (băm mật khẩu), QRCoder |
| Web | Domain, Application, Infrastructure | — |

## Được và không được

| Tầng | Chứa | Không được |
| --- | --- | --- |
| Domain | Thực thể, bất biến, enum, lớp `<Module>Access` | Dùng ASP.NET, EF Core, I/O; định dạng tiền/ngày/HTML |
| Application | Handler (use case), interface repository, enum lỗi, validator, DTO | Dùng ASP.NET/Razor/EF Core trực tiếp; trả HTML hoặc chữ hiển thị |
| Infrastructure | DbContext, repository, migration, băm mật khẩu, lưu tệp | Chứa luật nghiệp vụ |
| Web | PageModel, `.cshtml`, định dạng hiển thị, map mã lỗi sang chữ | Gọi Infrastructure ngoài `Program.cs`; tự tính nghiệp vụ trong view |

- **Web không gọi Infrastructure**, trừ khi đăng ký DI trong `Program.cs`. Hiện chỉ `Program.cs` có `using Pancras.Infrastructure`.
- **Domain/Application không định dạng** tiền, ngày, câu thông báo, HTML. Trả số `decimal`, `DateTimeOffset`, mã lỗi (enum).
- `.cshtml` chỉ vẽ; PageModel gọi handler của Application; không gọi repository trực tiếp từ PageModel.
- Phụ thuộc đi vào trong: Application chỉ biết repository qua interface; cài đặt nằm ở Infrastructure.

## Kiểm tra hiện trạng (2026-11-16)

- Domain và Application không `using` ASP.NET hay EF Core: **đạt**.
- Web chỉ dùng Infrastructure ở `Program.cs`: **đạt**.

## Chờ chốt (chưa có hiệu lực)

- **Đã chốt và làm 2026-11-16:** test kiến trúc bằng NetArchTest (`tests/Pancras.Infrastructure.Tests/ArchitectureTests.cs`) cấm Domain/Application/Infrastructure phụ thuộc sai chiều. Quy tắc "Web không gọi Infrastructure ngoài `Program.cs`" chưa có test tự động.
- **Chốt kỹ thuật 2026-11-16 (đề xuất của AI theo thực tế code, chủ dự án có thể đổi):** trang `.cshtml` quá khoảng 500 dòng thì tách thành partial theo khối (đã làm cho Kho và Mua hàng); ngoại lệ phải có lý do. Handler dài chia theo bước, không gộp nhiều use case vào một lớp.
- **Chốt kỹ thuật 2026-11-16 (đề xuất của AI theo thực tế code, chủ dự án có thể đổi):** mỗi project tổ chức theo module (`Catalog`, `Suppliers`, `Inventory`, `Purchasing`, `Employees`, `Notifications`, `Auditing`...), phần dùng chung ở `Common`; module mới theo mẫu của `catalog` hoặc `suppliers`.
