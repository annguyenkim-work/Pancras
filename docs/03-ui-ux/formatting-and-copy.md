# Định dạng và văn bản UI

> Trạng thái: **Đã chốt** (phiên 2026-11-16). Hậu tố tiền `VNĐ` đã được chủ dự án xác nhận, thay quy tắc `đ` cũ.

**Mục đích:** Cách hiển thị tiền, ngày, số và cách viết chữ trên giao diện, để mọi màn giống nhau.

Nguồn code: `src/Pancras.Web/Formatting/` (`VndMoney.cs`, `StockCsv.cs`, `CatalogLabels.cs`...). Domain/Application không định dạng; chỉ Web định dạng.

## 1. Tiền

| Loại | Hiển thị | Ví dụ |
| --- | --- | --- |
| VND | số nhóm theo `vi-VN` (dấu chấm), một dấu cách, hậu tố `VNĐ` | `1.234.567 VNĐ` |

- VND làm tròn đồng, `MidpointRounding.AwayFromZero`; không hiện số lẻ. Số âm `-1.234 VNĐ`; số 0 là `0 VNĐ`.
- Không dùng ký hiệu `$`, không dùng `đ` hay `VND` (không dấu). Hậu tố là `VNĐ` và `USD`.
- Mọi chỗ hiển thị tiền gọi hàm chung (`VndMoney.FormatDong`, `FormatUsd`, `FormatPercent`); không tự `ToString` ở trang. Giờ hiển thị luôn qua `VietnamTime` (UTC+7 cố định), không dùng `ToLocalTime()`.
- Tỷ giá: `25.400 VNĐ / USD`.
- Cột tiền trong bảng căn phải, `font-mono tabular-nums`.
- Ô nhập tiền VNĐ: **một hook duy nhất `data-vnd`** (không dùng class `vnd` hay hook riêng của từng module). **Một script duy nhất** `wwwroot/js/vnd-input.js` (nạp ở `_Head` cho mọi trang) định dạng mọi ô `data-vnd`: nhóm số khi mở trang và ngay khi gõ (giữ vị trí con trỏ), ô chỉ chứa số (`1.500.000`, không thêm chữ "VNĐ" vào ô, đơn vị nằm ở nhãn) — nhãn ô tiền luôn ghi đơn vị, ví dụ "Giá vốn (VNĐ)", và gửi lên máy chủ số thô (`1500000`) qua sự kiện `formdata` mà không đổi ô trên màn hình. Script của trang cần đọc số dùng `window.PancrasVnd.parse`, không tự định dạng (test `MoneyInputsTests` kiểm). Hiện số nhóm `200.000` khi mở form (giá trị đổ sẵn từ server cũng phải đã nhóm) và khi gõ; ô "Số tiền" của điều chỉnh chi phí (cho phép số âm) **không dùng hook**: chủ dự án chốt 2026-11-21 giữ ô số thường, không định dạng nhóm khi gõ. Server làm sạch dấu nhóm rồi đọc bằng `InvariantCulture`.
- Xuất CSV: số thuần `InvariantCulture`, không hậu tố, không dấu nhóm.

> **Đảo quy tắc cũ (đã xác nhận):** CLAUDE.md và spec 005 ghi `đ` và cấm `VND/VNĐ`. Quy tắc mới là `VNĐ`; hàm `FormatDong`, các test liên quan và mọi chỗ đang hiện `đ` phải đổi theo (xem "Nợ lệch"). Khi viết lại CLAUDE.md và glossary, bỏ dòng cấm `VND/VNĐ`.

## 2. Ngày giờ

- Lưu và truyền trong Domain/Application bằng UTC (`DateTimeOffset`).
- Hiển thị theo **giờ Việt Nam cố định** (`SE Asia Standard Time`, UTC+7), không phụ thuộc múi giờ máy chủ.
- Ngày: `dd/MM/yyyy`. Ngày giờ: `dd/MM/yyyy HH:mm` (24 giờ). Có giây chỉ khi nhật ký cần: `dd/MM/yyyy HH:mm:ss`.
- Ô chọn ngày dùng `type="date"`; query string dùng `yyyy-MM-dd`.
- Mọi chỗ hiển thị gọi một hàm chung, không gọi `ToLocalTime()` rải rác.

## 3. Số

- Số lượng, số thập phân: nhóm theo `vi-VN`, tối đa 3 chữ số thập phân, bỏ số 0 thừa.
- Phần trăm: `0.####%` (ví dụ VAT `8%`, `7,5%`), lưu dạng tỷ lệ 0–1.
- Số, mã (SKU, mã nhân viên, số phiếu) dùng `font-mono`; số trong bảng `tabular-nums`.
- Giá trị thiếu hiển thị một dấu gạch `—` (qua `CatalogLabels.Missing`), không để trống hay `null`.

## 4. Chữ trên nút và nhãn

- **Câu thường** (chỉ viết hoa chữ đầu): `Lưu hồ sơ`, `Duyệt phiếu`, `Thêm nhân viên`.
- Nút = động từ + đối tượng. Chỉ để động từ trần khi ngữ cảnh đã rõ (`Lưu`, `Xác nhận`).
- **Đóng modal / bỏ thao tác = `Quay lại`.** `Hủy ...` chỉ dùng khi hủy một đối tượng (`Hủy phiếu`, `Hủy đơn`).
- Ngoại lệ có chủ ý: nút nhận hàng ở mua hàng là `Nhập Kho & Cập Nhật Giá Vốn`; test cấm chuỗi `Nhập kho` trần trên nút này.
- Tiêu đề trang, tên nhóm menu giữ kiểu đang dùng (Viết Hoa Mỗi Từ ở tiêu đề menu, IN HOA ở nhóm menu).
- Dùng từ thống nhất theo [glossary](../00-overview/glossary.md); không đổi tên một khái niệm giữa các màn.

## 5. Thông điệp

- **Ngắn, nói rõ việc và cách sửa, không đổ lỗi cho người dùng.** Mọi thông điệp kết thúc bằng dấu chấm.
- Thành công: kể việc đã xong, `Đã lưu hồ sơ.`
- Lỗi nhập liệu: nói thiếu gì, sai gì: `Nhập số lượng lớn hơn 0.`
- Thiếu quyền: `Bạn không có quyền thực hiện thao tác này.`
- Không lộ mã lỗi, tên enum, stack trace.
- Trạng thái rỗng: `Chưa có ...` kèm gợi ý hoặc nút tạo (qua `_EmptyState`). Không khớp bộ lọc: `Không có ... khớp bộ lọc.`
- Xác nhận nguy hiểm nêu rõ đối tượng và hậu quả, nút xác nhận mang tên hành động (`Hủy phiếu`), không chỉ `OK`.
- PageModel map mã lỗi của Application (enum) sang câu ở một chỗ cho mỗi module; không viết câu lỗi ở Domain/Application.

## Nợ lệch so với quy tắc

| Vấn đề | Hiện trạng |
| --- | --- |
