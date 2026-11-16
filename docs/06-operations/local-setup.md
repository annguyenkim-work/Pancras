# Cài đặt môi trường dev

> Trạng thái: **Đã chốt** (phiên 2026-11-16).

**Mục đích:** Chạy được dự án từ một máy mới. Lệnh build/test chi tiết ở [build-and-git.md](../02-coding-rules/build-and-git.md).

## 1. Cần cài

| Thứ | Phiên bản | Dùng để |
| --- | --- | --- |
| .NET SDK | 10 (ghim bằng `global.json` khi có) | Build, chạy, test |
| PostgreSQL | bản mới ổn định (cùng dòng với production) | Cơ sở dữ liệu |
| Node.js + npm | LTS | Chỉ để build Tailwind (`npm run build:css`) |
| `dotnet-ef` | cùng phiên bản EF Core (10.0.x) | Tạo và áp migration |
| Git | | |

## 2. Cơ sở dữ liệu và chuỗi kết nối

1. Tạo một database PostgreSQL local (ví dụ `pancras`) và một tài khoản có quyền trên đó.
2. **Chuỗi kết nối chỉ ở user-secrets**, không ghi vào file nào trong repo. Id user-secrets: `pancras-web-7c1a9e2b`.

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<chuỗi-kết-nối-của-bạn>" --project src/Pancras.Web
```

3. Ứng dụng **chỉ đọc user-secrets khi `ASPNETCORE_ENVIRONMENT=Development`** và **ném lỗi khi thiếu chuỗi kết nối**. Vì vậy phải chạy bằng launch profile `http` (đặt sẵn môi trường Development), không dùng `--no-launch-profile`.

## 3. Migration và dữ liệu đầu

```bash
dotnet ef database update --project src/Pancras.Infrastructure --startup-project src/Pancras.Infrastructure
```

- Dùng project **Infrastructure** làm cả project và startup project (khởi động Web sẽ treo công cụ vì `app.Run()`); `PancrasDbContextFactory` cung cấp context lúc thiết kế.
- Sau khi thêm migration mới, **áp ngay lên DB local**.
- **Không có tài khoản mẫu.** Tạo Admin đầu tiên bằng `create-admin` với các biến môi trường `PANCRAS_ADMIN_NAME`, `PANCRAS_ADMIN_EMAIL`, `PANCRAS_ADMIN_PHONE`, `PANCRAS_ADMIN_PASSWORD` (đặt mật khẩu trong phiên shell, không ghi vào file):

```bash
dotnet run --project src/Pancras.Web --launch-profile http -- create-admin
```

## 4. Chạy và kiểm tra

```bash
dotnet run --project src/Pancras.Web --launch-profile http     # http://localhost:5010
dotnet build Pancras.slnx --warnaserror
dotnet test Pancras.slnx --warnaserror --nologo                 # một dự án test mỗi lần gọi khi chọn đường dẫn
```

- **Dừng `Pancras.Web.exe` (cổng 5010) trước khi build lại**; ứng dụng đang chạy khóa tệp build.
- Sửa class giao diện thì chạy `npm run build:css` trong `src/Pancras.Web` (hoặc `npm run watch:css` khi đang làm), rồi commit `site.css`.
- Ảnh sản phẩm tải lên lưu ở `src/Pancras.Web/wwwroot/uploads/` (không vào git). Chứng từ đính kèm lưu ở thư mục cấu hình `Storage:AttachmentsPath` (không cấu hình thì dùng đường dẫn mặc định); `App_Data/` cũng không vào git.

## 5. Dữ liệu thử

- Test thủ công dùng dữ liệu có tiền tố `ZZ Test`/`ZZT-`, seed bằng chương trình tạm ngoài repo; mật khẩu test ở user-secrets `ManualTest:Password` ([manual-testing-and-review.md](../05-process/manual-testing-and-review.md)).
- DB local có thể giữ dữ liệu test (không bắt buộc dọn); sao lưu trước khi seed lớn (`App_Data/backups/`).

## Chờ chốt (chưa có hiệu lực)

- **Đã chốt 2026-11-16:** chưa cần Docker Compose cho PostgreSQL và chưa cần tập lệnh khởi tạo một lần; giữ cách cài trực tiếp theo hướng dẫn.
- Kiểm tra đường dẫn mặc định của `Storage:AttachmentsPath` khi thiếu cấu hình và ghi vào đây.
