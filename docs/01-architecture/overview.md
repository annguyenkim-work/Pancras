# Kiến trúc tổng thể

> Trạng thái: **Đã chốt** (phiên 2026-11-16). Quy tắc chi tiết từng tầng: [../02-coding-rules/layering-and-dependencies.md](../02-coding-rules/layering-and-dependencies.md).

**Mục đích:** Bức tranh tổng thể: các tầng, cách một yêu cầu đi qua hệ thống, cấu trúc thư mục.

## 1. Mẫu kiến trúc

**Clean Architecture** bốn tầng, phụ thuộc chỉ đi vào trong. Luật nghiệp vụ (Domain, Application) **không** phụ thuộc framework, giao diện hay hạ tầng.

```
┌──────────────────────────────────────────────────────────┐
│  Web (Razor Pages, cookie auth, định dạng, DI composition)│
│  ┌────────────────────────────────────────────────────┐  │
│  │ Infrastructure (EF Core/PostgreSQL, repository,    │  │
│  │ migration, băm mật khẩu, lưu tệp)                  │  │
│  │  ┌──────────────────────────────────────────────┐  │  │
│  │  │ Application (handler = use case, validator,  │  │  │
│  │  │ interface repository, enum lỗi, DTO)         │  │  │
│  │  │  ┌────────────────────────────────────────┐  │  │  │
│  │  │  │ Domain (thực thể, bất biến, *Access)   │  │  │  │
│  │  │  └────────────────────────────────────────┘  │  │  │
│  │  └──────────────────────────────────────────────┘  │  │
│  └────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────┘
```

Chiều phụ thuộc: `Web → Infrastructure → Application → Domain`. Web chỉ tham chiếu Infrastructure để **đăng ký DI trong `Program.cs`**.

## 2. Một yêu cầu đi qua hệ thống

1. **Trình duyệt → Razor Page.** Cookie xác thực đã được `EmployeePrincipalValidator` kiểm lại (còn hoạt động, `SecurityStamp` khớp); `FallbackPolicy` bắt buộc đăng nhập.
2. **PageModel** (`Index.cshtml.cs`) đọc form/query, dựng **command hoặc query**, lấy người thao tác (`EmployeeActor`) từ phiên.
3. **Handler (Application)**: kiểm quyền (`<Module>Access`), validate (FluentValidation), gọi thực thể Domain, gọi **repository** (interface), ghi audit, trả `Result` kèm mã lỗi.
4. **Repository (Infrastructure)**: một phương thức = một giao dịch; EF Core; bắt lỗi trùng khóa/xung đột và đổi thành mã lỗi.
5. **PageModel** map mã lỗi sang chữ tiếng Việt, đặt toast hoặc lỗi inline, redirect (PRG) hoặc render lại.
6. **`.cshtml`** chỉ vẽ HTML theo `Model.CanX` và dữ liệu đã được handler lọc theo vai.

## 3. Cấu trúc solution

`Pancras.slnx`:

```
src/
  Pancras.Domain/          Auditing, Catalog, Employees, Inventory, Purchasing, Suppliers
  Pancras.Application/     Auditing, Catalog, Common, Employees, Inventory, Locations, Purchasing, Suppliers
  Pancras.Infrastructure/  Identity (băm mật khẩu), Persistence (DbContext, repository, cấu hình, Migrations), Seeding (rỗng)
  Pancras.Web/             Pages, Formatting, Security, Catalog, Styles, wwwroot, Program.cs
tests/
  Pancras.Domain.Tests/  Pancras.Application.Tests/  Pancras.Infrastructure.Tests/  Pancras.Web.Tests/
docs/                      bộ tài liệu chuẩn
```

- Trong Domain và Application, **mỗi thư mục là một module** (nghiệp vụ). Web tổ chức theo trang: `Pages/<Module>/Index.cshtml(.cs)` cho mỗi module, cùng các lớp phụ (`*ErrorMessages.cs`, partial `_*.cshtml`); partial dùng chung ở `Pages/Shared`.
- `Web/Formatting` giữ định dạng hiển thị (tiền, nhãn, CSV); `Web/Security` giữ xác thực phía web (`AdminBootstrap`, `EmployeePrincipalValidator`, `RequestInfo`).
- Mỗi module có docs ở [04-domain/modules](../04-domain/modules/accounts-and-employees.md).

## 4. Module hiện có và trạng thái

Xem [../00-overview/product.md](../00-overview/product.md).

## Nợ lệch so với kiến trúc

| Vấn đề | Hiện trạng |
| --- | --- |
| Trang to | Mỗi module một trang `Index` rất dài (Inventory hơn 1.000 dòng) với script inline; cần tách thành các phần/partial hoặc nhiều trang theo tab |
| Contracts | Interface repository và DTO gom trong `<Module>Contracts.cs`; tách mỗi interface một file |
| Domain/Application dùng chung | `Auditing` có ở cả Domain và Application nhưng `Locations` chỉ có ở Application; cân nhắc thống nhất khi làm lại module Kho & Showroom |

## Chờ chốt (chưa có hiệu lực)

- Có tách `Application` theo module thành project riêng khi hệ thống lớn hơn hay giữ một project.
- Giám sát, health check, và cấu trúc deployment (xem `06-operations`).
