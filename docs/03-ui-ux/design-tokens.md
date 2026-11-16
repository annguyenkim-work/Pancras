# Design tokens

> Trạng thái: **Đã chốt một phần** (phiên 2026-11-16). Mục "Chờ chốt" chưa có hiệu lực.

**Mục đích:** Màu, font, cỡ chữ, bo góc dùng chung. Màn mới chỉ dùng giá trị ở đây; cần giá trị mới thì cập nhật file này trước.

Nguồn: `src/Pancras.Web/tailwind.config.js`, `Pages/Shared/_Head.cshtml`, `_Layout.cshtml`.

## 1. Palette (bộ màu cố định)

Sáu họ màu Tailwind. Mỗi họ có một vai trò; không dùng màu khác (indigo, blue, red, green, orange...).

| Họ | Vai trò | Ví dụ đang dùng |
| --- | --- | --- |
| slate | Nền, chữ, viền, nút phụ | nền app `bg-slate-100`, thẻ `bg-white border-slate-200`, sidebar `bg-slate-900`, chữ `text-slate-900/700/500`, nút phụ `bg-slate-100 text-slate-700` |
| sky | Hành động chính, mục đang chọn, liên kết/nhấn | nút chính `bg-sky-600 text-white`, mục menu chọn, thẻ vai trò `bg-sky-50 text-sky-700` |
| emerald | Thành công, hoàn tất, xuất Excel | `bg-emerald-50`, nút xác nhận `bg-emerald-600` |
| rose | Lỗi, nguy hiểm, hủy/xóa | `text-rose-700`, nút nguy hiểm `bg-rose-600`, nền lỗi `bg-rose-50` |
| amber | Cảnh báo, chờ xử lý | `bg-amber-50`, `bg-amber-100` |
| violet | **Phân loại đối tượng**, không phải trạng thái | Hiện dùng cho nút "Kiểm kê" (Inventory) và thẻ "Showroom" (Locations) |

Quy tắc: màu trạng thái (emerald/rose/amber) chỉ dùng đúng nghĩa trạng thái; violet chỉ để phân loại (loại kho, loại nghiệp vụ), không dùng để báo thành công hay lỗi.

## 2. Font

| Token | Font | Dùng cho | Class |
| --- | --- | --- | --- |
| sans | DM Sans (400/500/700) | Chữ nội dung mặc định | `font-sans` (gắn ở `body`) |
| display | Plus Jakarta Sans (600/700) | Tiêu đề, tên app | `font-display` |
| mono | JetBrains Mono (500) | Mã, số tiền, số lượng | `font-mono tabular-nums` |

Font **tự host** trong `wwwroot/fonts` (không dùng Google Fonts, xem [tech-stack.md](../01-architecture/tech-stack.md)). Chỉ dùng các độ đậm đã nạp.

## 3. Cỡ chữ

| Cỡ | Dùng cho |
| --- | --- |
| `text-sm` | Nội dung chính, ô nhập, nút |
| `text-xs` | Bảng, nhãn phụ, chú thích, dấu vết bản ghi |
| `text-[11px]` | Tiêu đề nhóm menu sidebar (in hoa, `tracking-wider`) |
| `text-[10px]` | Badge "Sắp có" |
| `text-base` / `text-lg sm:text-xl` | Tiêu đề modal / tiêu đề trang |

## 4. Bo góc

| Class | Dùng cho |
| --- | --- |
| `rounded-lg` | Mọi nút, ô nhập, mục menu |
| `rounded-xl` | Thẻ nội dung |
| `rounded-2xl` | Modal, thẻ đăng nhập, `_EmptyState` |
| `rounded-full` | Badge vai trò |
| `rounded-md` | Logo, thẻ loại nhỏ |

## 5. Khung

- Nội dung `max-w-7xl`; padding `p-4 sm:p-6 lg:p-8`.
- Sidebar `w-72`; modal xác nhận rộng `min(30rem, 100% - 2rem)`; nền mờ modal/sidebar `bg-slate-900/50`.
- Bóng: dùng `shadow-sm`/`shadow-xl` (modal); không dùng `shadow-xs`.

## Nợ lệch so với tokens

| Vấn đề | Hiện trạng |
| --- | --- |

Định dạng tiền, ngày, số: xem [formatting-and-copy.md](formatting-and-copy.md).

## Chờ chốt (chưa có hiệu lực)

- Quy tắc khoảng cách (padding thẻ, khoảng cách giữa các khối).
- Cách dùng màu trạng thái chi tiết cho badge (vòng đời đơn, phiếu).
