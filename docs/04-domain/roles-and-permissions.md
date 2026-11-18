# Vai trò và phân quyền

> Trạng thái: **Đã chốt** (phiên 2026-11-16). **Nguồn sự thật duy nhất cho quyền.** Code (`<Module>Access`), test và spec sau này phải khớp file này. Cách cài đặt: [../02-coding-rules/security-and-authorization.md](../02-coding-rules/security-and-authorization.md); cách thể hiện trên UI: [../03-ui-ux/role-based-ui.md](../03-ui-ux/role-based-ui.md).

## 1. Vai trò và phạm vi dữ liệu

| Vai trò | Hằng số code | Phạm vi dữ liệu |
| --- | --- | --- |
| Admin | `EmployeeRole.Admin` | Toàn hệ thống |
| Giám đốc | `Director` | Toàn hệ thống |
| Kế toán | `Accountant` | Toàn hệ thống |
| Trưởng Showroom/Kho | `ShowroomWarehouseLead` | Đúng 1 kho/showroom được gán; mỗi kho tối đa 1 Trưởng |
| Thủ kho | `WarehouseKeeper` | Đúng 1 kho/showroom được gán |
| Sales | `Sales` | Đúng 1 kho/showroom được gán |
| CSKH | `CustomerCare` | Chưa có module, chỉ có hồ sơ của mình |

- Trưởng Showroom/Kho, Thủ kho, Sales **bắt buộc** có kho/showroom. Admin, Giám đốc, Kế toán không cần gán.
- Gọi chung "**tài chính**" là ba vai Admin, Giám đốc, Kế toán (`InventoryRoles.IsFinance`).

## 2. Nhân viên (`/employees`)

| Hành động | Admin | Giám đốc | Kế toán | Trưởng kho | Khác |
| --- | --- | --- | --- | --- | --- |
| Xem danh sách và hồ sơ (không thấy mật khẩu) | Tất cả | Tất cả | Tất cả | Chỉ nhân viên kho mình | Không |
| Tạo nhân viên | Mọi vai trò | Mọi vai trò trừ Admin | Không | Thủ kho, Sales, chỉ vào kho mình | Không |
| Sửa thông tin cá nhân người khác (tên, email, điện thoại, địa chỉ) | Có | Không | Không | Không | Không |
| Sửa vai trò, chức vụ, phòng ban, kho/showroom | Có | Có, trừ vai trò Admin | Không | Không | Không |
| Đặt lại mật khẩu người khác | Có | Không | Không | Không | Không |
| Vô hiệu hóa / kích hoạt lại tài khoản | Có | Có, trừ Admin | Không | Không | Không |
| Tự sửa hồ sơ của mình (tên, email, điện thoại, địa chỉ; đổi mật khẩu bằng mật khẩu cũ) | Có | Có | Có | Có | Có |

Quy tắc cứng:
- Chỉ Admin cấp, đổi hoặc gỡ vai trò Admin. Giám đốc không sửa được tài khoản Admin.
- Luôn còn ít nhất 1 Admin đang hoạt động; không cho hạ hoặc vô hiệu hóa Admin cuối.
- Không ai tự đổi vai trò, chức vụ, kho, và không ai tự vô hiệu hóa chính mình.
- Không xóa nhân viên, chỉ vô hiệu hóa để giữ lịch sử.
- Sau khi Admin đặt lại mật khẩu, **không bắt buộc** đổi ở lần đăng nhập đầu.
- Không có quên mật khẩu qua email; Admin đặt lại. Mất điện thoại 2FA thì Admin reset 2FA.
- Mã nhân viên do server sinh từ tên (ví dụ `AnNK`, thêm số khi trùng).

## 3. Kho (`/inventory`)

Phạm vi:
- **Tra cứu tồn:** mọi vai xem kho (Admin, Giám đốc, Kế toán, Trưởng kho, Thủ kho, Sales) thấy **số lượng ở mọi kho và showroom**.
- **Giá vốn:** chỉ Admin, Giám đốc, Kế toán thấy; Trưởng kho, Thủ kho, Sales không.
- **Phiếu chuyển, kiểm kê, điều chỉnh, hủy:** tài chính thấy mọi kho; Trưởng kho và Thủ kho chỉ thấy và thao tác phiếu thuộc kho mình; Sales không vào các tab này.

| Hành động | Admin / Giám đốc / Kế toán | Trưởng kho | Thủ kho | Sales |
| --- | --- | --- | --- | --- |
| Tra cứu tồn (mọi kho) | Có | Có | Có | Có (chỉ số lượng) |
| Thấy giá vốn | Có | Không | Không | Không |
| Xuất CSV tồn kho | Có | Kho mình | Không | Không |
| Lập phiếu đề nghị chuyển kho | Có | Kho xuất hoặc kho nhận là kho mình | Như Trưởng kho | Không |
| Kiểm kê | Ghi thẳng | Lập, chờ duyệt (kho mình) | Lập, chờ duyệt (kho mình) | Không |
| Lập phiếu điều chỉnh | Ghi thẳng | Lập, chờ duyệt (kho mình) | Lập, chờ duyệt (kho mình) | Không |
| Lập phiếu hủy | Ghi thẳng | Lập, chờ duyệt (kho mình) | Lập, chờ duyệt (kho mình) | Không |

Ai duyệt phiếu chờ duyệt:

| Phiếu | Người duyệt |
| --- | --- |
| Kiểm kê, hủy của **Thủ kho** | Trưởng kho của kho đó; hoặc Admin, Giám đốc, Kế toán |
| Kiểm kê, hủy của **Trưởng kho** | Admin, Giám đốc, Kế toán |
| Điều chỉnh số lượng | Admin, Giám đốc, Kế toán |

