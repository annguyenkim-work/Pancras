# Module: Tài khoản và nhân sự (`/login`, `/profile`, `/employees`)

> Trạng thái: **Đã chốt** (phiên 2026-11-16). Quyền theo vai: [../roles-and-permissions.md](../roles-and-permissions.md) mục 2. Bảo mật cài đặt: [security-and-authorization.md](../../02-coding-rules/security-and-authorization.md).

**Mục đích:** Hồ sơ nhân viên, mã nhân viên, đăng nhập, khóa tài khoản, xác thực hai bước, đổi mật khẩu, vô hiệu hóa. Code: `Domain/Employees`, `Application/Employees`, `Infrastructure/Persistence/EmployeeRepository.cs`, `Infrastructure/Identity`, `Web/Pages/Account`, `Web/Pages/Employees`, `Web/Security`.

## 1. Hồ sơ nhân viên (`Employee`)

| Trường | Quy tắc |
| --- | --- |
| Họ tên | Bắt buộc, tối đa 200 |
| Mã nhân viên | Server sinh, **không sửa**; xem mục 2 |
| Vai trò (`Role`) | Một trong 7 vai ([roles-and-permissions.md](../roles-and-permissions.md) mục 1) |
| Email | Bắt buộc khi tạo, đúng dạng địa chỉ |
| Điện thoại | Bắt buộc khi tạo, tối đa 30 |
| Chức vụ (`JobTitle`) | Bắt buộc khi tạo, tối đa 100 |
| Phòng ban (`Department`) | Tùy chọn, tối đa 100 |
| Địa chỉ | Tùy chọn, tối đa 300 |
| Kho/showroom (`StockLocationId`) | **Bắt buộc** với Trưởng Showroom/Kho, Thủ kho, Sales; mỗi kho tối đa **1 Trưởng đang hoạt động** |
| Trạng thái | `IsActive`; vô hiệu hóa thay cho xóa |
| Mật khẩu | 8–128 ký tự, lưu băm PBKDF2, không bao giờ hiển thị |

- **Chức vụ và phòng ban là văn bản tự do, chỉ để hiển thị; quyền dựa vào Vai trò**, không dựa vào chức vụ hay phòng ban.
- Hồ sơ kế thừa dấu vết bản ghi.
- `SecurityStamp` đổi mỗi khi đổi mật khẩu, vai trò/kho, vô hiệu hóa hay reset 2FA (mục 5).

## 2. Mã nhân viên

- **Server sinh từ tên**, hiện trước khi lưu, ô mã bị khóa: các từ đứng trước tên cuối lấy chữ cái đầu viết hoa, **tên cuối ghi trước**. Ví dụ `Nguyễn Kim An` → `AnNK`. Tên một từ thì lấy nguyên từ.
- Trùng thì thêm số `1`…`9999` vào cuối. **Không được là `ADMIN`.**
- **Mã không đổi.** Nhân viên bị vô hiệu vẫn giữ mã, **mã không được dùng lại**.
- Đăng nhập không phân biệt hoa thường của mã (`NormalizedCode`).

## 3. Đăng nhập, khóa, 2FA

- Đăng nhập bằng **mã nhân viên + mật khẩu**. Lỗi luôn chung chung ("sai thông tin"); **thời gian phản hồi như nhau** dù tài khoản không tồn tại, bị khóa hay vô hiệu (luôn băm một lần).
- **Sai 5 lần khóa 15 phút**; đăng nhập đúng thì xóa bộ đếm. Admin mở khóa được. Có giới hạn tốc độ theo IP để chặn dò mật khẩu.
- **2FA (TOTP) là tùy chọn cho mọi vai**, tự bật/tắt ở `/profile`: mã 6 chữ số, chu kỳ 30 giây, mỗi bước thời gian chỉ dùng một lần (`LastTotpStep`). Có **10 mã khôi phục** dùng một lần, lưu dạng băm SHA-256, chỉ hiện đúng một lần lúc bật. Khóa TOTP mã hóa bằng Data Protection, không lưu thô.
- **Nhập sai mã 2FA tính vào bộ đếm khóa.** Mất điện thoại thì Admin reset 2FA.
- Sau mật khẩu đúng, tài khoản có 2FA nhận một phiên chờ 5 phút (cookie riêng) cho tới khi nhập đúng mã.

