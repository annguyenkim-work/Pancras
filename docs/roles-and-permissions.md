# Vai trò và phân quyền (bản nháp để duyệt)

Nguồn sự thật duy nhất cho quyền. Code, test và các spec sau này phải khớp file này. Mục đánh dấu **[Cần xác nhận]** là đề xuất của Claude, chưa được chốt.

## 1. Vai trò

| Vai trò | Phạm vi dữ liệu |
| --- | --- |
| Admin | Toàn hệ thống |
| Giám đốc | Toàn hệ thống |
| Kế toán | Toàn hệ thống |
| Trưởng Showroom/Kho | Đúng 1 kho/showroom được gán. Mỗi kho có tối đa 1 Trưởng |
| Thủ kho | Đúng 1 kho/showroom được gán |
| Sales | Đúng 1 kho/showroom được gán |
| CSKH | Chưa có module, chỉ có profile của mình |

Mỗi nhân viên thuộc 1 kho/showroom (Admin, Giám đốc, Kế toán không cần gán).

## 2. Nhân viên

| Hành động | Admin | Giám đốc | Kế toán | Trưởng kho | Khác |
| --- | --- | --- | --- | --- | --- |
| Xem danh sách và hồ sơ (không thấy mật khẩu) | Tất cả | Tất cả | Tất cả | Chỉ nhân viên kho mình | Không |
| Tạo nhân viên | Mọi vai trò | Mọi vai trò trừ Admin | Không | Thủ kho, Sales, chỉ vào kho mình | Không |
| Sửa thông tin cá nhân của người khác (tên, email, điện thoại, địa chỉ) | Có | Không | Không | Không | Không |
| Sửa vai trò, chức vụ, kho/showroom | Có | Có, trừ vai trò Admin | Không | Không | Không |
| Đặt lại mật khẩu người khác | Có | Không | Không | Không | Không |
| Vô hiệu hóa tài khoản **[Cần xác nhận]** | Có | Có, trừ Admin | Không | Không | Không |
| Tự sửa profile của mình (tên, email, điện thoại, địa chỉ, đổi mật khẩu bằng mật khẩu cũ) | Có | Có | Có | Có | Có |

Quy tắc cứng:
- Chỉ Admin cấp, đổi hoặc gỡ vai trò Admin. Giám đốc không sửa được tài khoản Admin.
- Luôn còn ít nhất 1 Admin đang hoạt động. Không cho hạ, vô hiệu hóa hoặc xóa Admin cuối.
- Không ai tự đổi vai trò, chức vụ, kho của chính mình.
- Không xóa nhân viên, chỉ vô hiệu hóa để giữ lịch sử.

## 3. Kho (`/inventory`)

Phạm vi: Admin, Giám đốc, Kế toán thấy mọi kho. Trưởng kho, Thủ kho, Sales chỉ thấy kho của mình.

| Hành động | Admin | Giám đốc | Kế toán | Trưởng kho | Thủ kho | Sales |
| --- | --- | --- | --- | --- | --- | --- |
| Tra cứu tồn | Có | Có | Có | Có | Có | Có (chỉ tra cứu) |
| Thấy giá vốn **[Cần xác nhận]** | Có | Có | Có | Không | Không | Không |
| Xuất Excel tồn kho | Có | Có | Có | Kho mình | Không | Không |
| Lập phiếu chuyển kho, xuất kho | Có | Có | Có | Từ kho mình | Từ kho mình | Không |
| Xác nhận nhận hàng chuyển đến | Có | Có | Có | Kho đích là kho mình | Kho đích là kho mình | Không |
| Kiểm kê | Ghi thẳng | Ghi thẳng | Ghi thẳng | Lập, chờ duyệt | Lập, chờ duyệt | Không |
| Lập phiếu điều chỉnh | Ghi thẳng | Ghi thẳng | Ghi thẳng | Lập, chờ duyệt | Lập, chờ duyệt | Không |
| Lập phiếu hủy | Ghi thẳng | Ghi thẳng | Ghi thẳng | Lập, chờ duyệt | Lập, chờ duyệt | Không |

Ai duyệt phiếu chờ duyệt (**[Cần xác nhận]**):
- Người lập không tự duyệt phiếu của mình.
- Phiếu của Thủ kho: Trưởng kho của kho đó duyệt. Giám đốc, Kế toán, Admin cũng duyệt được.
- Phiếu của Trưởng kho: Giám đốc hoặc Kế toán duyệt.
- Phiếu điều chỉnh: Giám đốc, Kế toán, Admin duyệt (như code hiện tại).

## 4. Các module khác (giữ nguyên quyền hiện tại)

| Module | Quyền |
| --- | --- |
| Nhà cung cấp | Admin, Giám đốc, Kế toán: xem, tạo, sửa, xóa mềm. Vai trò khác: không vào |
| Mua hàng và container | Admin, Giám đốc, Kế toán |
| Danh mục sản phẩm và giá | Theo `CatalogAccess.ViewRoles` hiện tại |
| Menu "Sắp có" (khách hàng, bán hàng, báo giá, combo, công nợ, bảo hành, kho & showroom, audit) | Chưa có quyền, định nghĩa khi làm module |

## 5. Bảo mật tài khoản (app public)

- Mật khẩu tối thiểu 8 ký tự.
- Sai 5 lần thì khóa tài khoản 15 phút. Thêm giới hạn theo IP để không bị khóa Admin có chủ đích. Admin mở khóa được.
- Đáp ứng đăng nhập có thời gian như nhau với tài khoản không tồn tại và sai mật khẩu.
- Xác thực hai bước (2FA) là tùy chọn cho mọi vai trò (khách hàng chốt). Mỗi người tự bật hoặc tắt trong trang hồ sơ cá nhân (`/profile`), bằng TOTP (app Google/Microsoft Authenticator, Authy), tự cài trong app, không dùng dịch vụ trả phí. Có 10 mã khôi phục dùng một lần (lưu dạng hash). Mất điện thoại thì Admin reset 2FA. Nhập sai mã 2FA tính vào bộ đếm khóa. Mỗi mã chỉ dùng được một lần.
- Không có quên mật khẩu qua email. Admin đặt lại, người dùng phải đổi ở lần đăng nhập đầu **[Cần xác nhận]**.
- Ghi nhật ký: đăng nhập, khóa tài khoản, đổi vai trò, đặt lại mật khẩu, duyệt phiếu, đổi giá.
- HTTPS bắt buộc, HSTS, cookie `Secure`, security header, bỏ Tailwind CDN.

## 6. Hệ quả cho dữ liệu (để làm sau khi bảng này được duyệt)

- Nhân viên: thêm `StockLocationId` (thay cho chuỗi `Chi nhánh`), `Address`, `IsActive`, `MustChangePassword`.
- Ràng buộc: mỗi kho tối đa 1 Trưởng kho; Trưởng kho, Thủ kho, Sales bắt buộc có kho.
- Đăng nhập: đếm lần sai và thời điểm hết khóa.
- Bảng audit log.
- Tập trung kiểm tra quyền vào một lớp chính sách duy nhất, không rải trong từng handler và page.

## 7. Còn mở

1. Ai duyệt phiếu của Trưởng kho (mục 3).
2. Trưởng kho và Thủ kho có thấy giá vốn không.
3. (Đã chốt: 2FA tùy chọn cho mọi vai trò.)
4. Vô hiệu hóa tài khoản: Giám đốc có làm được không.
