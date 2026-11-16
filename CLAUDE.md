# CLAUDE.md

Hướng dẫn cho Claude Code khi làm việc trong repo này. **Nguồn luật duy nhất là bộ docs ở [`docs/`](docs/README.md)**; file này chỉ giữ lệnh chính và vài luật cứng. Không có luật nào khác (không còn `.cursorrules`, `specs/`, `.specify/`).

## Dự án

PancrasERP: ERP tiếng Việt cho thương mại thiết bị vệ sinh và nội thất (.NET 10, EF Core 10, PostgreSQL, Razor Pages, xác thực cookie). UI tiếng Việt, URL tính năng tiếng Anh. Xem [docs/00-overview/product.md](docs/00-overview/product.md).

## Đọc gì trước khi làm

Đọc [docs/README.md](docs/README.md) (bản đồ theo việc). Sửa module nào thì đọc docs module đó trong `docs/04-domain/modules/` và [ma trận quyền](docs/04-domain/roles-and-permissions.md); đụng giao diện thì đọc `docs/03-ui-ux/`.

## Lệnh

```bash
dotnet build Pancras.slnx --warnaserror            # không được có cảnh báo
dotnet test Pancras.slnx --warnaserror --nologo    # một đường dẫn dự án mỗi lần nếu chạy riêng
dotnet run --project src/Pancras.Web --launch-profile http   # http://localhost:5010
dotnet ef migrations add <Name> --project src/Pancras.Infrastructure --startup-project src/Pancras.Infrastructure
```

- Chuỗi kết nối ở user-secrets (id `pancras-web-7c1a9e2b`), chỉ đọc khi `ASPNETCORE_ENVIRONMENT=Development`: dùng profile `http`.
- Dừng `Pancras.Web.exe` (cổng 5010) trước khi build lại. Đổi class Tailwind thì `npm run build:css` trong `src/Pancras.Web` và commit `site.css`.
- Chi tiết: [build-and-git.md](docs/02-coding-rules/build-and-git.md), [local-setup.md](docs/06-operations/local-setup.md).

## Luật cứng

- **Không bịa luật nghiệp vụ.** Mục "Chờ chốt" trong docs là việc của chủ dự án: hỏi, đừng đoán. Quyết định chốt thì ghi vào docs ([quy trình](docs/05-process/new-module-checklist.md), [quy tắc cho AI](docs/05-process/working-with-ai.md)).
- **Thứ tự làm:** docs → viết test (đỏ đúng lý do) → implement → chạy test, báo cáo trong chat → sửa lỗi tối đa 3 lần rồi dừng hỏi chủ dự án ([checklist](docs/05-process/new-module-checklist.md)).
- Phân tầng: Domain ← Application ← Infrastructure ← Web; Web không gọi Infrastructure ngoài `Program.cs`. Lỗi nghiệp vụ trả bằng `Result`, không exception ([layering](docs/02-coding-rules/layering-and-dependencies.md), [use case](docs/02-coding-rules/use-cases-and-results.md)).
- Quyền kiểm ở cả UI (`CanX`) và handler; dữ liệu nhạy cảm không đưa vào HTML khi thiếu quyền.
- Không để mật khẩu, chuỗi kết nối, token trong mã nguồn, docs, test hay lệnh. Không seed tài khoản.
- Tiền hiển thị `VNĐ`/`USD`; nút nhận hàng mua giữ nhãn `Nhập Kho & Cập Nhật Giá Vốn` ([formatting-and-copy.md](docs/03-ui-ux/formatting-and-copy.md)).
- Áp migration lên DB local ngay sau khi tạo (nêu rõ nếu xóa cột/bảng). Hỏi trước khi xóa dữ liệu, SQL trực tiếp, đẩy remote hay thêm thư viện.
- Xong khi build/test sạch và theo [Definition of Done](docs/05-process/definition-of-done.md).
