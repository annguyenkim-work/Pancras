# Công nghệ và phiên bản

> Trạng thái: **Đã chốt** (phiên 2026-11-16).

**Mục đích:** Khóa phiên bản và danh sách thư viện đang được phép dùng. Thêm thư viện mới phải **hỏi chủ dự án trước** (xem mục 3).

## 1. Nền tảng

| Hạng mục | Lựa chọn |
| --- | --- |
| Ngôn ngữ / runtime | C# trên **.NET 10** (`net10.0`), `Nullable` và `ImplicitUsings` bật |
| Web | **ASP.NET Core Razor Pages**, xác thực cookie |
| ORM | **Entity Framework Core 10** |
| Cơ sở dữ liệu | **PostgreSQL** (Npgsql), tên bảng/cột `snake_case` |
| CSS | **Tailwind CSS 3.4** (build bằng npm, `site.css` commit vào git) |
| Phông chữ | DM Sans, Plus Jakarta Sans, JetBrains Mono, **tự host** trong `wwwroot/fonts` (không dùng Google Fonts) |
| Kiểm thử | xUnit, EF Core InMemory, `Microsoft.AspNetCore.Mvc.Testing` |

## 2. Thư viện đang được phép

| Gói | Dự án | Dùng để |
| --- | --- | --- |
| `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.x | Infrastructure | Truy cập PostgreSQL |
| `Microsoft.EntityFrameworkCore.Design` 10.0.x | Infrastructure | Công cụ migration |
| `EFCore.NamingConventions` 10.0.x | Infrastructure | Đặt tên `snake_case` |
| `Microsoft.Extensions.Identity.Core` 10.0.x | Infrastructure | Băm mật khẩu (PBKDF2) |
| `FluentValidation` 12.x | Application | Kiểm tra dữ liệu đầu vào của lệnh |
| `Microsoft.Extensions.Logging.Abstractions` 10.0.x | Application | `ILogger<T>` |
| `QRCoder` 1.8.x | Web/Infrastructure | Mã QR cho cài 2FA |
| `tailwindcss` 3.4.17 | Web (npm, dev) | Build CSS |
| `xunit`, `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, `coverlet.collector` | Tests | Chạy test |
| `Microsoft.EntityFrameworkCore.InMemory` | Tests | CSDL giả cho test |
| `Microsoft.AspNetCore.Mvc.Testing` | Web.Tests | Test trang |

- Phiên bản phụ/bản vá có thể nâng khi cần; nâng **phiên bản chính** (ví dụ .NET, EF Core, Tailwind 4) phải hỏi trước.
- Cấu hình dùng chung: `Directory.Build.props` (`Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`), `global.json` (ghim SDK 10.0.402, `rollForward: latestFeature`), `.editorconfig` — xem [build-and-git.md](../02-coding-rules/build-and-git.md).

## 3. Quy trình thêm thư viện

1. **Hỏi chủ dự án trước**, nêu: thư viện gì, để làm gì, tại sao thư viện chuẩn .NET không đủ.
2. **Mặc định ưu tiên thư viện chuẩn .NET**; **không thêm thư viện cho việc nhỏ** viết được trong vài chục dòng.
3. Được duyệt thì ghi vào bảng ở mục 2 (gói, dự án, mục đích) trong cùng thay đổi.
4. Không thêm: framework JS, thư viện CSS khác, CDN ngoài, dịch vụ trả phí hay gọi ra ngoài (kể cả gửi email) khi chưa được duyệt.

## 4. Phụ thuộc ngoài đang có

| Thứ | Hiện trạng | Quyết định |
| --- | --- | --- |
| Excel | Xuất "Excel" thực ra là CSV | Giữ CSV; xem Chờ chốt |

## Nợ lệch so với quy định

| Vấn đề | Hiện trạng |
| --- | --- |
| Gói dùng chung | `Microsoft.Extensions.Logging.Abstractions` 10.0.4 (Application) và 10.0.5 (Infrastructure.Tests, do `EntityFrameworkCore.InMemory` 10.0.5 yêu cầu); sẽ thống nhất khi nâng cả solution lên 10.0.5 |

## Chờ chốt (chưa có hiệu lực)

- **Đã chốt 2026-11-16:** không xuất `.xlsx`; mọi nút xuất ghi nhãn `Xuất CSV` (không thêm thư viện).
- Quét virus cho tệp tải lên (hiện không có; chỉ cho phép pdf, jpg, png, xlsx, docx, giới hạn 10 MB).
- Giám sát lỗi và log tập trung.
