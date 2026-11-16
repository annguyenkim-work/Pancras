# Module: Nhà cung cấp (`/suppliers`)

> Trạng thái: **Đã chốt** (phiên 2026-11-16). Quyền theo vai: [../roles-and-permissions.md](../roles-and-permissions.md) mục 4. Đơn mua dùng nhà cung cấp: [purchasing.md](purchasing.md).

**Mục đích:** Danh bạ nhà cung cấp nhập khẩu và luật liên quan: hồ sơ, Disable, tài khoản ngân hàng, liên kết với đơn mua. Code: `Domain/Suppliers`, `Application/Suppliers`, `Infrastructure/Persistence/SupplierRepository.cs`, `Web/Pages/Suppliers`.

## 1. Hồ sơ nhà cung cấp (`Supplier`)

| Trường | Bắt buộc | Quy tắc |
| --- | --- | --- |
| Mã (`Code`) | Có | Tối đa 32 ký tự; chuẩn hóa chữ hoa để so trùng (`NormalizedCode`); **duy nhất trong các NCC Active** |
| Tên | Có | Tối đa 200 |
| Quốc gia | Có | Chọn từ danh sách; giá trị cũ ngoài danh sách vẫn giữ được khi sửa |
| Người liên hệ | Có | Tối đa 200 |
| Điện thoại | Không | Nếu nhập phải đúng dạng số điện thoại (`SupplierPhone`) |
| Email | Không | Nếu nhập phải đúng dạng địa chỉ (`SupplierEmail`), tối đa 254 |
| Ngân hàng thụ hưởng | Không | Tối đa 200 |
| Số tài khoản / IBAN | Không | Tối đa 64 |
| Mã SWIFT/BIC | Không | 8 hoặc 11 ký tự chữ-số, lưu chữ hoa (`SupplierSwift`) |

- Hồ sơ kế thừa dấu vết bản ghi: ai ghi lần cuối và lúc nào.
- Mã được giữ nguyên khi sửa chính nhà cung cấp đó; sửa thành mã của NCC khác Active thì bị từ chối.

## 2. Disable (thay cho xóa)

- **Không xóa nhà cung cấp.** Dùng **"Disable"** (`IsInactive`): ẩn khỏi danh sách chọn khi lập đơn mua nhưng giữ để xem lịch sử.
- **Không Disable được khi còn đơn mua chưa nhập kho**; nút vô hiệu kèm lý do.
- Hộp thoại xác nhận nêu hậu quả: số đơn lịch sử và ai khôi phục được.
- **Chỉ Admin khôi phục** NCC Disabled.
- Tạo NCC với mã đang thuộc một NCC Disabled bị chặn, kèm hướng dẫn khôi phục NCC cũ.
- Bộ lọc trạng thái: Active (mặc định) / Disabled / Tất cả.

## 3. Liên kết với đơn mua

- **Đơn mua chụp lại mã và tên NCC lúc lập đơn** (và chụp lại khi đổi NCC của đơn). Sửa hồ sơ NCC sau đó không đổi chứng từ cũ.
- Danh sách chọn NCC khi lập đơn chỉ gồm NCC Active, và cũng kiểm quyền.
- Mỗi dòng NCC hiện số đơn mua và số lô chưa nhập kho (không có lô thì ghi "Không có lô chưa nhập kho"); bấm vào chuyển tới `/purchasing?supplier=<id>`.

## 4. Tài khoản ngân hàng

- Vai quản lý (Admin, Giám đốc, Kế toán) thấy tài khoản; **danh sách hiện dạng che** (`••••1234`), **số đầy đủ chỉ trong form sửa**.
- Thủ kho, Trưởng Showroom/Kho xem danh bạ nhưng **không thấy tài khoản ngân hàng**; handler không trả trường này (xem [role-based-ui.md](../../03-ui-ux/role-based-ui.md)).
- Mở form sửa **không ghi nhật ký**. Nhật ký chỉ ghi khi thêm, sửa, Disable, khôi phục; trường đổi từ gì sang gì, riêng tài khoản chỉ ghi dạng che.

## 5. Danh sách và tìm kiếm

- Dạng **bảng**; tìm theo mã, tên, người liên hệ, email, điện thoại, quốc gia (không phân biệt hoa thường); lọc quốc gia và trạng thái (live filter).
- Phân trang theo chuẩn chung: **20 dòng/trang, chọn 20 hoặc 50**; sắp xếp theo cột tên, mã, quốc gia (xem [components.md](../../03-ui-ux/components.md) mục 6).
- Thêm và sửa dùng **một form chung** trong modal `<dialog>`; Disable qua hộp xác nhận chung.

## Nợ lệch so với luật

| Vấn đề | Hiện trạng |
| --- | --- |
| Phân trang, sắp xếp | Đã có 20/50 và sắp xếp theo tên, mã, quốc gia |
| `Version` | Form sửa gửi `Version`, handler từ chối bằng `SupplierError.VersionConflict`; hai lần lưu chồng nhau thật sự hiện trang 409 chung |

## Chờ chốt (chưa có hiệu lực)

- Trường nhập khẩu giai đoạn 2: tiền tệ giao dịch mặc định, Incoterms, điều khoản thanh toán, mã số thuế, địa chỉ. Chưa làm, chờ yêu cầu nghiệp vụ.
- Hiển thị giá trị mua, ngày mua gần nhất trên mỗi NCC.
- Nối công nợ phải trả nhà cung cấp khi có module Công nợ (xem điều kiện Disable khi còn dư nợ).
