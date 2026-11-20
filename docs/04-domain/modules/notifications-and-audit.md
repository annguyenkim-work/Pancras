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

**Trang `/notifications` (đã chốt 2026-11-16):**
- Sắp theo **thời điểm tạo, mới nhất trước**, không phụ thuộc đã xem hay chưa (thông báo không nhảy vị trí sau khi mở).
- Thông báo **chưa xem** nổi bật: nền xanh nhạt, viền trái xanh, nhãn "Mới". Thông báo **đã xem**: chữ xám, nhãn "Đã xem".
- Thông báo của phiếu chuyển kho, kiểm kê, điều chỉnh, hủy hiện kèm **mã phiếu** (`CK-`, `KK-`, `DC-`, `HY-` + 8 ký tự cuối của Id), cùng mã hiện ở thẻ phiếu trong màn Kho để đối chiếu. Thông báo đơn mua chưa kèm mã (chỉ có Id chứng từ).

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

**Kiểm kê** (đã chốt phiên 2026-11-16): biên bản của nhân viên chờ duyệt báo Trưởng Showroom/Kho đúng kho đó (trừ người lập); duyệt xong thì thông báo biến mất.

**Mua hàng** (đã chốt, đã có trong code):

| Sự kiện | Người nhận |
| --- | --- |
| Gửi duyệt đơn | Admin, Giám đốc, Kế toán (trừ người gửi) |
| Đơn được duyệt hoặc trả về nháp (kèm lý do) | Người lập đơn |
| Đơn bị hủy (kèm lý do) | Người lập đơn, Trưởng Showroom/Kho và Thủ kho của kho nhận (trừ người hủy) |
| Đơn đã nhập kho (một phần hoặc đủ) | Người lập đơn và Admin, Giám đốc, Kế toán (trừ người nhận hàng) |
| Đơn đã duyệt hoặc đang về | Trưởng Showroom/Kho và Thủ kho của kho nhận (không kèm giá) |

## B. Nhật ký hệ thống (`AuditEntry`)

### B1. Cấu trúc

`Id`, thời điểm, người làm (`ActorEmployeeId`, `ActorCode`), **mã hành động**, loại đối tượng, Id đối tượng, chi tiết (tối đa 500 ký tự), địa chỉ IP. Chuỗi bị cắt khi quá dài (mã hành động 60, loại 60, Id 80).

- **Mã hành động** là hằng số trong `AuditActions`, dạng **`nhóm.sự_kiện`** (ví dụ `login.failed`, `employee.role_changed`, `stock.transfer_approved`); UI đổi sang câu tiếng Việt khi hiển thị.
- **Chi tiết không chứa** mật khẩu, mã xác thực, token, khóa bí mật, PII; tài khoản ngân hàng chỉ ghi dạng che.

### B2. Ghi gì

Theo quy tắc ở [use-cases-and-results.md](../../02-coding-rules/use-cases-and-results.md): mọi thao tác ghi dữ liệu và sự kiện bảo mật; thao tác ghi dữ liệu ghi nhật ký **trong cùng giao dịch** với dữ liệu (nhật ký lỗi thì cả thao tác bị hủy), sự kiện không kèm ghi dữ liệu (từ chối quyền, xuất file, tải chứng từ) ghi riêng và lỗi chỉ log cảnh báo; **không ghi thao tác đọc**. Nhóm hành động hiện có: `login`, `2fa`, `password`, `employee`, `location`, `stock` (chuyển kho, tệp đính kèm chứng từ), `access.denied`; mua hàng, danh mục, nhà cung cấp ghi theo cùng quy ước. **Xuất file** (tồn kho, danh mục, đơn mua) cũng ghi nhật ký ai xuất, bao nhiêu dòng; **xem trên màn hình thì không ghi**. Thông báo đã xem chỉ đánh dấu, không tự dọn; nhật ký lưu vô thời hạn (quyết 2026-11-16).

### B3. Xem và bảo toàn

- **Chỉ Admin và Giám đốc xem** (`/audit`).
- **Chỉ thêm, không sửa hoặc xóa qua ứng dụng; lưu vô thời hạn.**
- Danh sách **phân trang keyset 20/50** (chỉ Trước/Sau, không đánh số trang, không đếm tổng; dấu trang là cặp giờ ghi + mã), sắp theo thời gian mới nhất; **lọc theo người, hành động, đối tượng, khoảng ngày**, kèm ô tìm tự do.

## Nợ lệch so với luật

| Vấn đề | Hiện trạng |
| --- | --- |
| Thông báo dùng chung | Đã có thực thể `Notification` (bảng `notification`), chuông header, `/notifications`, mở đúng chứng từ. Cột kho `StockLocationId` có thể null; chưa đổi tên cột thành `LocationId` |
| Vòng đời | Đã đúng trong code cho phiếu chuyển, điều chỉnh, hủy và đơn mua: mỗi bước xóa thông báo cũ của chứng từ rồi tạo thông báo mới (không trùng, biến mất khi bước xong), chỉ người nhận mở được. Chưa có test riêng cho phiếu chuyển/điều chỉnh/hủy |

## Quyết định đã chốt (2026-11-16)

- [Chưa làm] Thêm kênh email cho thông báo (không Zalo, không push); hiện vẫn chỉ trong phần mềm. Dùng chung dịch vụ email với quên mật khẩu (SMTP VinaHost, xem accounts-and-employees.md); còn chờ chốt loại thông báo nào gửi email và cho ai.

## Chờ chốt (chưa có hiệu lực)

Không còn mục chờ chốt.