## 4. Quản lý nhân viên

- **Tạo:** Admin (mọi vai), Giám đốc (trừ Admin), Trưởng kho (Thủ kho/Sales vào kho mình). Vai cần kho thì phải gán kho đã tồn tại.
- **Sửa thông tin cá nhân người khác:** Admin. **Sửa vai trò, chức vụ, phòng ban, kho:** Admin; Giám đốc (trừ vai Admin); không ai tự đổi vai trò/chức vụ/kho của mình.
- **Đặt lại mật khẩu:** Admin; không bắt buộc đổi ở lần đăng nhập đầu. Người dùng tự đổi mật khẩu phải nhập mật khẩu cũ.
- **Vô hiệu hóa / kích hoạt lại:** Admin; Giám đốc (trừ Admin); không ai tự vô hiệu mình. **Luôn còn ít nhất 1 Admin hoạt động.**
- **Vô hiệu hóa:** không đăng nhập được; **lịch sử (phiếu, đơn, nhật ký) giữ nguyên tên và mã**. Vô hiệu Trưởng kho thì kho tạm không có Trưởng.
- **Admin đầu tiên** tạo bằng lệnh `create-admin` (xem security-and-authorization.md); không có tài khoản mẫu trong mã nguồn.

## 5. Phiên đăng nhập

- Cookie `HttpOnly`, `SameSite=Lax`, `Secure` ở production, phiên 8 giờ trượt.
- `EmployeePrincipalValidator` kiểm lại **mỗi request**: tài khoản còn hoạt động và `SecurityStamp` khớp. **Đổi mật khẩu, đổi vai trò/kho, vô hiệu hóa, reset 2FA đều làm phiên cũ hết hiệu lực ngay.**

## 6. Danh sách và hồ sơ cá nhân

- **Danh sách nhân viên** dạng **bảng**: STT, mã, tên, email, điện thoại, chi nhánh, chức vụ, trạng thái; lọc tên/mã/vai trò/kho (live filter); phân trang chuẩn 20/50; hành động ở cột thứ 2 ([components.md](../../03-ui-ux/components.md)). Trưởng kho chỉ thấy nhân viên kho mình.
- Thêm/sửa nhân viên mở **modal**; mã hiện trước khi lưu.
- **`/profile`:** mọi vai tự sửa tên, email, điện thoại, địa chỉ, đổi mật khẩu, bật/tắt 2FA.

## 7. Nhật ký

Ghi `IAuditLog` (mã hành động dạng `nhóm.sự_kiện`, hiển thị tiếng Việt ở UI): đăng nhập thành công/thất bại, khóa tài khoản, 2FA bật/tắt/reset/sai/dùng mã khôi phục, đổi mật khẩu, đặt lại mật khẩu, tạo/sửa/đổi vai trò/kích hoạt/vô hiệu hóa/mở khóa nhân viên, bị từ chối do thiếu quyền. **Không ghi mật khẩu, mã, token.** Xem [notifications-and-audit.md](notifications-and-audit.md).

## Nợ lệch so với luật

| Vấn đề | Hiện trạng |
| --- | --- |
| `Version` | Form sửa gửi `Version`, handler từ chối bằng `EmployeeError.VersionConflict`; hai lần lưu chồng nhau thật sự hiện trang 409 chung |

## Chờ chốt (chưa có hiệu lực)

- **Đã chốt 2026-11-16 (chưa làm):** thêm quên mật khẩu qua email (gửi link đặt lại có hạn dùng). Đảo ngược ADR-12 và dòng "Không có quên mật khẩu qua email" ở roles-and-permissions.md; hiện vẫn Admin đặt lại. Dịch vụ gửi: SMTP của VinaHost qua thư viện có sẵn của .NET (không MailKit); thông tin SMTP ở user-secrets/biến môi trường, không ghi vào mã nguồn. Còn chờ chủ dự án tạo hộp thư gửi đi và cấu hình; làm kèm test và rate limit.
- **Đã chốt 2026-11-16:** chức vụ và phòng ban giữ **văn bản tự do** (không danh sách cố định).
- **Chốt kỹ thuật 2026-11-16 (đề xuất của AI theo thực tế code, chủ dự án có thể đổi):** không có tab ma trận quyền trên màn nhân sự; quyền chỉ ghi ở roles-and-permissions.md.