### Phiếu chuyển kho

Phiếu đề nghị: kho xuất và nơi nhận chọn tùy ý, miễn **kho xuất hoặc nơi nhận là kho/showroom của mình**. Luồng: lập phiếu, duyệt, xuất kho, nhận hàng, xử lý chênh lệch (nếu có).

| Bước | Ai làm được |
| --- | --- |
| Sửa phiếu (khi chưa duyệt) | Chỉ người lập phiếu |
| Hủy phiếu (chưa xuất kho, bắt buộc lý do) | Chỉ người lập phiếu |
| Duyệt hoặc từ chối (từ chối bắt buộc lý do) | Admin, Giám đốc, Kế toán, Trưởng kho của kho xuất |
| Xuất kho | Admin, Giám đốc, Kế toán, Trưởng kho và Thủ kho của kho xuất |
| Nhận hàng (nhập số thực nhận từng dòng) | Admin, Giám đốc, Kế toán, Trưởng kho và Thủ kho của nơi nhận |
| Xử lý chênh lệch khi nhận | Admin, Giám đốc, Kế toán |
| Trả hàng đang vận chuyển về kho xuất (bắt buộc lý do) | Admin, Giám đốc, Kế toán |
| In phiếu xuất kho (chỉ khi phiếu từ `Approved` trở đi, không in `Requested`, `Rejected`, `Cancelled`) | Admin, Giám đốc, Kế toán: mọi phiếu; Trưởng kho và Thủ kho: chỉ khi thuộc kho xuất của phiếu. Kho nhận xem được nhưng không in |

Quy tắc luồng (luật nghiệp vụ chi tiết ở [modules/inventory.md](modules/inventory.md)):
- Không áp dụng tách nhiệm vụ: một người có thể lập, duyệt, xuất và nhận cùng một phiếu nếu có quyền ở mỗi bước (Product Owner đã chấp nhận). Bù lại mọi bước đều ghi lịch sử.
- Thông báo gửi tới ai ở từng bước: xem `modules/notifications-and-audit.md`.

## 4. Các module khác

| Module | Quyền |
| --- | --- |
| Nhà cung cấp (`/suppliers`) | Admin, Giám đốc, Kế toán: xem, tạo, sửa, Disable (thay cho xóa mềm), thấy tài khoản ngân hàng (che 4 số cuối ở danh sách, đầy đủ trong form sửa). Chỉ Admin khôi phục nhà cung cấp Disabled. Thủ kho, Trưởng Showroom/Kho: chỉ xem danh bạ, không thấy ngân hàng, không sửa. Vai khác: không vào. Không Disable được khi còn đơn mua chưa nhập kho |
| Mua hàng và container (`/purchasing`) | Quản lý (tạo, sửa, đổi trạng thái, hủy, điều chỉnh chi phí, chứng từ, xem FOB/thuế/giá vốn): Admin, Giám đốc, Kế toán. Thủ kho, Trưởng Showroom/Kho: vào trang để xem **đơn đã duyệt trở đi của kho mình** và nhận hàng, không thấy giá |
| Danh mục sản phẩm và giá (`/catalog`) | Admin, Giám đốc, Kế toán: xem, thêm/sửa mặt hàng, sửa giá, Disable. Trưởng Showroom/Kho không sửa giá. Cấu hình % chiết khấu theo mức giá (chưa làm) cùng nhóm với sửa giá. Sales và CSKH chưa vào được; quyền Sales xem giá bán sẽ định nghĩa khi làm module Bán hàng/Báo giá |
| Kho & Showroom (`/locations`) | Xem: Admin, Giám đốc, Kế toán. Quản lý: Admin, Giám đốc |
| Nhật ký hệ thống (`/audit`) | Admin, Giám đốc |
| Bán hàng (`/sales`) | "Sắp có". **Đã chốt một phần:** Kế toán, Giám đốc, Admin được sửa đơn bán **trước khi xuất chứng từ**. Các quyền bán hàng còn lại **chưa chốt**, định nghĩa khi làm module (không đoán) |
| Khách hàng, báo giá, combo, công nợ, bảo hành | "Sắp có"; định nghĩa quyền khi làm module |

## 5. Bảo mật tài khoản

Chi tiết cài đặt ở [security-and-authorization.md](../02-coding-rules/security-and-authorization.md). Tóm tắt chính sách:
- Mật khẩu 8–128 ký tự; sai 5 lần khóa 15 phút; Admin mở khóa được.
- 2FA (TOTP, 10 mã khôi phục) là **tùy chọn cho mọi vai**, tự bật/tắt ở `/profile`.
- Ghi nhật ký: đăng nhập, khóa tài khoản, đổi vai trò, đặt lại mật khẩu, duyệt phiếu, đổi giá.

## Đối chiếu code (2026-11-16)

Khớp với `EmployeeAccess`, `SupplierAccess`, `LocationAccess`, `CatalogAccess`, `PurchaseAccess`, `InventoryAccess`. Chưa khớp hoặc chưa kiểm:

| Mục | Hiện trạng |
| --- | --- |
| Hồ sơ nhân viên tại kho | Việc Trưởng kho chỉ tạo Thủ kho/Sales vào kho mình đã có trong `EmployeeAccess`; cần test cho ranh giới |
| Xuất CSV tồn kho | `CanExport` đang cho tài chính và Trưởng kho (kho mình), khớp bảng; Thủ kho không |

## Chờ chốt (chưa có hiệu lực)

- Quyền của module Khách hàng, Bán hàng, Báo giá, Combo, Công nợ, Bảo hành, CSKH: định nghĩa khi làm từng module.
