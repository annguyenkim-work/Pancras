# Bảo mật và phân quyền trong code

> Trạng thái: **Đã chốt một phần** (phiên 2026-11-16). Ma trận quyền từng vai ở [../04-domain/roles-and-permissions.md](../04-domain/roles-and-permissions.md); phía UI ở [role-based-ui.md](../03-ui-ux/role-based-ui.md).

**Mục đích:** Quy tắc cài đặt xác thực, phân quyền, secret và tệp tải lên.

## 1. Phân quyền ở server

- **Mọi trang cần đăng nhập** (`FallbackPolicy` yêu cầu người dùng đã xác thực). Trang công khai (đăng nhập, từ chối truy cập...) phải đánh dấu `[AllowAnonymous]` có chủ ý.
- **Handler lấy người thao tác (actor) từ phiên đăng nhập** (`EmployeeActor`/`IRequestInfo`), **không bao giờ nhận từ form hoặc query string**.
- **Kiểm `<Module>Access` ở đầu handler**, trước khi đụng dữ liệu. Ẩn nút ở UI không thay thế bước này.
- **Phạm vi kho/showroom:** mọi Id gửi từ form (kho, phiếu, đơn, nhân viên) phải được kiểm là thuộc phạm vi của actor; **không tin Id** do client gửi.
- **Phiên đăng nhập kiểm lại mỗi request** (`EmployeePrincipalValidator`): tài khoản bị khóa, vô hiệu hóa, đổi mật khẩu hoặc đổi vai trò (security stamp thay đổi) thì phiên cũ bị đá ra.
- Thiếu quyền ghi nhật ký (`Bị từ chối do thiếu quyền`), xem audit ở [use-cases-and-results.md](use-cases-and-results.md).

## 2. Mật khẩu và xác thực hai bước

- Mật khẩu 8–128 ký tự (`PasswordPolicy`), băm PBKDF2 (`Pbkdf2PasswordHasher`).
- Khóa tài khoản **15 phút sau 5 lần đăng nhập sai** liên tiếp.
- Xác thực hai bước (TOTP + mã khôi phục) là **tùy chọn**, khuyến khích nhưng chưa bắt buộc với vai nào.
- Admin đặt lại mật khẩu người khác; đổi mật khẩu bắt buộc nhập mật khẩu cũ khi tự đổi.

## 3. Secret và tài khoản

- **Secret chỉ ở user-secrets (dev, id `pancras-web-7c1a9e2b`) và biến môi trường / cấu hình ngoài git (production).** Ứng dụng ném lỗi khi thiếu chuỗi kết nối.
- **Không seed tài khoản** trong code, spec hay file được git theo dõi. Không đưa mật khẩu vào mã nguồn.
- **Admin đầu tiên tạo bằng lệnh `create-admin`** (`dotnet Pancras.Web.dll create-admin`), đọc `PANCRAS_ADMIN_NAME/EMAIL/PHONE/PASSWORD` từ biến môi trường để mật khẩu không nằm trong lịch sử lệnh; chỉ chạy khi chưa có Admin hoạt động.
- Không log mật khẩu, token, mã xác thực, PII (xem `async-logging-errors.md`).

## 4. Tệp tải lên

- Giới hạn dung lượng bằng cách **đọc thật** (đọc tối đa `MaxBytes + 1` byte), không tin kích thước trình duyệt khai báo.
- Làm sạch tên file (`AttachmentRules.CleanFileName`); **tên lưu do hệ thống sinh** (`IsSafeStoredName`), không lấy từ người dùng.
- **Nội dung tệp nằm ngoài database** (`IAttachmentStorage`); DB chỉ giữ thông tin tệp.
- Tải về qua handler có kiểm quyền, không phục vụ thư mục tải lên như tệp tĩnh.
- **Loại tệp cho phép:** chỉ `pdf`, `jpg`/`jpeg`, `png`, `xlsx`, `docx` (kiểm theo đuôi và loại nội dung); đã bỏ `.doc` và `.xls` vì có thể chứa macro.
- **Giới hạn:** tối đa **10 MB mỗi tệp** (`AttachmentRules.MaxBytes`), **30 tệp mỗi đơn** mua; toàn bộ request tối đa **12 MB** (đặt ở Kestrel và `FormOptions` trong `Program.cs`, không dùng `[RequestSizeLimit]` vì antiforgery đã đọc form trước).
- **Chưa quét virus.**
- Xóa chứng từ: lỗi xóa tệp chỉ ghi log (tệp mồ côi), không làm hỏng thao tác.

## 5. CSP

- Phông chữ tự host, nên `style-src` và `font-src` không còn nguồn Google (`fonts.googleapis.com`, `fonts.gstatic.com`).
- Mục tiêu: bỏ `'unsafe-inline'` khỏi `script-src` (script ngoại + nonce). Hiện vẫn còn `'unsafe-inline'` vì còn JS inline (`onclick`, script trong `_ConfirmDialog`).

## Nợ lệch so với quy tắc

| Vấn đề | Hiện trạng |
| --- | --- |
| Rà phạm vi theo kho | Chưa rà từng handler xem đã kiểm Id thuộc phạm vi actor chưa (kho, phiếu, đơn); cần danh sách kiểm tra và test cho từng module |
| Tải tệp | Giới hạn 12 MB request chưa thử bằng tệp lớn thật; tải về: kiểm lại header `Content-Disposition`; tệp mồ côi khi xóa chứng từ lỗi chưa có việc dọn định kỳ |
| Giới hạn tốc độ | Có policy `login` (120 lượt/phút/IP) và `sensitive` (30 lượt/phút/người dùng, cấu hình `RateLimiting:SensitivePerMinute`) cho trang hồ sơ (đổi mật khẩu, 2FA). Policy `sensitive-post` (cùng hạn mức) áp cho mọi yêu cầu POST ở trang Nhân viên (tạo, sửa, đặt lại mật khẩu, Disable/Active, mở khóa, reset 2FA); xem danh sách không bị giới hạn |

## Quyết định đã chốt (2026-11-16)

- 2FA vẫn tùy chọn, mật khẩu tối thiểu 8 ký tự (không đổi). Ghi lại để không hỏi lại.
- [Đề xuất của AI, chủ dự án đã duyệt] Các cấu hình đang bật trở thành quy tắc chính thức, không được tắt khi chưa hỏi chủ dự án: bí mật 2FA mã hóa bằng Data Protection, header bảo mật (nosniff, X-Frame-Options DENY, Referrer-Policy, Permissions-Policy), cookie `HttpOnly`/`SameSite=Lax`/`Secure` ở production, phiên 8 giờ trượt.

## Chờ chốt (chưa có hiệu lực)

Không còn mục chờ chốt.

