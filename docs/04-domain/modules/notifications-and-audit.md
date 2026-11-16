# Module: Thông báo và nhật ký hệ thống

> Trạng thái: **Đã chốt** (phiên 2026-11-16). Quy tắc ghi nhật ký trong handler: [use-cases-and-results.md](../../02-coding-rules/use-cases-and-results.md). Quyền xem nhật ký: [../roles-and-permissions.md](../roles-and-permissions.md) mục 4.

**Mục đích:** Hai việc khác nhau. **Thông báo** báo người liên quan có việc cần làm (bước chờ, bước tiếp theo). **Nhật ký (audit)** ghi lại ai đã làm gì để truy vết. Code: `Domain/Notifications/Notification.cs`, `Domain/Auditing`, `Application/Auditing`, `Web/Pages/Audit`.

## A. Thông báo

### A1. Thiết kế

- **Thông báo dùng chung mọi module** (`Notification`), không riêng module nào. Có **chuông trên header** hiện số chưa xem và trang **`/notifications`** liệt kê.
- Mỗi thông báo: người nhận, kho/địa điểm liên quan (nếu có), **loại chứng từ + Id chứng từ**, bước, đã xem hay chưa, thời điểm. Bấm vào mở **đúng chứng từ** (đúng tab, đúng phiếu).
- `Notification` dùng chung (đã chuyển từ `StockNotice` của kho); thông báo mua hàng dùng cùng thực thể.
- Tạo thông báo là một phần của giao dịch ghi chứng từ (repository), không phải bước riêng dễ sót.

### A2. Vòng đời

1. **Đánh dấu đã xem khi người nhận mở thông báo.** Mỗi người có trạng thái xem riêng; một người xem không làm người khác thành đã xem.
2. **Khi bước đã xong, thông báo của bước đó biến mất với mọi người nhận**, kể cả người chưa mở.
3. **Không tạo thông báo thứ hai cho cùng một bước đang chờ** (lưu lại phiếu mà bước không đổi thì không báo thêm).
4. Chỉ người nhận mới mở được thông báo của mình.
5. Bước cuối của luồng không báo "cần bước tiếp".

### A3. Ai nhận thông báo gì

**Chuyển kho** (đã có trong code):

| Sự kiện | Người nhận |
| --- | --- |
| Lập hoặc sửa phiếu đề nghị | Admin, Giám đốc, Kế toán, Trưởng kho của kho xuất |
| Mỗi bước sau (duyệt, từ chối, xuất, nhận, xử lý chênh lệch, hủy, trả hàng) | Người lập phiếu (trừ chính người thao tác) |
| Duyệt | Thêm nhân viên kho xuất |
| Xuất kho | Thêm nhân viên nơi nhận |
| Nhận lệch | Thêm Admin, Giám đốc, Kế toán |

**Kho khác** (đã có trong code): lập điều chỉnh chờ duyệt → Admin, Giám đốc, Kế toán; duyệt hủy → nhân viên của đúng kho đó.

**Mua hàng** (đã chốt, đã có trong code, trừ hủy và nhận):

| Sự kiện | Người nhận |
| --- | --- |
| Gửi duyệt đơn | Admin, Giám đốc, Kế toán (trừ người gửi) |
| Đơn được duyệt hoặc trả về nháp (kèm lý do) | Người lập đơn |
| Đơn đã duyệt hoặc đang về | Trưởng Showroom/Kho và Thủ kho của kho nhận (không kèm giá) |

## B. Nhật ký hệ thống (`AuditEntry`)

### B1. Cấu trúc

`Id`, thời điểm, người làm (`ActorEmployeeId`, `ActorCode`), **mã hành động**, loại đối tượng, Id đối tượng, chi tiết (tối đa 500 ký tự), địa chỉ IP. Chuỗi bị cắt khi quá dài (mã hành động 60, loại 60, Id 80).

- **Mã hành động** là hằng số trong `AuditActions`, dạng **`nhóm.sự_kiện`** (ví dụ `login.failed`, `employee.role_changed`, `stock.transfer_approved`); UI đổi sang câu tiếng Việt khi hiển thị.
- **Chi tiết không chứa** mật khẩu, mã xác thực, token, khóa bí mật, PII; tài khoản ngân hàng chỉ ghi dạng che.

### B2. Ghi gì

Theo quy tắc ở [use-cases-and-results.md](../../02-coding-rules/use-cases-and-results.md): mọi thao tác ghi dữ liệu và sự kiện bảo mật, **sau khi lưu thành công**; **không ghi thao tác đọc**; lỗi ghi nhật ký không làm hỏng thao tác chính. Nhóm hành động hiện có: `login`, `2fa`, `password`, `employee`, `location`, `stock` (chuyển kho, tệp đính kèm chứng từ), `access.denied`; mua hàng, danh mục, nhà cung cấp ghi theo cùng quy ước.

### B3. Xem và bảo toàn

- **Chỉ Admin và Giám đốc xem** (`/audit`).
- **Chỉ thêm, không sửa hoặc xóa qua ứng dụng; lưu vô thời hạn.**
- Danh sách **phân trang chuẩn 20/50**, sắp theo thời gian mới nhất; **lọc theo người, hành động, đối tượng, khoảng ngày**, kèm ô tìm tự do.

## Nợ lệch so với luật

| Vấn đề | Hiện trạng |
| --- | --- |
| Thông báo dùng chung | Đã có thực thể `Notification` (bảng `notification`), chuông header, `/notifications`, mở đúng chứng từ. Cột kho `StockLocationId` có thể null; chưa đổi tên cột thành `LocationId` |
| Vòng đời | Đã đúng trong code cho phiếu chuyển, điều chỉnh, hủy và đơn mua: mỗi bước xóa thông báo cũ của chứng từ rồi tạo thông báo mới (không trùng, biến mất khi bước xong), chỉ người nhận mở được. Chưa có test riêng cho phiếu chuyển/điều chỉnh/hủy |
| Thông báo mua hàng | Đã làm cho gửi duyệt, duyệt, trả về nháp (kèm lý do) và đang về, qua `PurchaseNotifier` và `INoticeStore`. Chưa làm: thông báo khi đơn đã nhận hoặc bị hủy. Người lập đơn xác định theo mã nhân viên trong sự kiện Created của đơn |
| Kiểm kê chờ duyệt | Spec cũ có thông báo cho Trưởng kho; code mới có thông báo cho điều chỉnh và hủy, chưa cho kiểm kê |

## Chờ chốt (chưa có hiệu lực)

- Kênh thông báo ngoài phần mềm (email, Zalo, push): hiện chỉ trong phần mềm.
- Thời hạn lưu thông báo đã xem và dọn thông báo cũ.
- Chính sách lưu trữ nhật ký rất cũ (hiện vô thời hạn).
- Có ghi nhật ký truy cập dữ liệu nhạy cảm (xem giá vốn, xuất file) hay không (xem [role-based-ui.md](../../03-ui-ux/role-based-ui.md)).
