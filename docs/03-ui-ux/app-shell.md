# Khung ứng dụng

> Trạng thái: **Đã chốt** (phiên 2026-11-16). Nguồn chuẩn là code Razor hiện tại (`Pages/Shared/_Layout.cshtml`).

**Mục đích:** Sidebar, header, thứ tự nhóm menu, trang chủ, layout và bố cục chuẩn của một trang nghiệp vụ.

## 1. Layout

- **`_Layout`** cho mọi trang trong ứng dụng; **`_AuthLayout`** cho trang đăng nhập và xác thực hai bước (không sidebar, nền `bg-slate-100`).
- Cả hai nạp `_Head` (font, CSS, favicon) và dùng `lang="vi"`.
- Nền app `bg-slate-100`, chữ `text-slate-900`, font `font-sans` (xem [design-tokens.md](design-tokens.md)).
- Vùng nội dung `mx-auto w-full max-w-screen-2xl lg:min-w-[40rem]` (căn giữa, có độ rộng tối thiểu và tối đa trên laptop/desktop; không dùng `max-w-*` mà thiếu `mx-auto`, vì thu nhỏ menu sẽ làm nội dung dính trái), padding `p-4 sm:p-6 lg:p-8`; trên mobile thêm `pt-20` để tránh thanh trên cùng cố định.

## 2. Sidebar

- Cố định trái, `w-72`, `bg-slate-900`; dưới 1024px trượt vào từ trái, nền mờ `bg-slate-900/50`, mở bằng nút hamburger.
- Đầu sidebar: logo + **PancrasERP** + dòng phụ **Thiết Bị Vệ Sinh & Nội Thất**.
- Mục đang chọn `bg-sky-600 font-medium text-white`; mục khác `text-slate-100 hover:bg-slate-800`; mục chưa làm `text-slate-500` kèm badge "Sắp có".
- **Sáu nhóm theo đúng thứ tự, không đổi:**
  1. HỆ THỐNG
  2. VẬN HÀNH KHO BÃI
  3. KINH DOANH & PHÂN PHỐI
  4. TÀI CHÍNH
  5. DỊCH VỤ KHÁCH HÀNG
  6. QUẢN TRỊ NỘI BỘ
- Tiêu đề nhóm `text-[11px] font-bold tracking-wider text-slate-400`.
- **Menu là danh sách dữ liệu ở một chỗ** (nhóm, mục, đường dẫn, nhãn, điều kiện hiện qua `CanX`, cờ "chưa làm"); `_Layout` chỉ lặp vòng để vẽ. Thêm module = thêm một dòng dữ liệu.
- Mục người dùng không có quyền **không hiện** (xem [role-based-ui.md](role-based-ui.md)). Module chưa làm vẫn hiện với badge "Sắp có" và trỏ tới trang `ComingSoon` (`ComingSoonModel.Routes` lấy từ cùng danh sách dữ liệu `Web/Navigation/NavigationMenu.cs`); khi làm xong module thì bỏ khỏi danh sách đó.

## 3. Header

- **Desktop** (từ 1024px): thanh trắng `h-16 border-b border-slate-200`: bên trái breadcrumb **`PancrasERP / <Tiêu đề trang>`** (lấy từ `ViewData["Title"]`); bên phải tên nhân viên (liên kết `/profile`), thẻ vai trò `rounded-full bg-sky-50 text-sky-700`, nút **Đăng xuất** (nút phụ, cỡ nhỏ).
- **Mobile** (dưới 1024px): thanh cố định trên cùng `h-14`: hamburger, logo + PancrasERP, liên kết **Hồ sơ**, nút **Đăng xuất**.
- Header không chứa bộ chọn kho, Xuất CSV hay In PDF; các thao tác đó nằm trong từng trang (xem [principles.md](principles.md) về nguồn chuẩn là code hiện tại).
- **Header desktop cá» Äá»nh á» Äáº§u mÃ n hÃ¬nh khi cuá»n trang** (`sticky top-0 z-30`); thanh mobile ÄÃ£ `fixed`. Test: `SidebarCollapseTests`.
- Shell (sidebar, header, thanh mobile) mang class `no-print`.

## 4. Trang chủ

- Trang `/` ("Tổng quan điều hành") **chỉ là danh sách thẻ lối tắt theo vai**, dùng cùng điều kiện `CanX` và cùng danh sách dữ liệu với menu.
- **Không đặt số liệu tổng hợp hay biểu đồ** cho đến khi có spec riêng (nguyên tắc "không thêm dashboard").

## 5. Bố cục một trang nghiệp vụ

Thứ tự từ trên xuống:

1. **Thẻ tiêu đề:** `rounded-xl border border-slate-200 bg-white`, chứa `h1` (`font-display text-xl sm:text-2xl font-bold text-slate-900`) và dòng mô tả ngắn nếu cần.
2. **Hàng lọc / tìm / nút tạo:** form GET `data-live-filter` bên trái; nút hành động chính (Tạo, Thêm) ở góc phải của thẻ tiêu đề hoặc hàng lọc. Bộ đếm `N mục` cùng hàng.
3. **Bảng hoặc danh sách thẻ** (xem [components.md](components.md) mục 4), hoặc `_EmptyState` khi rỗng.
- Tạo/sửa/nhập/phân bổ mở modal `<dialog>`; kết quả sau POST hiện bằng toast.
- Tiêu đề trang đặt qua `ViewData["Title"]` (hiện ở tab trình duyệt dạng `<Tiêu đề> - PancrasERP`, ở breadcrumb và ở `h1`).
- Trang `ComingSoon`: một thẻ tiêu đề + dòng "Tính năng đang được xây dựng" (nội dung ở `ComingSoonModel.Message`).

## Nợ lệch so với quy tắc

| Vấn đề | Hiện trạng |
| --- | --- |
| Tiêu đề trang | Hai chuẩn `h1` lệch (xem components.md); cần thống nhất theo mục 5 |
| Breadcrumb | Chỉ một cấp; chưa có quy tắc cho trang con nếu sau này tách trang chi tiết |

## Quyết định đã chốt (2026-11-16)

- [Đã làm] Nút thu gọn/mở sidebar ở header desktop (`data-sidebar-toggle`, xử lý trong `ui.js`), nhớ lựa chọn cục bộ trên trình duyệt (localStorage). Chưa xem thử bằng trình duyệt khi đăng nhập.
- Chưa cần bộ chọn kho/showroom toàn cục trên header.

## Chờ chốt (chưa có hiệu lực)

- Vị trí chính xác và hình dạng của chuông thông báo trên header (đã chốt có chuông và trang `/notifications`, xem `04-domain/modules/notifications-and-audit.md`).
