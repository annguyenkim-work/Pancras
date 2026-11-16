# Build, lệnh và Git

> Trạng thái: **Đã chốt** (phiên 2026-11-16).

**Mục đích:** Lệnh chuẩn để build/test/chạy, quy tắc về cảnh báo và CSS, quy ước commit và nhánh.

## 1. Lệnh chuẩn

Tệp solution là `Pancras.slnx`.

```bash
dotnet build Pancras.slnx --warnaserror            # không được có cảnh báo
dotnet test Pancras.slnx --warnaserror --nologo    # toàn bộ test
dotnet test tests/Pancras.Domain.Tests --filter "FullyQualifiedName~SomeTest"   # một test/lớp
dotnet run --project src/Pancras.Web --launch-profile http                      # http://localhost:5010
```

- Mỗi lần gọi `dotnet test` chỉ truyền **một** đường dẫn dự án (hai csproj cùng lúc lỗi MSB1008).
- Ứng dụng chạy ở `http://localhost:5010`. Phải dùng profile `http` (không `--no-launch-profile`): chuỗi kết nối chỉ được đọc từ user-secrets khi `ASPNETCORE_ENVIRONMENT=Development`.
- `Pancras.Web.exe` đang chạy khóa tệp build; **dừng tiến trình ở cổng 5010 trước khi build lại**.
- Migration EF: xem [data-access.md](data-access.md) mục 6.

## 2. Cảnh báo và cấu hình dự án

- **Không được để cảnh báo biên dịch** (`Nullable` bật).
- Quy tắc ép trong cấu hình, không chỉ nhờ tham số lệnh: `TreatWarningsAsErrors` trong `Directory.Build.props`, `global.json` ghim phiên bản SDK, `.editorconfig` cho kiểu code (thụt lề, charset, using); đã áp dụng.

## 3. CSS (Tailwind)

- Tailwind 3.4 chạy bằng npm trong `src/Pancras.Web`:

```bash
npm run build:css     # build một lần (minify) -> wwwroot/css/site.css
npm run watch:css     # tự build khi đổi class
```

- **`wwwroot/css/site.css` được commit.** Khi đổi/thêm class Tailwind trong `.cshtml` hoặc `.cs`, **luôn chạy `npm run build:css` trước khi commit**, để VPS không cần Node.
- Tailwind quét `Pages/**/*.{cshtml,cs}`, `Formatting`, `Catalog`, `Security`, `wwwroot/js`; class tạo bằng chuỗi nối động sẽ không được phát hiện, hãy viết nguyên chuỗi class.

## 4. Git

- **Commit:** câu mệnh lệnh tiếng Anh, tiêu đề **≤ 72 ký tự**, thân (nếu có) giải thích *vì sao*. Ví dụ: `Add concurrency token to catalog items`.
- Mỗi commit là một thay đổi có ý nghĩa; build và test phải xanh tại commit đó.
- **Nhánh:** `dev/<tên>/<loại>/<chủ đề>`, loại là `feature`, `fix`, `docs`, `chore`; **không dùng ký tự `&`** hay khoảng trắng trong tên nhánh. Nhánh chính là `main`.
- **Không commit:** secret, mật khẩu, chuỗi kết nối, `App_Data/`, `wwwroot/uploads/`, `bin/`, `obj/`, `node_modules/`, `.vs/`.
- Không viết lại lịch sử đã đẩy lên remote.

## 5. Cổng chất lượng trước khi commit/merge

Bắt buộc chạy sạch **cục bộ** trước khi commit:

```bash
dotnet build Pancras.slnx --warnaserror
dotnet test Pancras.slnx --warnaserror --nologo
```

Ghi chú: đổi class UI thì thêm `npm run build:css`. Chưa có CI; dự định thêm GitHub Actions (build + test `--warnaserror`) về sau.

## Nợ lệch so với quy tắc

| Vấn đề | Hiện trạng |
| --- | --- |
| Tên nhánh | Nhánh hiện tại `dev/annk/feature/Sprint1/Login&Warehouse` có ký tự `&` và cấp con `Sprint1`; đổi tên khi tiện |
| CI | Chưa có |

## Chờ chốt (chưa có hiệu lực)

- **Đã chốt 2026-11-16:** chưa cần quy tắc PR bắt buộc hay bảo vệ nhánh `main`; build và test sạch là đủ.
- Quy ước phiên bản và thẻ phát hành (tag) khi deploy.
- **Đã chốt 2026-11-16:** chưa thêm analyzer bổ sung.
